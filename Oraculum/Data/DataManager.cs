using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Faithlife.Data;
using Faithlife.Data.SqlFormatting;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using Microsoft.Data.Sqlite;

namespace Oraculum.Data
{
	public sealed class DataManager
	{
		public DataManager() { }

		public async Task Initialize()
		{
			await using var connector = CreateConnector();
			var hasInfoTable = await connector.Command("select name from sqlite_master where type='table' AND name='Info'")
				.QueryFirstOrDefaultAsync<string>().ConfigureAwait(false) is not null;
			if (hasInfoTable)
				await VerifyDbVersion(connector).ConfigureAwait(false);
			else
				await CreateDb(connector).ConfigureAwait(false);
		}

		public async Task<IReadOnlyList<SetMetadata>> GetAllSetMetadata()
		{
			await using var connector = CreateConnector();
			return new[]
			{
				new SetMetadata
				{
					Id = new Guid("2A78DDFB-0E28-4C46-A558-38CDDFFAF840"),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "RPG", "Ironsworn" },
					Title = "Ironsworn",
				},
				new SetMetadata
				{
					Id = new Guid("95BFB184-C3BB-4300-9D74-33BAEAA6157B"),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "RPG", "Ironsworn" },
					Title = "Ironsworn (custom)",
				},
			};
		}

		public Task<IReadOnlyList<TableMetadata>> GetTablesInSet(Guid setId)
		{
			if (setId == StaticData.AllSetId)
				return GetAllTableMetadata();

			return GetAllTableMetadata();
		}

		public async Task<IReadOnlyList<TableMetadata>> GetAllTableMetadata()
		{
			await using var connector = CreateConnector();
			return new[]
			{
				new TableMetadata
				{
					Id = new Guid("678A8613-375F-478E-9428-C5B34616C386"),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Oracle" },
					Title = "50/50",
				},
				new TableMetadata
				{
					Id = new Guid("2D3EEBC8-A692-4F82-9B95-0C1A880C6470"),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Oracle" },
					Title = "Unlikely",
				},
				new TableMetadata
				{
					Id = new Guid("40926EB9-28E2-4004-B0D5-30C2A67182B1"),
					Author = "SaberSnail",
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					Groups = new[] { "Ironsworn", "Oracle" },
					Title = "Likely",
				},
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

		private static async Task VerifyDbVersion(DbConnector connector)
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

		private static async Task CreateDb(DbConnector connector)
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
	}
}
