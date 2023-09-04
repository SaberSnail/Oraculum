using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Faithlife.Data;
using Faithlife.Data.SqlFormatting;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using Microsoft.Data.Sqlite;
using ProtoBuf;

namespace Oraculum.Data
{
	public sealed class DataManager
	{
		public DataManager() { }

		public async Task InitializeAsync(CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();
			var hasInfoTable = await connector.Command("select name from sqlite_master where type='table' AND name='Info'")
				.QueryFirstOrDefaultAsync<string>(cancellationToken).ConfigureAwait(false) is not null;
			if (hasInfoTable)
				await VerifyDbVersionAsync(connector, cancellationToken).ConfigureAwait(false);
			else
				await CreateDbAsync(connector, cancellationToken).ConfigureAwait(false);
		}

		public async Task<IReadOnlyList<(string Key, string Value)>> GetAllSettingsAsync(CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();
			var settings = await connector.Command("select Key, Value from Settings")
				.QueryAsync<(string Key, string Value)>(cancellationToken)
				.ConfigureAwait(false);
			return settings;
		}

		public async Task<string> GetSettingAsync(string key, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();
			return await connector.Command(Sql.Format($"select Value from Settings where Key = {key}"))
				.QuerySingleAsync<string>(cancellationToken)
				.ConfigureAwait(false);
		}

		public async Task SetSettingAsync(string key, string value, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();
			await connector.Command(Sql.Format($"insert into Settings (Key, Value) values ({key}, {value}) on conflict do update set Value={value}"))
				.ExecuteAsync(cancellationToken)
				.ConfigureAwait(false);
		}

