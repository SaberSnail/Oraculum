using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Faithlife.Data;
using Faithlife.Data.SqlFormatting;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.Threading;
using ProtoBuf;

namespace Oraculum.Data
{
	public sealed partial class DataManager : IDisposable
	{
		public DataManager()
		{
			m_dispatcher = Dispatcher.CurrentDispatcher;
			m_taskGroup = new TaskGroup();
			m_tableCache = new ConcurrentDictionary<Guid, TableMetadataImpl>();
			m_tableReferencesCache = new ConcurrentDictionary<Guid, TableReference>();
			m_workQueue = new BlockingCollection<Func<TaskStateController, Task>>();
			m_workWatcher = TaskWatcher.Create(ProcessWorkQueueAsync, m_taskGroup);
		}

		public async Task InitializeAsync(TaskStateController state)
		{
			using var connector = CreateConnector();
			var hasInfoTable = await connector.Command("select name from sqlite_master where type='table' AND name='Info'")
				.QueryFirstOrDefaultAsync<string>(state.CancellationToken).ConfigureAwait(false) is not null;
			if (hasInfoTable)
				await VerifyDbVersionAsync(connector, state.CancellationToken).ConfigureAwait(false);
			else
				await CreateDbAsync(connector, state.CancellationToken).ConfigureAwait(false);

			var tableReferences = await connector.Command("select TableId, Title from TableMetadata")
				.QueryAsync<(string id, string title)>(state.CancellationToken)
				.ConfigureAwait(false);
			foreach (var (idString, title) in tableReferences)
			{
				var id = Guid.Parse(idString);
				m_tableReferencesCache.TryAdd(id, new TableReferenceImpl(id, title, this));
			}

			await state.ToSyncContext();

			m_workWatcher.Start();
		}

		public async Task<IReadOnlyList<(string Key, string Value)>> GetAllSettingsAsync(CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();
			var settings = await connector.Command("select Key, Value from Settings")
				.QueryAsync<(string Key, string Value)>(cancellationToken)
				.ConfigureAwait(false);
			return settings;
		}

		public void SetSetting(string key, string value)
		{
			m_workQueue.Add(async state =>
			{
				using var connector = CreateConnector();
				await connector.Command(Sql.Format($"insert into Settings (Key, Value) values ({key}, {value}) on conflict do update set Value={value}"))
					.ExecuteAsync(state.CancellationToken)
					.ConfigureAwait(false);
			});
		}

		public void DeleteSetting(string key)
		{
			m_workQueue.Add(async state =>
			{
				using var connector = CreateConnector();
				await connector.Command(Sql.Format($"delete from Settings where Key = {key}"))
					.ExecuteAsync(state.CancellationToken)
					.ConfigureAwait(false);
			});
		}

		public TableReference? GetTableReference(Guid id) =>
			m_tableReferencesCache.TryGetValue(id, out var value) ? value : null;

		public HashSet<string> GetAllTableTitles() =>
			m_tableReferencesCache.Values.Select(x => x.Title).ToHashSet();

		public async Task WaitForWriteAsync()
		{
			m_workQueue.CompleteAdding();
			await m_workWatcher.Task.ConfigureAwait(false);
		}

		public void Dispose()
		{
			DisposableUtility.Dispose(ref m_taskGroup);
		}

		private void UpdateTableTitle(Guid id, string title) =>
			UpdateTableMetadata(id, Sql.Format($"Title={title}"));

		private void UpdateTableSource(Guid id, string? source) =>
			UpdateTableMetadata(id, Sql.Format($"Source={source}"));

		private void UpdateTableAuthor(Guid id, string? author) =>
			UpdateTableMetadata(id, Sql.Format($"Author={author}"));

		private void UpdateTableRandomPlan(Guid id, RandomPlan randomPlan)
		{
			using var stream = new MemoryStream();
			Serializer.Serialize(stream, randomPlan);
			UpdateTableMetadata(id, Sql.Format($"RandomPlan={stream.ToArray()}"));
		}

		private void UpdateTableGroups(Guid id, IReadOnlyList<string> groups)
		{
			using var stream = new MemoryStream();
			Serializer.Serialize(stream, groups);
			UpdateTableMetadata(id, Sql.Format($"Groups={stream.ToArray()}"));
		}

		private void UpdateTableMetadata(Guid id, Sql updateSql)
		{
			m_workQueue.Add(async state =>
			{
				using var connector = CreateConnector();

				var tableRowId = await connector
					.Command(Sql.Format($@"update TableMetadata set {updateSql}, Modifed={GetCurrentDateOnlyString()}, Version=Version+1 where TableId={id}"))
					.ExecuteAsync(state.CancellationToken)
					.ConfigureAwait(false);
			});
		}

