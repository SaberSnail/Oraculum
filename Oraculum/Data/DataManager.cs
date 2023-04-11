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

		public async Task<IReadOnlyList<SetMetadata>> GetAllSetMetadataAsync(CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var metadataStreams = await connector.Command("select Metadata from SetMetadata").QueryAsync<byte[]>(cancellationToken).ConfigureAwait(false);
			var metadatas = metadataStreams
				.Select(x => Serializer.Deserialize<SetMetadata>(new ReadOnlySpan<byte>(x)))
				.AsReadOnlyList();
			return metadatas;
		}

		public async Task<IReadOnlyList<TableMetadata>> GetTablesInSetAsync(Guid setId, CancellationToken cancellationToken)
		{
			if (setId == StaticData.AllSetId)
				return await GetAllTableMetadataAsync(cancellationToken).ConfigureAwait(false);

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

		public async Task<IReadOnlyList<TableMetadata>> GetAllTableMetadataAsync(CancellationToken cancellationToken)
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
			var tableIdToDbId = new Dictionary<Guid, long>();
			var tables = GetTableMetadatas();
			foreach (var table in tables)
			{
				using var stream = new MemoryStream();
				Serializer.Serialize(stream, table);

				var dbId = await connector.Command(Sql.Format($@"
					insert into TableMetadata (Metadata, TableId) values ({stream.ToArray()}, {table.Id}) returning RecordId
				")).QuerySingleAsync<long>(cancellationToken).ConfigureAwait(false);
				tableIdToDbId.Add(table.Id, dbId);

				var rows = GetRowDatas(table.Id);
				foreach (var row in rows)
				{
					using var rowStream = new MemoryStream();
					Serializer.Serialize(rowStream, row);

					await connector.Command(Sql.Format($@"
						insert into RowData (TableRecordId, Data) values ({dbId}, {rowStream.ToArray()})
					")).ExecuteAsync(cancellationToken).ConfigureAwait(false);
				}
			}

			var sets = GetSetMetadatas();
			foreach (var set in sets)
			{
				using var stream = new MemoryStream();
				Serializer.Serialize(stream, set);

				var dbId = await connector.Command(Sql.Format($@"
					insert into SetMetadata (Metadata, SetId) values ({stream.ToArray()}, {set.Id}) returning RecordId
				")).QuerySingleAsync<long>(cancellationToken).ConfigureAwait(false);

				var tablesInSet = GetTablesInSet(set.Id);
				foreach (var tableId in tablesInSet)
				{
					await connector.Command(Sql.Format($@"
						insert into SetTables (SetRecordId, TableRecordId) values ({dbId}, {tableIdToDbId[tableId]})
					")).ExecuteAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}

		private static IReadOnlyList<SetMetadata> GetSetMetadatas()
		{
			return new[]
			{
				new SetMetadata
				{
					Id = new Guid(c_ironswornSet),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "RPG", "Ironsworn" },
					Title = "Ironsworn",
				},
				new SetMetadata
				{
					Id = new Guid(c_ironswornCustomSet),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "RPG", "Ironsworn" },
					Title = "Ironsworn (custom)",
				},
			};
		}

		private static IReadOnlyList<TableMetadata> GetTableMetadatas()
		{
			return new[]
			{
				new TableMetadata
				{
					Id = new Guid(c_oracleAlmostCertain),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Oracle" },
					Title = "Almost Certain",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = new Guid(c_oracleLikely),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Oracle" },
					Title = "Likely",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = new Guid(c_oracle5050),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Oracle" },
					Title = "50/50",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = new Guid(c_oracleUnlikely),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Oracle" },
					Title = "Unlikely",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = new Guid(c_oracleSmallChance),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Oracle" },
					Title = "Small Chance",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = new Guid(c_oracleCustomAlmostCertain),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Custom", "Oracle" },
					Title = "Almost Certain",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = new Guid(c_oracleCustomLikely),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Custom", "Oracle" },
					Title = "Likely",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = new Guid(c_oracleCustom5050),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Custom", "Oracle" },
					Title = "50/50",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = new Guid(c_oracleCustomUnlikely),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Custom", "Oracle" },
					Title = "Unlikely",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = new Guid(c_oracleCustomSmallChance),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Custom", "Oracle" },
					Title = "Small Chance",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
			};
		}

		private static IReadOnlyList<RowData> GetRowDatas(Guid tableId)
		{
			return tableId.ToString() switch
			{
				c_oracleAlmostCertain => new[]
				{
					new RowData { Min = 1, Max = 10, Output = "no" },
					new RowData { Min = 11, Max = 100, Output = "yes" },
				},
				c_oracleLikely => new[]
				{
					new RowData { Min = 1, Max = 25, Output = "no" },
					new RowData { Min = 26, Max = 100, Output = "yes" },
				},
				c_oracle5050 => new[]
				{
					new RowData { Min = 1, Max = 50, Output = "no" },
					new RowData { Min = 51, Max = 100, Output = "yes" },
				},
				c_oracleUnlikely => new[]
				{
					new RowData { Min = 1, Max = 75, Output = "no" },
					new RowData { Min = 76, Max = 100, Output = "yes" },
				},
				c_oracleSmallChance => new[]
				{
					new RowData { Min = 1, Max = 90, Output = "no" },
					new RowData { Min = 91, Max = 100, Output = "yes" },
				},
				c_oracleCustomAlmostCertain => new[]
				{
					new RowData { Min = 1, Max = 1, Output = "no, and..." },
					new RowData { Min = 2, Max = 9, Output = "no" },
					new RowData { Min = 10, Max = 10, Output = "no, but..." },
					new RowData { Min = 11, Max = 19, Output = "yes, but..." },
					new RowData { Min = 20, Max = 91, Output = "yes" },
					new RowData { Min = 92, Max = 100, Output = "yes, and..." },
				},
				c_oracleCustomLikely => new[]
				{
					new RowData { Min = 1, Max = 2, Output = "no, and..." },
					new RowData { Min = 3, Max = 23, Output = "no" },
					new RowData { Min = 24, Max = 25, Output = "no, but..." },
					new RowData { Min = 26, Max = 32, Output = "yes, but..." },
					new RowData { Min = 33, Max = 93, Output = "yes" },
					new RowData { Min = 94, Max = 100, Output = "yes, and..." },
				},
				c_oracleCustom5050 => new[]
				{
					new RowData { Min = 1, Max = 5, Output = "no, and..." },
					new RowData { Min = 6, Max = 45, Output = "no" },
					new RowData { Min = 46, Max = 50, Output = "no, but..." },
					new RowData { Min = 51, Max = 55, Output = "yes, but..." },
					new RowData { Min = 56, Max = 95, Output = "yes" },
					new RowData { Min = 96, Max = 100, Output = "yes, and..." },
				},
				c_oracleCustomUnlikely => new[]
				{
					new RowData { Min = 1, Max = 7, Output = "no, and..." },
					new RowData { Min = 8, Max = 68, Output = "no" },
					new RowData { Min = 69, Max = 75, Output = "no, but..." },
					new RowData { Min = 76, Max = 77, Output = "yes, but..." },
					new RowData { Min = 78, Max = 98, Output = "yes" },
					new RowData { Min = 99, Max = 100, Output = "yes, and..." },
				},
				c_oracleCustomSmallChance => new[]
				{
					new RowData { Min = 1, Max = 9, Output = "no, and..." },
					new RowData { Min = 10, Max = 81, Output = "no" },
					new RowData { Min = 82, Max = 90, Output = "no, but..." },
					new RowData { Min = 91, Max = 91, Output = "yes, but..." },
					new RowData { Min = 92, Max = 99, Output = "yes" },
					new RowData { Min = 100, Max = 100, Output = "yes, and..." },
				},
				_ => throw new ArgumentException($"Unknown table ID {tableId}"),
			};
		}

		private static IReadOnlyList<Guid> GetTablesInSet(Guid setId)
		{
			return (setId.ToString() switch
			{
				c_ironswornSet => new[] { c_oracleAlmostCertain, c_oracleLikely, c_oracle5050, c_oracleUnlikely, c_oracleSmallChance },
				c_ironswornCustomSet => new[] { c_oracleCustomAlmostCertain, c_oracleCustomLikely, c_oracleCustom5050, c_oracleCustomUnlikely, c_oracleCustomSmallChance },
				_ => throw new ArgumentException($"Unknown set ID {setId}"),
			})
			.Select(x => new Guid(x))
			.AsReadOnlyList();
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(DataManager));

		private static readonly DbConnectorSettings s_connectorSettings = new()
		{
			AutoOpen = true,
			LazyOpen = true,
			SqlSyntax = SqlSyntax.Sqlite,
		};

		private const int c_dbVersion = 2;

		private const string c_createSql = @"
			create table Info (
				Version integer not null,
				Created text not null,
				Updated text not null
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

		const string c_ironswornSet = "599d53df-5076-4f1e-af03-0abe36991eba";
		const string c_ironswornCustomSet = "04e1a881-9650-4cbb-8781-9f0b31391f83";
		const string c_oracleAlmostCertain = "5eb0f20f-b06f-4c75-9c5d-6cbfe6de34d4";
		const string c_oracleLikely = "f95e9aa9-9787-49a1-abd7-ecc1c6700424";
		const string c_oracle5050 = "52f316f0-bd52-4b0d-af98-cf2833399976";
		const string c_oracleUnlikely = "dc4d8000-653b-4dec-9b65-b66962b0090f";
		const string c_oracleSmallChance = "cde49cd5-49c2-4dce-9cf3-495477f7147b";
		const string c_oracleCustomAlmostCertain = "39a8303a-e2dc-403a-8364-0df4aaafa443";
		const string c_oracleCustomLikely = "ae058877-9e5a-4012-9fbc-3a1c00dfa6eb";
		const string c_oracleCustom5050 = "4511c72d-f5ca-4638-bfbd-0dbbc3a2e8a1";
		const string c_oracleCustomUnlikely = "565fafb0-09c0-467b-b7d4-091afa7704be";
		const string c_oracleCustomSmallChance = "3637fb1e-ed23-49be-bee7-19216456ee01";
	}
}
