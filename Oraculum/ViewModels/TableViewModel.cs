using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.Data;
using Oraculum.Engine;

namespace Oraculum.ViewModels
{
	public abstract class TreeNodeBase : ViewModelBase
	{
		public string? Title
		{
			get => VerifyAccess(m_title);
			set => SetPropertyField(value, ref m_title);
		}

		public void SetCurrentFilter(string? filterText, bool force)
		{
			VerifyAccess();
			if (!force && m_currentFilter == filterText)
				return;

			m_currentFilter = filterText;
			m_lastMatchesFilter = null;

			SetCurrentFilterCore(filterText, force);
		}

		public bool MatchesCurrentFilter()
		{
			if (m_lastMatchesFilter.HasValue)
				return m_lastMatchesFilter.Value;

			m_lastMatchesFilter = MatchesCurrentFilterCore();
			return m_lastMatchesFilter.Value;
		}

		protected string? GetCurrentFilter() => m_currentFilter;

		protected virtual bool MatchesCurrentFilterCore()
		{
			var filterText = GetCurrentFilter();

			if (string.IsNullOrEmpty(filterText))
				return true;

			if (string.IsNullOrEmpty(Title))
				return false;

			return Title.Contains(filterText, StringComparison.CurrentCultureIgnoreCase);
		}

		protected virtual void SetCurrentFilterCore(string? filterText, bool force)
		{
		}

		private string? m_title;
		private string? m_currentFilter;
		private bool? m_lastMatchesFilter;
	}

	public sealed class TreeBranch : TreeNodeBase
	{
		public TreeBranch()
		{
			m_children = new List<TreeNodeBase>();
			Children = CollectionViewSource.GetDefaultView(m_children);
			Children.Filter = x => (x as TreeNodeBase)?.MatchesCurrentFilter() ?? false;
		}

		public ICollectionView Children { get; }

		public bool IsExpanded
		{
			get => VerifyAccess(m_isExpanded);
			set => SetPropertyField(value, ref m_isExpanded);
		}

		public IEnumerable<TreeBranch> GetChildBranches() => m_children.OfType<TreeBranch>();

		public void AddChild(TreeNodeBase child) => m_children.Add(child);

		protected override bool MatchesCurrentFilterCore() =>
			base.MatchesCurrentFilterCore() || m_children.Any(x => x.MatchesCurrentFilter());

		protected override void SetCurrentFilterCore(string? filterText, bool force)
		{
			var adjustedFilterText = filterText;
			if (base.MatchesCurrentFilterCore())
				adjustedFilterText = null;

			foreach (var child in m_children)
				child.SetCurrentFilter(adjustedFilterText, force);
			Children.Refresh();
		}

		private readonly List<TreeNodeBase> m_children;

		private bool m_isExpanded;
	}

	public abstract class TreeLeafBase : TreeNodeBase
	{
	}

	public sealed class TableViewModel : TreeLeafBase
	{
		public TableViewModel(TableMetadata metadata)
		{
			m_id = metadata.Id;
			m_author = metadata.Author ?? "";
			m_version = metadata.Version;
			m_created = metadata.Created;
			m_modified = metadata.Modified;
			m_groups = metadata.Groups ?? Array.Empty<string>();

			Title = metadata.Title ?? "";
			RandomSource = new DiceSourceViewModel((DiceSource) RandomSourceBase.Create(metadata.RandomSource), OnRandomValueDisplayed);
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

				m_rows = new RowManager(rows);

				m_isLoaded = true;
			}
			finally
			{
				await state.ToSyncContext();
				IsWorking = false;
			}
		}

		private void OnRandomValueDisplayed(object? key)
		{
			Log.Info($"Finished rolling, got {key} : {m_rows?.GetOutput(key)}");
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
	}
}
