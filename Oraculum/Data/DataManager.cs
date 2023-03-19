using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Faithlife.Data;
using Faithlife.Data.SqlFormatting;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using Microsoft.Data.Sqlite;
using Oraculum.Engine;

namespace Oraculum.Data
{
	public sealed class DataManager
	{
		public DataManager() { }

		public async Task InitializeAsync()
		{
			await using var connector = CreateConnector();
			var hasInfoTable = await connector.Command("select name from sqlite_master where type='table' AND name='Info'")
				.QueryFirstOrDefaultAsync<string>().ConfigureAwait(false) is not null;
			if (hasInfoTable)
				await VerifyDbVersionAsync(connector).ConfigureAwait(false);
			else
				await CreateDbAsync(connector).ConfigureAwait(false);
		}

		public async Task<IReadOnlyList<SetMetadata>> GetAllSetMetadataAsync()
		{
			await using var connector = CreateConnector();
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

		public async Task<IReadOnlyList<TableMetadata>> GetTablesInSetAsync(Guid setId)
		{
			var allTables = await GetAllTableMetadataAsync().ConfigureAwait(false);

			if (setId == StaticData.AllSetId)
				return allTables;

			var tableIds = (setId.ToString() switch
			{
				c_ironswornSet => new[] { c_oracleAlmostCertain, c_oracleLikely, c_oracle5050, c_oracleUnlikely, c_oracleSmallChance },
				c_ironswornCustomSet => new[] { c_oracleCustomAlmostCertain, c_oracleCustomLikely, c_oracleCustom5050, c_oracleCustomUnlikely, c_oracleCustomSmallChance },
				_ => throw new ArgumentException($"Unknown set ID {setId.ToString()}"),
			})
			.Select(x => new Guid(x))
			.ToHashSet();

			return allTables.Where(x => tableIds.Contains(x.Id)).AsReadOnlyList();
		}

		public async Task<IReadOnlyList<TableMetadata>> GetAllTableMetadataAsync()
		{
			await using var connector = CreateConnector();
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
					RandomSource = new DiceSource(10, 10, 10),
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
					RandomSource = new DiceSource(100),
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
					RandomSource = new DiceSource(100),
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
					RandomSource = new DiceSource(100),
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
					RandomSource = new DiceSource(100),
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
					RandomSource = new DiceSource(100),
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
					RandomSource = new DiceSource(100),
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
					RandomSource = new DiceSource(100),
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
					RandomSource = new DiceSource(100),
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
					RandomSource = new DiceSource(100),
				},
			};
		}

		public async Task<IReadOnlyList<RowData>> GetRowsAsync(Guid tableId)
		{
			await using var connector = CreateConnector();
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
				_ => throw new ArgumentException($"Unknown table ID {tableId.ToString()}"),
			};
		}

		private static DbConnector CreateConnector() =>
			DbConnector.Create(new SqliteConnection(CreateConnectionString()), s_connectorSettings);

		private static string CreateConnectionString()
		{
			var path = AppModel.Instance.GetOrCreateDataFolder();
			var dbFile = Path.Combine(path, "data.db");
			return $"Data Source={dbFile}";
		}

		private static async Task VerifyDbVersionAsync(DbConnector connector)
		{
			var version = await connector.Command($"select Version from Info")
				.QueryFirstAsync<long>().ConfigureAwait(false);

			Scope logScope = Scope.Empty;
			if (version < c_dbVersion)
				logScope = Log.TimedInfo($"Updating database from version {version} to version {c_dbVersion}.");
			using (logScope)
			{
				while (version < c_dbVersion)
				{
					version++;
				}
			}
		}

		private static async Task CreateDbAsync(DbConnector connector)
		{
			var now = DateTime.UtcNow;
			await using (await connector.BeginTransactionAsync())
			{
				await connector.Command(c_createSql).ExecuteAsync();
				await connector.Command(Sql.Format($@"
					insert into Info (Version, Created, Updated)
					values ({c_dbVersion}, {now}, {now})
				")).ExecuteAsync();

				await connector.CommitTransactionAsync();
			}
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(DataManager));

		private static readonly DbConnectorSettings s_connectorSettings = new DbConnectorSettings
		{
			AutoOpen = true,
			LazyOpen = true,
			SqlSyntax = Faithlife.Data.SqlFormatting.SqlSyntax.Sqlite,
		};

		private const int c_dbVersion = 1;

		private const string c_createSql = @"
			create table Info (
				Version integer not null,
				Created text not null,
				Updated text not null
			)";

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
