using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.Data;
using Oraculum.Engine;

namespace Oraculum.ViewModels
{
	public sealed class TableViewModel : TreeLeafBase, IDisposable
	{
		public TableViewModel(TableMetadata metadata)
		{
			m_metadata = metadata;
			m_metadata.PropertyChanged += OnMetadataPropertyChanged;

			Title = metadata.Title;
			m_author = metadata.Author ?? "";
			m_version = metadata.Version;
			m_created = metadata.Created;
			m_modified = metadata.Modified;
			m_groups = metadata.Groups;
			m_randomPlan = metadata.RandomPlan;
			m_randomSource = RandomSourceBase.Create(m_randomPlan);
			m_valueGenerator = new ValueGenerator(metadata.TableReference, m_randomSource, OnValueGenerated);
			m_useManualRoll = AppModel.Instance.Settings.Get<bool>(SettingsKeys.RollValueManually);
			m_extraTables = new Dictionary<Guid, (TableMetadata Metadata, ValueToResultMapper ResultMapper)>();

			AppModel.Instance.Settings.SettingChanged += OnSettingChanged;
		}

		public ValueGenerator ValueGenerator => m_valueGenerator;

		public TableReference TableReference => m_metadata.TableReference;

		public string Author
		{
			get => VerifyAccess(m_author);
			set => SetPropertyField(value, ref m_author);
		}

		public int Version
		{
			get => VerifyAccess(m_version);
		}

		public DateOnly Created
		{
			get => VerifyAccess(m_created);
		}

		public DateOnly Modified
		{
			get => VerifyAccess(m_modified);
		}

		public IReadOnlyList<string> Groups
		{
			get => VerifyAccess(m_groups);
			set => SetPropertyField(value, ref m_groups);
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
					AppModel.Instance.Settings.Set(SettingsKeys.RollValueManually, value);
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
			var tableId = m_metadata.Id;
			var data = AppModel.Instance.Data;

			try
			{
				await state.ToThreadPool();

				var rows = await data.GetRowsAsync(tableId, state.CancellationToken).ConfigureAwait(false);

				/*
				var extraTableIds = rows
					.SelectMany(x => TokenStringUtility.GetTableReferences(x.Output).Select(x => x.Id))
					.Distinct()
					.AsReadOnlyList();
				var extraTables = new List<(TableMetadata Metadata, IReadOnlyList<RowDataDto> Rows)>();
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
				*/

				await state.ToSyncContext();

				m_resultMapper = new ValueToResultMapper(TableReference, m_randomSource, rows);

				/*
				foreach (var (extraMetadata, extraRows) in extraTables)
				{
					var mapper = new ValueToResultMapper(extraMetadata.Id, extraMetadata.Title, extraRows);
					m_extraTables.Add(extraMetadata.Id, (extraMetadata, mapper));
				}
				*/

				m_isLoaded = true;
			}
			finally
			{
				await state.ToSyncContext();
				IsWorking = false;
			}
		}

		public void Roll() => m_valueGenerator.Roll();

		public void CommitChanges()
		{
			m_isUpdatingMetadata++;

			m_metadata.Author = Author;
			m_metadata.Groups = Groups;

			m_isUpdatingMetadata--;
		}

		public void Dispose()
		{
			m_metadata.PropertyChanged -= OnMetadataPropertyChanged;
			AppModel.Instance.Settings.SettingChanged += OnSettingChanged;
			DisposableUtility.Dispose(ref m_valueGenerator);
		}

		private void OnValueGenerated(RandomValueBase value)
		{
			var result = m_resultMapper!.GetResult(value);
			AppModel.Instance.RollLog!.Add(result);
		}

		private void OnMetadataPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (m_isUpdatingMetadata > 0)
				return;

			switch (e.PropertyName)
			{
				case nameof(TableMetadata.Author):
					Author = m_metadata.Author ?? "";
					break;
				case nameof(TableMetadata.Version):
					SetPropertyField(nameof(Version), m_metadata.Version, ref m_version);
					break;
				case nameof(TableMetadata.Modified):
				SetPropertyField(nameof(Modified), m_metadata.Modified, ref m_modified);
					break;
				case nameof(TableMetadata.Groups):
					Groups = m_metadata.Groups;
					break;
				case nameof(TableMetadata.RandomPlan):
					throw new NotImplementedException("Updating of RandomPlan is not yet implemented.");
				default:
					throw new InvalidOperationException($"Unexpected property name: {e.PropertyName}");
			}
		}

		private void OnSettingChanged(object? sender, GenericEventArgs<string> e)
		{
			if (e.Value == SettingsKeys.RollValueManually)
				UseManualRoll = AppModel.Instance.Settings.Get<bool>(SettingsKeys.RollValueManually);
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(TableViewModel));

		private readonly TableMetadata m_metadata;
		private readonly Dictionary<Guid, (TableMetadata Metadata, ValueToResultMapper ResultMapper)> m_extraTables;

		private int m_isUpdatingMetadata;
		private string m_author;
		private int m_version;
		private DateOnly m_created;
		private DateOnly m_modified;
		private IReadOnlyList<string> m_groups;
		private RandomPlan m_randomPlan;
		private RandomSourceBase m_randomSource;
		private bool m_isLoaded;
		private bool m_isWorking;
		private ValueGenerator m_valueGenerator;
		private ValueToResultMapper? m_resultMapper;
		private bool m_useManualRoll;
	}
}
