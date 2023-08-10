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

		public async Task<TableMetadata?> GetTableMetadataAsync(Guid tableId, CancellationToken cancellationToken)
		{
			using var connector = CreateConnector();

			var metadata = await connector.Command(Sql.Format($"select Metadata from TableMetadata where TableId = {tableId}")).QuerySingleAsync<byte[]>(cancellationToken).ConfigureAwait(false);
			return metadata is null ? null : Serializer.Deserialize<TableMetadata>(new ReadOnlySpan<byte>(metadata));
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
				var dbId = await AddTableImplAsync(connector, table, GetRowDatas(table.Id), cancellationToken).ConfigureAwait(false);
				tableIdToDbId.Add(table.Id, dbId);
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

		private static IReadOnlyList<TableMetadata> GetTableMetadatas()
		{
			return new[]
			{
				new TableMetadata
				{
					Id = s_oracleAlmostCertain,
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
					Id = s_oracleLikely,
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
					Id = s_oracle5050,
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
					Id = s_oracleUnlikely,
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
					Id = s_oracleSmallChance,
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
					Id = s_oracleCustomAlmostCertain,
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
					Id = s_oracleCustomLikely,
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
					Id = s_oracleCustom5050,
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
					Id = s_oracleCustomUnlikely,
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
					Id = s_oracleCustomSmallChance,
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Custom", "Oracle" },
					Title = "Small Chance",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				},
				new TableMetadata
				{
					Id = s_action1,
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn" },
					Title = "Action",
					RandomSource = new RandomSourceData { Dice = new[] { 100 } },
				}
			};
		}

		private static IReadOnlyList<RowData> GetRowDatas(Guid tableId)
		{
			return tableId switch
			{
				var id when id == s_oracleAlmostCertain => new[]
				{
					new RowData { Min = 1, Max = 10, Output = "no" },
					new RowData { Min = 11, Max = 100, Output = "yes" },
				},
				var id when id == s_oracleLikely => new[]
				{
					new RowData { Min = 1, Max = 25, Output = "no" },
					new RowData { Min = 26, Max = 100, Output = "yes" },
				},
				var id when id == s_oracle5050 => new[]
				{
					new RowData { Min = 1, Max = 50, Output = "no" },
					new RowData { Min = 51, Max = 100, Output = "yes" },
				},
				var id when id == s_oracleUnlikely => new[]
				{
					new RowData { Min = 1, Max = 75, Output = "no" },
					new RowData { Min = 76, Max = 100, Output = "yes" },
				},
				var id when id ==	s_oracleSmallChance => new[]
				{
					new RowData { Min = 1, Max = 90, Output = "no" },
					new RowData { Min = 91, Max = 100, Output = "yes" },
				},
				var id when id == s_oracleCustomAlmostCertain => new[]
				{
					new RowData { Min = 1, Max = 1, Output = "no, and...", Next = s_action1 },
					new RowData { Min = 2, Max = 9, Output = "no" },
					new RowData { Min = 10, Max = 10, Output = "no, but...", Next = s_action1  },
					new RowData { Min = 11, Max = 19, Output = "yes, but...", Next = s_action1  },
					new RowData { Min = 20, Max = 91, Output = "yes" },
					new RowData { Min = 92, Max = 100, Output = "yes, and...", Next = s_action1  },
				},
				var id when id == s_oracleCustomLikely => new[]
				{
					new RowData { Min = 1, Max = 2, Output = "no, and...", Next = s_action1  },
					new RowData { Min = 3, Max = 23, Output = "no" },
					new RowData { Min = 24, Max = 25, Output = "no, but...", Next = s_action1  },
					new RowData { Min = 26, Max = 32, Output = "yes, but...", Next = s_action1  },
					new RowData { Min = 33, Max = 93, Output = "yes" },
					new RowData { Min = 94, Max = 100, Output = "yes, and...", Next = s_action1  },
				},
				var id when id == s_oracleCustom5050 => new[]
				{
					new RowData { Min = 1, Max = 5, Output = "no, and...", Next = s_action1  },
					new RowData { Min = 6, Max = 45, Output = "no" },
					new RowData { Min = 46, Max = 50, Output = "no, but...", Next = s_action1  },
					new RowData { Min = 51, Max = 55, Output = "yes, but...", Next = s_action1  },
					new RowData { Min = 56, Max = 95, Output = "yes" },
					new RowData { Min = 96, Max = 100, Output = "yes, and...", Next = s_action1  },
				},
				var id when id == s_oracleCustomUnlikely => new[]
				{
					new RowData { Min = 1, Max = 7, Output = "no, and...", Next = s_action1  },
					new RowData { Min = 8, Max = 68, Output = "no" },
					new RowData { Min = 69, Max = 75, Output = "no, but...", Next = s_action1  },
					new RowData { Min = 76, Max = 77, Output = "yes, but...", Next = s_action1  },
					new RowData { Min = 78, Max = 98, Output = "yes" },
					new RowData { Min = 99, Max = 100, Output = "yes, and...", Next = s_action1  },
				},
				var id when id == s_oracleCustomSmallChance => new[]
				{
					new RowData { Min = 1, Max = 9, Output = "no, and...", Next = s_action1  },
					new RowData { Min = 10, Max = 81, Output = "no" },
					new RowData { Min = 82, Max = 90, Output = "no, but...", Next = s_action1  },
					new RowData { Min = 91, Max = 91, Output = "yes, but...", Next = s_action1  },
					new RowData { Min = 92, Max = 99, Output = "yes" },
					new RowData { Min = 100, Max = 100, Output = "yes, and...", Next = s_action1  },
				},
				var id when id == s_action1 => new[]
				{
					new RowData { Min = 1, Max = 1, Output = "Scheme" },
					new RowData { Min = 2, Max = 2, Output = "Clash" },
					new RowData { Min = 3, Max = 3, Output = "Weaken" },
					new RowData { Min = 4, Max = 4, Output = "Initiate" },
					new RowData { Min = 5, Max = 5, Output = "Create" },
					new RowData { Min = 6, Max = 6, Output = "Swear" },
					new RowData { Min = 7, Max = 7, Output = "Avenge" },
					new RowData { Min = 8, Max = 8, Output = "Guard" },
					new RowData { Min = 9, Max = 9, Output = "Defeat" },
					new RowData { Min = 10, Max = 10, Output = "Control" },
					new RowData { Min = 11, Max = 11, Output = "Break" },
					new RowData { Min = 12, Max = 12, Output = "Risk" },
					new RowData { Min = 13, Max = 13, Output = "Surrender" },
					new RowData { Min = 14, Max = 14, Output = "Inspect" },
					new RowData { Min = 15, Max = 15, Output = "Raid" },
					new RowData { Min = 16, Max = 16, Output = "Evade" },
					new RowData { Min = 17, Max = 17, Output = "Assault" },
					new RowData { Min = 18, Max = 18, Output = "Deflect" },
					new RowData { Min = 19, Max = 19, Output = "Threaten" },
					new RowData { Min = 20, Max = 20, Output = "Attack" },
					new RowData { Min = 21, Max = 21, Output = "Leave" },
					new RowData { Min = 22, Max = 22, Output = "Preserve" },
					new RowData { Min = 23, Max = 23, Output = "Manipulate" },
					new RowData { Min = 24, Max = 24, Output = "Remove" },
					new RowData { Min = 25, Max = 25, Output = "Eliminate" },
					new RowData { Min = 26, Max = 26, Output = "Withdraw" },
					new RowData { Min = 27, Max = 27, Output = "Abandon" },
					new RowData { Min = 28, Max = 28, Output = "Investigate" },
					new RowData { Min = 29, Max = 29, Output = "Hold" },
					new RowData { Min = 30, Max = 30, Output = "Focus" },
					new RowData { Min = 31, Max = 31, Output = "Uncover" },
					new RowData { Min = 32, Max = 32, Output = "Breach" },
					new RowData { Min = 33, Max = 33, Output = "Aid" },
					new RowData { Min = 34, Max = 34, Output = "Uphold" },
					new RowData { Min = 35, Max = 35, Output = "Falter" },
					new RowData { Min = 36, Max = 36, Output = "Suppress" },
					new RowData { Min = 37, Max = 37, Output = "Hunt" },
					new RowData { Min = 38, Max = 38, Output = "Share" },
					new RowData { Min = 39, Max = 39, Output = "Destroy" },
					new RowData { Min = 40, Max = 40, Output = "Avoid" },
					new RowData { Min = 41, Max = 41, Output = "Reject" },
					new RowData { Min = 42, Max = 42, Output = "Demand" },
					new RowData { Min = 43, Max = 43, Output = "Explore" },
					new RowData { Min = 44, Max = 44, Output = "Bolster" },
					new RowData { Min = 45, Max = 45, Output = "Seize" },
					new RowData { Min = 46, Max = 46, Output = "Mourn" },
					new RowData { Min = 47, Max = 47, Output = "Reveal" },
					new RowData { Min = 48, Max = 48, Output = "Gather" },
					new RowData { Min = 49, Max = 49, Output = "Defy" },
					new RowData { Min = 50, Max = 50, Output = "Transform" },
					new RowData { Min = 51, Max = 51, Output = "Persevere" },
					new RowData { Min = 52, Max = 52, Output = "Serve" },
					new RowData { Min = 53, Max = 53, Output = "Begin" },
					new RowData { Min = 54, Max = 54, Output = "Move" },
					new RowData { Min = 55, Max = 55, Output = "Coordinate" },
					new RowData { Min = 56, Max = 56, Output = "Resist" },
					new RowData { Min = 57, Max = 57, Output = "Await" },
					new RowData { Min = 58, Max = 58, Output = "Impress" },
					new RowData { Min = 59, Max = 59, Output = "Take" },
					new RowData { Min = 60, Max = 60, Output = "Oppose" },
					new RowData { Min = 61, Max = 61, Output = "Capture" },
					new RowData { Min = 62, Max = 62, Output = "Overwhelm" },
					new RowData { Min = 63, Max = 63, Output = "Challenge" },
					new RowData { Min = 64, Max = 64, Output = "Acquire" },
					new RowData { Min = 65, Max = 65, Output = "Protect" },
					new RowData { Min = 66, Max = 66, Output = "Finish" },
					new RowData { Min = 67, Max = 67, Output = "Strengthen" },
					new RowData { Min = 68, Max = 68, Output = "Restore" },
					new RowData { Min = 69, Max = 69, Output = "Advance" },
					new RowData { Min = 70, Max = 70, Output = "Command" },
					new RowData { Min = 71, Max = 71, Output = "Refuse" },
					new RowData { Min = 72, Max = 72, Output = "Find" },
					new RowData { Min = 73, Max = 73, Output = "Deliver" },
					new RowData { Min = 74, Max = 74, Output = "Hide" },
					new RowData { Min = 75, Max = 75, Output = "Fortify" },
					new RowData { Min = 76, Max = 76, Output = "Betray" },
					new RowData { Min = 77, Max = 77, Output = "Secure" },
					new RowData { Min = 78, Max = 78, Output = "Arrive" },
					new RowData { Min = 79, Max = 79, Output = "Affect" },
					new RowData { Min = 80, Max = 80, Output = "Change" },
					new RowData { Min = 81, Max = 81, Output = "Defend" },
					new RowData { Min = 82, Max = 82, Output = "Debate" },
					new RowData { Min = 83, Max = 83, Output = "Support" },
					new RowData { Min = 84, Max = 84, Output = "Follow" },
					new RowData { Min = 85, Max = 85, Output = "Construct" },
					new RowData { Min = 86, Max = 86, Output = "Locate" },
					new RowData { Min = 87, Max = 87, Output = "Endure" },
					new RowData { Min = 88, Max = 88, Output = "Release" },
					new RowData { Min = 89, Max = 89, Output = "Lose" },
					new RowData { Min = 90, Max = 90, Output = "Reduce" },
					new RowData { Min = 91, Max = 91, Output = "Escalate" },
					new RowData { Min = 92, Max = 92, Output = "Distract" },
					new RowData { Min = 93, Max = 93, Output = "Journey" },
					new RowData { Min = 94, Max = 94, Output = "Escort" },
					new RowData { Min = 95, Max = 95, Output = "Learn" },
					new RowData { Min = 96, Max = 96, Output = "Communicate" },
					new RowData { Min = 97, Max = 97, Output = "Depart" },
					new RowData { Min = 98, Max = 98, Output = "Search" },
					new RowData { Min = 99, Max = 99, Output = "Charge" },
					new RowData { Min = 100, Max = 100, Output = "Summon" },
				},
				_ => throw new ArgumentException($"Unknown table ID {tableId}"),
			};
		}

		private static IReadOnlyList<Guid> GetTablesInSet(Guid setId)
		{
			return (setId switch
			{
				var id when id == s_ironswornSet => new[]
				{
					s_oracleAlmostCertain,
					s_oracleLikely,
					s_oracle5050,
					s_oracleUnlikely,
					s_oracleSmallChance,
					s_action1,
				},
				var id when id == s_ironswornCustomSet => new[]
				{
					s_oracleCustomAlmostCertain,
					s_oracleCustomLikely,
					s_oracleCustom5050,
					s_oracleCustomUnlikely,
					s_oracleCustomSmallChance
				},
				_ => throw new ArgumentException($"Unknown set ID {setId}"),
			})
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

		private static readonly Guid s_ironswornSet = new Guid("599d53df-5076-4f1e-af03-0abe36991eba");
		private static readonly Guid s_ironswornCustomSet = new Guid("04e1a881-9650-4cbb-8781-9f0b31391f83");
		private static readonly Guid s_oracleAlmostCertain = new Guid("5eb0f20f-b06f-4c75-9c5d-6cbfe6de34d4");
		private static readonly Guid s_oracleLikely = new Guid("f95e9aa9-9787-49a1-abd7-ecc1c6700424");
		private static readonly Guid s_oracle5050 = new Guid("52f316f0-bd52-4b0d-af98-cf2833399976");
		private static readonly Guid s_oracleUnlikely = new Guid("dc4d8000-653b-4dec-9b65-b66962b0090f");
		private static readonly Guid s_oracleSmallChance = new Guid("cde49cd5-49c2-4dce-9cf3-495477f7147b");
		private static readonly Guid s_oracleCustomAlmostCertain = new Guid("39a8303a-e2dc-403a-8364-0df4aaafa443");
		private static readonly Guid s_oracleCustomLikely = new Guid("ae058877-9e5a-4012-9fbc-3a1c00dfa6eb");
		private static readonly Guid s_oracleCustom5050 = new Guid("4511c72d-f5ca-4638-bfbd-0dbbc3a2e8a1");
		private static readonly Guid s_oracleCustomUnlikely = new Guid("565fafb0-09c0-467b-b7d4-091afa7704be");
		private static readonly Guid s_oracleCustomSmallChance = new Guid("3637fb1e-ed23-49be-bee7-19216456ee01");
		private static readonly Guid s_action1 = new Guid("4a1b51b0-2bfc-4868-a7fa-6c49ac5f2c5a");
		private static readonly Guid s_theme1 = new Guid("24e00c58-fc1d-4389-b71d-8d81275a39ed");
	}
}
