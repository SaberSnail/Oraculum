using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.Data;
using Oraculum.Engine;

namespace Oraculum.ViewModels
{
	public sealed class TableViewModel : TreeLeafBase
	{
		public TableViewModel(TableMetadata metadata)
		{
			m_id = metadata.Id;
			m_author = metadata.Author ?? "";
			m_version = metadata.Version;
			m_created = metadata.Created;
			m_modified = metadata.Modified;
			m_groups = metadata.Groups;
			m_randomPlan = metadata.RandomPlan;
			m_useManualRoll = AppModel.Instance.Settings.Get<bool>(SettingsKeys.RollValueManually);
			m_extraTables = new Dictionary<Guid, (TableMetadata Metadata, ValueToResultMapper ResultMapper)>();

			Title = metadata.Title ?? "";

			m_valueGenerators = new List<ValueGeneratorViewModelBase>();
			ValueGenerators = CreateValueGenerators(m_randomPlan, m_useManualRoll);
		}

		public Guid Id
		{
			get => VerifyAccess(m_id);
			set => SetPropertyField(value, ref m_id);
		}

		public string Author
		{
			get => VerifyAccess(m_author);
			set => SetPropertyField(value, ref m_author);
		}

		public int Version
		{
			get => VerifyAccess(m_version);
			set => SetPropertyField(value, ref m_version);
		}

		public DateTime Created
		{
			get => VerifyAccess(m_created);
			set => SetPropertyField(value, ref m_created);
		}

		public DateTime Modified
		{
			get => VerifyAccess(m_modified);
			set => SetPropertyField(value, ref m_modified);
		}

		public IReadOnlyList<string> Groups
		{
			get => VerifyAccess(m_groups);
			set => SetPropertyField(value, ref m_groups);
		}

		public RollLogViewModel? RollLog
		{
			get => VerifyAccess(m_rollLog);
			set => SetPropertyField(value, ref m_rollLog);
		}

		public bool IsWorking
		{
			get => VerifyAccess(m_isWorking);
			set => SetPropertyField(value, ref m_isWorking);
		}

		public bool UseManualRoll
		{
			get => VerifyAccess(m_useManualRoll);
			set
			{
				if (SetPropertyField(value, ref m_useManualRoll))
				{
					AppModel.Instance.Settings.Set(SettingsKeys.RollValueManually, value);
					var generators = CreateValueGenerators(m_randomPlan, m_useManualRoll);
					ValueGenerators = generators;
				}
			}
		}

		public IReadOnlyList<ValueGeneratorViewModelBase> ValueGenerators
		{
			get => VerifyAccess(m_valueGenerators);
			private set
			{
				var oldGenerators = m_valueGenerators;
				if (SetPropertyField(value, ref m_valueGenerators))
				{
					foreach (var generator in oldGenerators)
					{
						generator.RollStarted -= OnRollStarted;
						generator.ValueGenerated -= OnValueGenerated;
					}
					foreach (var generator in m_valueGenerators)
					{
						generator.RollStarted += OnRollStarted;
						generator.ValueGenerated += OnValueGenerated;
					}
				}
			}
		}

		public async Task LoadRowsIfNeededAsync(TaskStateController state)
		{
			VerifyAccess();
			if (m_isLoaded)
				return;

			using var _ = Log.TimedInfo($"Loading table: {Title}");

			await state.ToSyncContext();
			IsWorking = true;
			var tableId = Id;
			var data = AppModel.Instance.Data;

			try
			{
				await state.ToThreadPool();

				var rows = await data.GetRowsAsync(tableId, state.CancellationToken).ConfigureAwait(false);
				var extraTableIds = rows
					.Select(x => x.Next)
					.WhereNotNull()
					.Select(x => x!.Value)
					.Distinct()
					.AsReadOnlyList();
				var extraTables = new List<(TableMetadata Metadata, IReadOnlyList<RowData> Rows)>();
				if (extraTableIds.Count != 0)
				{
					var extraTableMetadatas = await data.GetTableMetadatasAsync(extraTableIds, state.CancellationToken).ConfigureAwait(false);
					foreach (var extraTableMetadata in extraTableMetadatas)
					{
						var extraRows = await data.GetRowsAsync(extraTableMetadata.Id, state.CancellationToken).ConfigureAwait(false);
						if (extraRows is not null)
							extraTables.Add((extraTableMetadata, extraRows));
					}
				}
				await state.ToSyncContext();

				m_resultMappers = m_valueGenerators.Select(x => new ValueToResultMapper(Id, Title, rows)).ToList();
				foreach (var (extraMetadata, extraRows) in extraTables)
				{
					var mapper = new ValueToResultMapper(extraMetadata.Id, extraMetadata.Title, extraRows);
					m_extraTables.Add(extraMetadata.Id, (extraMetadata, mapper));
				}

				m_isLoaded = true;
			}
			finally
			{
				await state.ToSyncContext();
				IsWorking = false;
			}
		}

		public void Roll()
		{
			foreach (var generator in m_valueGenerators)
				generator.Roll();
		}

		private void OnRollStarted(object? sender, EventArgs e)
		{
			if (m_valueGenerators.Where(x => x.IsRollStarted).Count() == 1)
				m_rollLog?.RollStarted(m_id, Title);
		}

		private void OnValueGenerated(object? sender, GenericEventArgs<RandomValueBase> e)
		{
			if (m_valueGenerators.All(x => x.GeneratedValue is not null))
			{
				var values = m_valueGenerators.Select(x => x.GeneratedValue!).AsReadOnlyList();
				var results = new List<RollResult>(m_resultMappers!.Count);
				for (int i = 0; i < m_resultMappers!.Count; i++)
				{
					var result = m_resultMappers[i].GetResult(values[i]);
					results.Add(result);
				}
				TaskWatcher.Execute(async state =>
				{
					foreach (var result in results)
						await m_rollLog!.AddAsync(state, result).ConfigureAwait(false);
					await state.ToSyncContext();
					foreach (var generator in m_valueGenerators)
						generator.OnReportingFinished();
				}, AppModel.Instance.TaskGroup);
			}
		}

		private static IReadOnlyList<ValueGeneratorViewModelBase> CreateValueGenerators(IReadOnlyList<RandomSourceData> randomPlan, bool rollManually)
		{
			List<ValueGeneratorViewModelBase> generators = new();

			foreach (var sourceInfo in randomPlan)
			{
				var source = RandomSourceBase.Create(sourceInfo);
				var generator = ValueGeneratorViewModelBase.Create(source, rollManually);
				generators.Add(generator);
			}

			return generators;
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(TableViewModel));

		private readonly Dictionary<Guid, (TableMetadata Metadata, ValueToResultMapper ResultMapper)> m_extraTables;

		private Guid m_id;
		private string m_author;
		private int m_version;
		private DateTime m_created;
		private DateTime m_modified;
		private IReadOnlyList<string> m_groups;
		private IReadOnlyList<RandomSourceData> m_randomPlan;
		private bool m_isLoaded;
		private bool m_isWorking;
		private RollLogViewModel? m_rollLog;
		private IReadOnlyList<ValueGeneratorViewModelBase> m_valueGenerators;
		private IReadOnlyList<ValueToResultMapper>? m_resultMappers;
		private bool m_useManualRoll;
	}
}
