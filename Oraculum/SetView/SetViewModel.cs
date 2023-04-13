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
using Oraculum.ViewModels;

namespace Oraculum.SetView
{
	public sealed class SetViewModel : ViewModelBase
	{
		public SetViewModel(SetMetadata metadata)
		{
			m_id = metadata.Id;
			m_author = metadata.Author ?? "";
			m_version = metadata.Version;
			m_created = metadata.Created;
			m_modified = metadata.Modified;
			m_groups = metadata.Groups ?? Array.Empty<string>();
			m_title = metadata.Title ?? "";

			m_tables = new List<TreeNodeBase>();
			Tables = CollectionViewSource.GetDefaultView(m_tables);
			Tables.Filter = MatchesTableFilter;
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

		public string Title
		{
			get => VerifyAccess(m_title);
			set => SetPropertyField(value, ref m_title);
		}

		public string? TableFilter
		{
			get => VerifyAccess(m_tableFilter);
			set
			{
				if (SetPropertyField(value, ref m_tableFilter))
					RefreshTablesFilter(false);
			}
		}

		public bool IsWorking
		{
			get => VerifyAccess(m_isWorking);
			set => SetPropertyField(value, ref m_isWorking);
		}

		public ICollectionView Tables { get; }

		public TreeNodeBase? SelectedTable
		{
			get => VerifyAccess(m_selectedTable);
			set
			{
				if (SetPropertyField(value, ref m_selectedTable))
				{
					if (m_selectedTable is TableViewModel table)
					{
						m_loadSelectedTableWork?.Cancel();
						m_loadSelectedTableWork = TaskWatcher.Create(table.LoadRowsIfNeededAsync, AppModel.Instance.TaskGroup);
					}
				}
			}
		}

		public async Task LoadTablesIfNeededAsync(TaskStateController state)
		{
			VerifyAccess();
			if (m_isLoaded)
				return;

			using var _ = Log.TimedInfo($"Loading set: {Title}");

			await state.ToSyncContext();
			IsWorking = true;
			var setId = Id;

			try
			{
				await state.ToThreadPool();

				var tables = await AppModel.Instance.Data.GetTablesInSetAsync(setId, state.CancellationToken).ConfigureAwait(false);

				await state.ToSyncContext();

				m_tables.Clear();
				foreach (var table in tables)
				{
					TreeBranch? parent = null;
					foreach (var parentTitle in table.Groups)
					{
						var newParent = GetMatchingBranch(parentTitle, parent);
						if (newParent is null)
						{
							newParent = new TreeBranch { Title = parentTitle };
							AddItemToTables(newParent, parent);
						}
						parent = newParent;
					}

					AddItemToTables(new TableViewModel(table), parent);
				}

				m_isLoaded = true;
			}
			finally
			{
				await state.ToSyncContext();
				IsWorking = false;
				RefreshTablesFilter(true);
			}
		}

		public void SelectedTableChanged(TreeNodeBase table) => SelectedTable = table;

		private TreeBranch? GetMatchingBranch(string branchTitle, TreeNodeBase? parentNode)
		{
			var items = (parentNode as TreeBranch)?.GetChildBranches() ??
				m_tables.OfType<TreeBranch>() ??
				Enumerable.Empty<TreeBranch>();
			return items.FirstOrDefault(x => x.Title == branchTitle);
		}

		private void AddItemToTables(TreeNodeBase item, TreeBranch? parent)
		{
			if (parent is null)
			{
				m_tables.Add(item);
			}
			else
			{
				parent.AddChild(item);
				if (item is TreeBranch)
					parent.IsExpanded = true;
			}
		}

		private bool MatchesTableFilter(object item) => ((TreeNodeBase) item).MatchesCurrentFilter();

		private void RefreshTablesFilter(bool force)
		{
			var filter = TableFilter;
			foreach (var table in m_tables)
				table.SetCurrentFilter(filter, force);
			Tables.Refresh();
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(SetViewModel));

		private readonly List<TreeNodeBase> m_tables;

		private Guid m_id;
		private string m_author;
		private int m_version;
		private DateTime m_created;
		private DateTime m_modified;
		private IReadOnlyList<string> m_groups;
		private string m_title;
		private bool m_isWorking;
		private bool m_isLoaded;
		private TreeNodeBase? m_selectedTable;
		private TaskWatcher? m_loadSelectedTableWork;
		private string? m_tableFilter;
	}
}
