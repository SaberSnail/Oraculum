using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.Data;
using Oraculum.SetsView;
using Oraculum.SetView;
using Oraculum.ViewModels;

namespace Oraculum.MainWindow
{
	public sealed class MainWindowViewModel : ViewModelBase, IDisposable
	{
		public MainWindowViewModel()
		{
			m_taskGroup = new TaskGroup();
			AllSets = new SetsViewModel();
			m_openSets = new ObservableCollection<SetViewModel>();
			OpenSets = new ReadOnlyObservableCollection<SetViewModel>(m_openSets);
			m_openSets.Add(new SetViewModel(StaticData.AllSet));
		}

		public bool IsSetsPanelVisible
		{
			get => VerifyAccess(m_isSetsPanelVisible);
			private set => SetPropertyField(value, ref m_isSetsPanelVisible);
		}

		public bool IsEditTablePanelVisible
		{
			get => VerifyAccess(m_isEditTablePanelVisible);
			private set => SetPropertyField(value, ref m_isEditTablePanelVisible);
		}

		public SetsViewModel AllSets { get; }

		public ReadOnlyObservableCollection<SetViewModel> OpenSets { get; }

		public SetViewModel? SelectedSet
		{
			get => VerifyAccess(m_selectedSet);
			set
			{
				var oldValue = m_selectedSet;
				if (SetPropertyField(value, ref m_selectedSet))
				{
					SelectedTable = null;

					if (oldValue is not null)
					{
						oldValue.PropertyChanged -= OnSelectedSetPropertyChanged;
						oldValue.TableSelected -= OnSelectedTableChanged;
					}

					if (m_selectedSet is not null)
					{
						m_loadSelectedSetWork?.Cancel();
						m_loadSelectedSetWork = TaskWatcher.Execute(m_selectedSet.LoadTablesIfNeededAsync, m_taskGroup);

						m_selectedSet.PropertyChanged += OnSelectedSetPropertyChanged;
						m_selectedSet.TableSelected += OnSelectedTableChanged;
					}
				}
			}
		}

		public TableViewModel? SelectedTable
		{
			get => VerifyAccess(m_selectedTable);
			set => SetPropertyField(value, ref m_selectedTable);
		}

		public RollLogViewModel? RollLog => AppModel.Instance.RollLog;

		public void SetLightMode() =>
			AppModel.Instance.CurrentTheme = new Uri(@"/Themes/Default/Default.xaml", UriKind.Relative);

		public void SetDarkMode() =>
			AppModel.Instance.CurrentTheme = new Uri(@"/Themes/Dark/Dark.xaml", UriKind.Relative);

		public void ToggleIsSetsPanelVisible() =>
			IsSetsPanelVisible = !IsSetsPanelVisible;

		public void ToggleTableView() =>
			IsEditTablePanelVisible = !IsEditTablePanelVisible;

		public async Task OpenTableAsync(TableReference table, TaskStateController state)
		{
			await state.ToSyncContext();
			if (SelectedSet is null || table == SelectedTable?.TableReference)
				return;

			await m_loadSelectedSetWork!.TaskCompleted.ConfigureAwait(false);
			if (!await SelectedSet.TryOpenTableAsync(table, state).ConfigureAwait(false))
			{
				await state.ToSyncContext();
				SelectedSet = m_openSets[0];

				await m_loadSelectedSetWork.TaskCompleted.ConfigureAwait(false);
				if (!await SelectedSet.TryOpenTableAsync(table, state).ConfigureAwait(false))
					Log.Error($"Failed to open table \"{table}\", table ID not found.");
			}
		}

		public async Task OpenSetAsync(Guid setId, CancellationToken cancellationToken)
		{
			var data = AppModel.Instance.Data;
			var setMetadata = await data.GetSetMetadataAsync(setId, cancellationToken);
			if (setMetadata != null)
				m_openSets.Add(new SetViewModel(setMetadata.Value));
		}

		public void Dispose()
		{
			foreach (var set in m_openSets)
				set.Dispose();
			m_openSets.Clear();

			DisposableUtility.Dispose(ref m_taskGroup);
		}

		private void OnSelectedSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.HasChanged(nameof(SetViewModel.SelectedTableNode)))
				SelectedTable = m_selectedSet!.SelectedTableNode as TableViewModel;
		}

		private void OnSelectedTableChanged(object? sender, GenericEventArgs<TableViewModel> e)
		{
			if (!IsEditTablePanelVisible)
				e.Value.Roll();
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

		private TaskGroup m_taskGroup;
		private bool m_isSetsPanelVisible;
		private bool m_isEditTablePanelVisible;
		private ObservableCollection<SetViewModel> m_openSets;
		private SetViewModel? m_selectedSet;
		private TaskWatcher? m_loadSelectedSetWork;
		private TableViewModel? m_selectedTable;
	}
}
