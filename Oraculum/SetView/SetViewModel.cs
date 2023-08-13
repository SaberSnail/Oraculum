using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.Data;
using Oraculum.ViewModels;

namespace Oraculum.SetView
{
	public sealed class SetViewModel : ViewModelBase, IDisposable
	{
		public SetViewModel(SetMetadata metadata)
		{
			m_taskGroup = new TaskGroup();
			m_id = metadata.Id;
			m_author = metadata.Author ?? "";
			m_version = metadata.Version;
			m_created = metadata.Created;
			m_modified = metadata.Modified;
			m_groups = metadata.Groups;
			m_title = metadata.Title ?? "";

			m_tables = new List<TreeNodeBase>();
			Tables = CollectionViewSource.GetDefaultView(m_tables);
			Tables.Filter = MatchesTableFilter;
		}

		public event EventHandler<GenericEventArgs<TableViewModel>>? TableSelected;

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

		public TreeNodeBase? SelectedTableNode
		{
			get => VerifyAccess(m_selectedTableNode);
			set
			{
				var oldSelectedNode = m_selectedTableNode;
				if (SetPropertyField(value, ref m_selectedTableNode))
				{
					if (oldSelectedNode is not null)
						oldSelectedNode.IsSelected = false;
					if (value is not null)
						value.IsSelected = true;

					if (value is TableViewModel table)
						SetSelectedTable(table);
				}
			}
		}

		public void ImportTable()
		{
			m_importTableWork?.Cancel();
			m_importTableWork = TaskWatcher.Execute(ImportTableAsync, m_taskGroup);
		}

		private async Task ImportTableAsync(TaskStateController state)
		{
			await state.ToSyncContext();

			var dialog = new Microsoft.Win32.OpenFileDialog
			{
				// Filter = "All Files (*.*)|*.*",
				Title = OurResources.ImportTable,
			};
			var result = dialog.ShowDialog();
			if (result == true)
			{
				var fileName = dialog.FileName;
				Log.Info($"Importing table: {fileName}");
				var (metadata, rows) = await DataManagerUtility.CreateTableDataAsync(state, fileName).ConfigureAwait(false);
				await AppModel.Instance.Data.AddTableAsync(metadata, rows, state.CancellationToken).ConfigureAwait(false);
			}
		}

		public async Task<bool> TryOpenTableAsync(Guid tableId, TaskStateController state)
		{
			await state.ToSyncContext();
			var table = FindNearestTable(SelectedTableNode, tableId);
			if (table is not null)
			{
				SelectedTableNode = table;
				await m_loadSelectedTableWork!.TaskCompleted.ConfigureAwait(false);
			}
			return table is not null;
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

				ClearTables();
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

		public void Dispose()
		{
			DisposableUtility.Dispose(ref m_taskGroup);
		}

		private TreeBranch? GetMatchingBranch(string branchTitle, TreeNodeBase? parentNode)
		{
			var items = (parentNode as TreeBranch)?.GetChildBranches() ??
				m_tables.OfType<TreeBranch>() ??
				Enumerable.Empty<TreeBranch>();
			return items.FirstOrDefault(x => x.Title == branchTitle);
		}

		private void ClearTables()
		{
			foreach (var table in m_tables)
			{
				table.PropertyChanged -= OnTablePropertyChanged;
				table.PropertyChanging -= OnTablePropertyChanging;
			}
			m_tables.Clear();
		}

		private void AddItemToTables(TreeNodeBase item, TreeBranch? parent)
		{
			item.PropertyChanged += OnTablePropertyChanged;
			item.PropertyChanging += OnTablePropertyChanging;
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

		private void OnTablePropertyChanging(object ?sender, PropertyChangingEventArgs e)
		{
			var node = (TreeNodeBase) sender!;
			if (e.IsChanging(nameof(TreeNodeBase.IsSelected)))
			{
				if (!node.IsSelected)
					SelectedTableNode = null;
			}
		}

		private void OnTablePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			var node = (TreeNodeBase) sender!;
			if (e.HasChanged(nameof(TreeNodeBase.IsSelected)))
			{
				if (node.IsSelected)
					SelectedTableNode = node;
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

		private TableViewModel? FindNearestTable(TreeNodeBase? current, Guid targetId)
		{
			if (current is TableViewModel table && table.Id == targetId)
				return table;

			List<TreeNodeBase> nodesToCheck = m_tables.Reverse<TreeNodeBase>().ToList();
			if (current is not null)
			{
				var ancesters = TreeNodeUtility.GetAncesters(current);
				nodesToCheck.Remove(ancesters[0]);
				nodesToCheck.AddRange(ancesters);
			}

			// Find nearest relative with the target ID
			TableViewModel? target = null;
			TreeNodeBase? lastCheck = null;
			foreach (var node in nodesToCheck.Reverse<TreeNodeBase>())
			{
				target = TreeNodeUtility.EnumerateNodes(node, TreeNodeTraversalOrder.BreadthFirst, false, x => x != lastCheck)
					.OfType<TableViewModel>()
					.FirstOrDefault(x => x.Id == targetId);
				if (target is not null)
					break;
				lastCheck = node;
			}

			return target;
		}

		private void SetSelectedTable(TableViewModel table)
		{
			var capturedTable = table;
			m_loadSelectedTableWork?.Cancel();
			m_loadSelectedTableWork = TaskWatcher.Execute(async state =>
			{
				await capturedTable.LoadRowsIfNeededAsync(state).ConfigureAwait(false);
				await state.ToSyncContext();
				TableSelected.Raise(this, new GenericEventArgs<TableViewModel>(capturedTable));
			}, m_taskGroup);
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(SetViewModel));

		private readonly List<TreeNodeBase> m_tables;
		private TaskGroup m_taskGroup;
		private Guid m_id;
		private string m_author;
		private int m_version;
		private DateTime m_created;
		private DateTime m_modified;
		private IReadOnlyList<string> m_groups;
		private string m_title;
		private bool m_isWorking;
		private bool m_isLoaded;
		private TaskWatcher? m_loadSelectedTableWork;
		private TaskWatcher? m_importTableWork;
		private string? m_tableFilter;
		private TreeNodeBase? m_selectedTableNode;
	}
}
