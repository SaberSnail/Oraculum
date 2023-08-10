using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.Data;
using Oraculum.Engine;
using Oraculum.ViewModels;

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

			Title = metadata.Title ?? "";
			RandomSource = new DiceSourceViewModel((DiceSource) RandomSourceBase.Create(metadata.RandomSource), OnRollStarted, OnRandomValueDisplayedAsync);
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

		public DiceSourceViewModel RandomSource { get; }

		public async Task LoadRowsIfNeededAsync(TaskStateController state)
		{
			VerifyAccess();
			if (m_isLoaded)
				return;

			using var _ = Log.TimedInfo($"Loading table: {Title}");

			await state.ToSyncContext();
			IsWorking = true;
			var tableId = Id;

			try
			{
				await state.ToThreadPool();

				var rows = await AppModel.Instance.Data.GetRowsAsync(tableId, state.CancellationToken).ConfigureAwait(false);

				await state.ToSyncContext();

				m_rows = new RowManager(Id, Title, rows);

				m_isLoaded = true;
			}
			finally
			{
				await state.ToSyncContext();
				IsWorking = false;
			}
		}

		private void OnRollStarted() => m_rollLog?.RollStarted(m_id, Title);

		private async Task OnRandomValueDisplayedAsync(TaskStateController state, object key)
		{
			var result = m_rows?.GetOutput(key);

			if (result is not null)
			{
				Log.Info($"Finished rolling, got {key} : {result.Output}");
				if (m_rollLog is not null)
					await m_rollLog.AddAsync(state, result).ConfigureAwait(false);
			}
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(TableViewModel));

		private Guid m_id;
		private string m_author;
		private int m_version;
		private DateTime m_created;
		private DateTime m_modified;
		private IReadOnlyList<string> m_groups;
		private bool m_isLoaded;
		private bool m_isWorking;
		private RowManager? m_rows;
		private RollLogViewModel? m_rollLog;
	}
}