		private static string GetCurrentDateOnlyString() =>
			DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

		private async Task ProcessWorkQueueAsync(TaskStateController state)
		{
			await state.ToThreadPool();

			while (!m_workQueue.IsCompleted)
			{
				if (m_workQueue.TryTake(out var work, Timeout.Infinite, state.CancellationToken))
					await work(state).ConfigureAwait(false);
			}
		}

		// Redo everything below

		public async Task<IReadOnlyList<SetMetadata>> GetAllSetMetadataAsync(CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var metadataStreams = await connector.Command("select Metadata from SetMetadata").QueryAsync<byte[]>(cancellationToken).ConfigureAwait(false);
			var metadatas = metadataStreams
				.Select(x => Serializer.Deserialize<SetMetadata>(new ReadOnlySpan<byte>(x)))
				.AsReadOnlyList();
			return metadatas;
		}

		public async Task<SetMetadata?> GetSetMetadataAsync(Guid setId, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var metadataStream = await connector.Command(Sql.Format($@"
				select Metadata from SetMetadata where SetId = {setId}
			")).QuerySingleOrDefaultAsync<byte[]>(cancellationToken).ConfigureAwait(false);

			return metadataStream is null ? null :
				Serializer.Deserialize<SetMetadata>(new ReadOnlySpan<byte>(metadataStream));
		}

		public async Task<IReadOnlyList<TableMetadata>> GetTablesInSetAsync(Guid setId, CancellationToken cancellationToken)
		{
			if (setId == StaticData.AllSetId)
				return await GetAllTableMetadatasAsync(cancellationToken).ConfigureAwait(false);

			using var connector = CreateConnector();
			var tableIds = await connector.Command(Sql.Format($@"
				select tm.TableId from TableMetadata tm
				join SetTables st on st.TableRecordId = tm.RecordId
				join SetMetadata sm on sm.RecordId = st.SetRecordId
				where sm.SetId = {setId}
			")).QueryAsync<string>(cancellationToken).ConfigureAwait(false);

			return await GetTableMetadatasAsync(tableIds.Select(x => Guid.Parse(x)), cancellationToken).ConfigureAwait(false);
		}

		public async Task<IReadOnlyList<TableMetadata>> GetTableMetadatasAsync(IEnumerable<Guid> tableIds, CancellationToken cancellationToken)
		{
			List<TableMetadata> tables = new List<TableMetadata>();
			List<Guid> uncachedTableIds = new List<Guid>();

			foreach (var tableId in tableIds)
			{
				if (m_tableCache.TryGetValue(tableId, out var table))
					tables.Add(table);
				else
					uncachedTableIds.Add(tableId);
			}

			if (uncachedTableIds.Count != 0)
			{
				using var connector = CreateConnector();

				var sql = Sql.Format($"select TableId, Title, Source, Author, Version, Created, Modified, Description, RandomPlan, Groups from TableMetadata where TableId in ({uncachedTableIds}...)");
				var values = await connector.Command(sql)
					.QueryAsync<(string TableId, string Title, string? Source, string? Author, long Version, string Created, string Modified, string? Description, byte[] RandomPlanBytes, byte[]? GroupsBytes)>(cancellationToken)
					.ConfigureAwait(false);
				var metadataDtos = values
					.Select(x =>
					{
						var randomPlan = Serializer.Deserialize<RandomPlan>(new ReadOnlySpan<byte>(x.RandomPlanBytes));
						var groups = Serializer.Deserialize<IReadOnlyList<string>>(new ReadOnlySpan<byte>(x.GroupsBytes));
						return new TableMetadataDto(
							tableId: Guid.Parse(x.TableId),
							title: x.Title,
							source: x.Source,
							author: x.Author,
							version: (int) x.Version,
							created: DateOnly.ParseExact(x.Created, c_dateFormat, CultureInfo.InvariantCulture),
							modified: DateOnly.ParseExact(x.Modified, c_dateFormat, CultureInfo.InvariantCulture),
							description: x.Description,
							randomPlan: randomPlan,
							groups: groups
							);
					});

				foreach (var metadataDto in metadataDtos)
				{
					var tableReference = (TableReferenceImpl) GetTableReference(metadataDto.TableId)!;
					tables.Add(m_tableCache.GetOrAdd(tableReference!.Id, new TableMetadataImpl(tableReference, metadataDto, this)));
				}
			}

			return tables;
		}

		private async Task<IReadOnlyList<TableMetadata>> GetAllTableMetadatasAsync(CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var tableIds = await connector.Command("select TableId from TableMetadata").QueryAsync<string>(cancellationToken).ConfigureAwait(false);

			return await GetTableMetadatasAsync(tableIds.Select(x => Guid.Parse(x)), cancellationToken).ConfigureAwait(false);
		}

		public async Task<IReadOnlyList<RowDataDto>> GetRowsAsync(Guid tableId, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var metadataStreams = await connector.Command(Sql.Format($@"
				select rd.Data from RowData rd
				join TableMetadata tm on tm.RecordId = rd.TableRecordId
				where tm.TableId = {tableId}
			")).QueryAsync<byte[]>(cancellationToken).ConfigureAwait(false);
			var metadatas = metadataStreams
				.Select(x => Serializer.Deserialize<RowDataDto>(new ReadOnlySpan<byte>(x)))
				.AsReadOnlyList();

			return metadatas;
		}

		public async Task<TableReference> AddTableAsync(TableMetadataDto metadata, IEnumerable<RowDataDto> rows, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			using var transaction = await connector.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
			var tableReference = await AddTableImplAsync(connector, metadata, rows, cancellationToken).ConfigureAwait(false);
			await connector.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

			return tableReference;
		}

		public async Task AddSetAsync(SetMetadata set, IEnumerable<Guid> tableIds, CancellationToken cancellationToken)
		{
			using var stream = new MemoryStream();
			Serializer.Serialize(stream, set);

			using var connector = CreateConnector();
			using var transaction = connector.BeginTransaction();

			var setRecordId = await connector.Command(Sql.Format($@"
					insert into SetMetadata (Metadata, SetId) values ({stream.ToArray()}, {set.Id}) returning RecordId
				")).QuerySingleAsync<long>(cancellationToken).ConfigureAwait(false);

			foreach (var tableId in tableIds)
			{
				var tableRecordId = await GetTableRecordIdAsync(tableId, connector, cancellationToken).ConfigureAwait(false);
				await connector.Command(Sql.Format($@"
					insert into SetTables (SetRecordId, TableRecordId) values ({setRecordId}, {tableRecordId})
				")).ExecuteAsync(cancellationToken).ConfigureAwait(false);
			}
			connector.CommitTransaction();
		}

		private async Task<long> GetTableRecordIdAsync(Guid tableId, DbConnector connector, CancellationToken cancellationToken)
		{
			return await connector.Command(Sql.Format($@"select RecordId from TableMetadata where TableId = {tableId}")).QuerySingleAsync<long>(cancellationToken).ConfigureAwait(false);
		}

		private async Task<TableReference> AddTableImplAsync(DbConnector connector, TableMetadataDto metadata, IEnumerable<RowDataDto> rows, CancellationToken cancellationToken)
		{
			var tableReference = new TableReferenceImpl(metadata.TableId, metadata.Title, this);
			m_tableReferencesCache.TryAdd(metadata.TableId, tableReference);

			var randomPlan = CreateBytes(metadata.RandomPlan);
			var groups = CreateBytes(metadata.Groups);
			var tableRowId = await connector.Command(Sql.Format($@"
					insert into TableMetadata (TableId, Title, Source, Author, Version, Created, Modified, Description, RandomPlan, Groups) values ({metadata.TableId}, {metadata.Title}, {metadata.Source}, {metadata.Author}, {metadata.Version}, {metadata.Created.ToString(c_dateFormat, CultureInfo.InvariantCulture)}, {metadata.Modified.ToString(c_dateFormat, CultureInfo.InvariantCulture)}, {metadata.Description}, {randomPlan}, {groups}) returning RecordId
				")).QuerySingleAsync<long>(cancellationToken).ConfigureAwait(false);

			foreach (var row in rows)
			{
				using var rowStream = new MemoryStream();
				Serializer.Serialize(rowStream, row);

				await connector.Command(Sql.Format($@"
						insert into RowData (TableRecordId, Data) values ({tableRowId}, {rowStream.ToArray()})
					")).ExecuteAsync(cancellationToken).ConfigureAwait(false);
			}

			return tableReference;

			static byte[] CreateBytes<T>(T value)
			{
				using var stream = new MemoryStream();
				Serializer.Serialize(stream, value);
				return stream.ToArray();
			}
		}

		private static DbConnector CreateConnector() =>
			DbConnector.Create(new SqliteConnection(CreateConnectionString()), s_connectorSettings);

		private static string CreateConnectionString()
		{
			var path = AppModel.Instance.GetOrCreateDataFolder();
			var dbFile = Path.Combine(path, "data.db");
			return $"Data Source={dbFile}";
		}

		private static async Task VerifyDbVersionAsync(DbConnector connector, CancellationToken cancellationToken)
		{
			var version = await connector.Command($"select Version from Info")
				.QueryFirstAsync<long>(cancellationToken).ConfigureAwait(false);

			Scope logScope = Scope.Empty;
			if (version < c_dbVersion)
				logScope = Log.TimedInfo($"Updating database from version {version} to version {c_dbVersion}.");
			using (logScope)
			{
				while (version < c_dbVersion)
				{
					/*
					using (await connector.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
					{
						switch (version)
						{
						default:
							throw new NotImplementedException($"Missing implementation of upgrade from version {version}");
						};

						version++;
						var now = DateTime.UtcNow;
						await connector.Command(Sql.Format($@"
							update Info set Version = {version}, Updated = {now}
						")).ExecuteAsync(cancellationToken).ConfigureAwait(false);

						await connector.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
					}
					*/
				}
			}
		}

		private static async Task CreateDbAsync(DbConnector connector, CancellationToken cancellationToken)
		{
			var now = DateTime.UtcNow;
			using (await connector.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
			{
				await connector.Command(c_createSql).ExecuteAsync(cancellationToken).ConfigureAwait(false);
				await connector.Command(Sql.Format($@"
					insert into Info (Version, Created, Updated)
					values ({c_dbVersion}, {now}, {now})
				")).ExecuteAsync(cancellationToken).ConfigureAwait(false);
				await LoadDefaultDataAsync(connector, cancellationToken).ConfigureAwait(false);

				await connector.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		private static async Task LoadDefaultDataAsync(DbConnector connector, CancellationToken cancellationToken)
		{
			/*
			var sets = GetSetMetadatas();
			foreach (var set in sets)
			{
				using var stream = new MemoryStream();
				Serializer.Serialize(stream, set);

				var dbId = await connector.Command(Sql.Format($@"
					insert into SetMetadata (Metadata, SetId) values ({stream.ToArray()}, {set.Id}) returning RecordId
				")).QuerySingleAsync<long>(cancellationToken).ConfigureAwait(false);
			}
			*/
		}

		private static IReadOnlyList<SetMetadata> GetSetMetadatas()
		{
			return new[]
			{
				new SetMetadata
				{
					Id = s_ironswornSet,
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "RPG", "Ironsworn" },
					Title = "Ironsworn",
				},
				new SetMetadata
				{
					Id = s_ironswornCustomSet,
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "RPG", "Ironsworn" },
					Title = "Ironsworn (custom)",
				},
			};
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(DataManager));

		private static readonly DbConnectorSettings s_connectorSettings = new()
		{
			AutoOpen = true,
			LazyOpen = true,
			SqlSyntax = SqlSyntax.Sqlite,
		};

		private const int c_dbVersion = 4;

		private const string c_createSql = @"
			create table Info (
				Version integer not null,
				Created text not null,
				Updated text not null
			);

			create table Settings (
				Key text not null primary key,
				Value text not null
			);

			create table SetMetadata (
				RecordId integer primary key,
				SetId text not null,
				Metadata blob not null
			);

			create table TableMetadata (
				RecordId integer primary key,
				TableId text not null,
				Title text not null,
				Source text,
				Author text,
				Version integer not null,
				Created text not null,
				Modified text not null,
				Description text,
				RandomPlan blob not null,
				Groups blob not null
			);

			create table SetTables (
				SetRecordId integer not null,
				TableRecordId integer not null
			);

			create index SetTables_SetTable on SetTables (SetRecordId, TableRecordId);

			create table RowData (
				TableRecordId integer not null,
				Data blob not null
			);
			";

		private static readonly Guid s_ironswornSet = new Guid("599d53df-5076-4f1e-af03-0abe36991eba");
		private static readonly Guid s_ironswornCustomSet = new Guid("04e1a881-9650-4cbb-8781-9f0b31391f83");
		private const string c_dateFormat = "yyyy-MM-dd";

		private readonly Dispatcher m_dispatcher;
		private readonly ConcurrentDictionary<Guid, TableMetadataImpl> m_tableCache;
		private readonly ConcurrentDictionary<Guid, TableReference> m_tableReferencesCache;
		private readonly BlockingCollection<Func<TaskStateController, Task>> m_workQueue;
		private readonly TaskWatcher m_workWatcher;
		private TaskGroup m_taskGroup;
	}
}
