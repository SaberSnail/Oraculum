using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
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
			m_openSets.Add(new SetViewModel(new SetMetadata
			{
				Id = new Guid("599d53df-5076-4f1e-af03-0abe36991eba"),
				Author = "SaberSnail",
				Version = 1,
				Created = DateTime.Now,
				Modified = DateTime.Now,
				Groups = new[] { "RPG", "Ironsworn" },
				Title = "Ironsworn",
			}));
			m_openSets.Add(new SetViewModel(new SetMetadata
			{
				Id = new Guid("04e1a881-9650-4cbb-8781-9f0b31391f83"),
				Author = "SaberSnail",
				Version = 1,
				Created = DateTime.Now,
				Modified = DateTime.Now,
				Groups = new[] { "RPG", "Ironsworn" },
				Title = "Ironsworn (custom)",
			}));
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
			set => SetPropertyField(value, ref m_selectedTable);
		}

		public void SetLightMode() =>
			AppModel.Instance.CurrentTheme = new Uri(@"/Themes/Default/Default.xaml", UriKind.Relative);

		public void SetDarkMode() =>
			AppModel.Instance.CurrentTheme = new Uri(@"/Themes/Dark/Dark.xaml", UriKind.Relative);

		public void ToggleIsSetsPanelVisible() =>
			IsSetsPanelVisible = !IsSetsPanelVisible;

		public void ToggleTableView() =>
			IsEditTablePanelVisible = !IsEditTablePanelVisible;

		private void OnSelectedSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.HasChanged(nameof(SetViewModel.SelectedTable)))
				SelectedTable = m_selectedSet!.SelectedTable;
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
