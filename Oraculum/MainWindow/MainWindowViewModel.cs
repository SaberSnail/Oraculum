using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Oraculum.Data;
using Oraculum.SetsView;
using Oraculum.SetView;
using Oraculum.ViewModels;

namespace Oraculum.MainWindow
{
	public sealed class MainWindowViewModel : ViewModelBase
	{
		public MainWindowViewModel()
		{
			AllSets = new SetsViewModel();
			m_openSets = new ObservableCollection<SetViewModel>();
			OpenSets = new ReadOnlyObservableCollection<SetViewModel>(m_openSets);
			m_openSets.Add(new SetViewModel(StaticData.AllSet));
			RollLog = new RollLogViewModel();
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
						oldValue.PropertyChanged -= OnSelectedSetPropertyChanged;

					if (m_selectedSet is not null)
					{
						m_loadSelectedSetWork?.Cancel();
						m_loadSelectedSetWork = TaskWatcher.Create(m_selectedSet.LoadTablesIfNeededAsync, AppModel.Instance.TaskGroup);

						m_selectedSet.PropertyChanged += OnSelectedSetPropertyChanged;
					}
				}
			}
		}

		public TableViewModel? SelectedTable
		{
			get => VerifyAccess(m_selectedTable);
			set
			{
				if (SetPropertyField(value, ref m_selectedTable) && m_selectedTable is not null)
					m_selectedTable.RollLog = RollLog;
			}
		}

		public RollLogViewModel? RollLog { get; }

		public void SetLightMode() =>
			AppModel.Instance.CurrentTheme = new Uri(@"/Themes/Default/Default.xaml", UriKind.Relative);

		public void SetDarkMode() =>
			AppModel.Instance.CurrentTheme = new Uri(@"/Themes/Dark/Dark.xaml", UriKind.Relative);

		public void ToggleIsSetsPanelVisible() =>
			IsSetsPanelVisible = !IsSetsPanelVisible;

		public void ToggleTableView() =>
			IsEditTablePanelVisible = !IsEditTablePanelVisible;

		public async Task OpenSetAsync(Guid setId, CancellationToken cancellationToken)
		{
			var data = AppModel.Instance.Data;
			var setMetadata = await data.GetSetMetadataAsync(setId, cancellationToken);
			if (setMetadata != null)
				m_openSets.Add(new SetViewModel(setMetadata.Value));
		}

		private void OnSelectedSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.HasChanged(nameof(SetViewModel.SelectedTable)))
				SelectedTable = m_selectedSet!.SelectedTable as TableViewModel;
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

		private bool m_isSetsPanelVisible;
		private bool m_isEditTablePanelVisible;
		private ObservableCollection<SetViewModel> m_openSets;
		private SetViewModel? m_selectedSet;
		private TaskWatcher? m_loadSelectedSetWork;
		private TableViewModel? m_selectedTable;
	}
}