		public async Task DeleteSettingAsync(string key, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();
			await connector.Command(Sql.Format($"delete from Settings where Key = {key}"))
				.ExecuteAsync(cancellationToken)
				.ConfigureAwait(false);
		}

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
			var metadataStreams = await connector.Command(Sql.Format($@"
				select tm.Metadata from TableMetadata tm
				join SetTables st on st.TableRecordId = tm.RecordId
				join SetMetadata sm on sm.RecordId = st.SetRecordId
				where sm.SetId = {setId}
			")).QueryAsync<byte[]>(cancellationToken).ConfigureAwait(false);
			var metadatas = metadataStreams
				.Select(x => Serializer.Deserialize<TableMetadata>(new ReadOnlySpan<byte>(x)))
				.AsReadOnlyList();

			return metadatas;
		}

		public async Task<TableMetadata?> GetTableMetadataAsync(Guid tableId, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var sql = Sql.Format($"select Metadata from TableMetadata where TableId = {tableId}");
			var metadata = await connector.Command(sql)
				.QuerySingleOrDefaultAsync<byte[]?>(cancellationToken).ConfigureAwait(false);
			return metadata is null ? null : Serializer.Deserialize<TableMetadata>(new ReadOnlySpan<byte>(metadata));
		}

		public async Task<IReadOnlyList<TableMetadata>> GetTableMetadatasAsync(IEnumerable<Guid> tableIds, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var sql = Sql.Format($"select Metadata from TableMetadata where TableId in ({tableIds}...)");
			var metadataStreams = await connector.Command(sql)
				.QueryAsync<byte[]?>(cancellationToken).ConfigureAwait(false);
			var metadatas = metadataStreams
				.Select(x => Serializer.Deserialize<TableMetadata>(new ReadOnlySpan<byte>(x)))
				.AsReadOnlyList();
			return metadatas;
		}

		public async Task<IReadOnlyList<TableMetadata>> GetAllTableMetadatasAsync(CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var metadataStreams = await connector.Command("select Metadata from TableMetadata").QueryAsync<byte[]>(cancellationToken).ConfigureAwait(false);
			var metadatas = metadataStreams
				.Select(x => Serializer.Deserialize<TableMetadata>(new ReadOnlySpan<byte>(x)))
				.AsReadOnlyList();

			return metadatas;
		}

		public async Task<IReadOnlyList<RowData>> GetRowsAsync(Guid tableId, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var metadataStreams = await connector.Command(Sql.Format($@"
				select rd.Data from RowData rd
				join TableMetadata tm on tm.RecordId = rd.TableRecordId
				where tm.TableId = {tableId}
			")).QueryAsync<byte[]>(cancellationToken).ConfigureAwait(false);
			var metadatas = metadataStreams
				.Select(x => Serializer.Deserialize<RowData>(new ReadOnlySpan<byte>(x)))
				.AsReadOnlyList();

			return metadatas;
		}

		public async Task<HashSet<string>> GetAllTableTitlesAsync(CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var metadataStreams = await connector.Command("select Metadata from TableMetadata").QueryAsync<byte[]>(cancellationToken).ConfigureAwait(false);
			return metadataStreams
				.Select(x => Serializer.Deserialize<TableMetadata>(new ReadOnlySpan<byte>(x)).Title)
				.ToHashSet();
		}

		public async Task AddTableAsync(TableMetadata metadata, IEnumerable<RowData> rows, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			using var transaction = await connector.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
			await AddTableImplAsync(connector, metadata, rows, cancellationToken).ConfigureAwait(false);
			await connector.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
		}

		private static async Task<long> AddTableImplAsync(DbConnector connector, TableMetadata metadata, IEnumerable<RowData> rows, CancellationToken cancellationToken)
		{
			using var stream = new MemoryStream();
			Serializer.Serialize(stream, metadata);

			var dbId = await connector.Command(Sql.Format($@"
					insert into TableMetadata (Metadata, TableId) values ({stream.ToArray()}, {metadata.Id}) returning RecordId
				")).QuerySingleAsync<long>(cancellationToken).ConfigureAwait(false);

			foreach (var row in rows)
			{
				using var rowStream = new MemoryStream();
				Serializer.Serialize(rowStream, row);

				await connector.Command(Sql.Format($@"
						insert into RowData (TableRecordId, Data) values ({dbId}, {rowStream.ToArray()})
					")).ExecuteAsync(cancellationToken).ConfigureAwait(false);
			}

			return dbId;
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
					using (await connector.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
					{
						switch (version)
						{
						case 2:
							await UpdateDatabaseFrom2To3Async(connector, cancellationToken).ConfigureAwait(false);
							break;
						case 1:
							await UpdateDatabaseFrom1To2Async(connector, cancellationToken).ConfigureAwait(false);
							break;
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
				}
			}
		}

		private static async Task UpdateDatabaseFrom2To3Async(DbConnector connector, CancellationToken cancellationToken)
		{
			await connector.Command(@"
				create table Settings (
					Key string not null primary key,
					Value string not null
				);
			").ExecuteAsync(cancellationToken).ConfigureAwait(false);
		}

		private static async Task UpdateDatabaseFrom1To2Async(DbConnector connector, CancellationToken cancellationToken)
		{
			await connector.Command(@"
				create table SetMetadata (
					RecordId integer primary key,
					SetId string not null,
					Metadata blob not null
				);

				create table TableMetadata (
					RecordId integer primary key,
					TableId string not null,
					Metadata blob not null
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
			").ExecuteAsync(cancellationToken).ConfigureAwait(false);
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
			var sets = GetSetMetadatas();
			foreach (var set in sets)
			{
				using var stream = new MemoryStream();
				Serializer.Serialize(stream, set);

				var dbId = await connector.Command(Sql.Format($@"
					insert into SetMetadata (Metadata, SetId) values ({stream.ToArray()}, {set.Id}) returning RecordId
				")).QuerySingleAsync<long>(cancellationToken).ConfigureAwait(false);
			}
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

		private const int c_dbVersion = 3;

		private const string c_createSql = @"
			create table Info (
				Version integer not null,
				Created text not null,
				Updated text not null
			);

			create table Settings (
				Key string not null primary key,
				Value string not null
			);

			create table SetMetadata (
				RecordId integer primary key,
				SetId string not null,
				Metadata blob not null
			);

			create table TableMetadata (
				RecordId integer primary key,
				TableId string not null,
				Metadata blob not null
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
	}
}
