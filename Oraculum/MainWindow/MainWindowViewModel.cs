using System;
using System.Collections.ObjectModel;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Oraculum.Data;
using Oraculum.SetsView;
using Oraculum.SetView;

namespace Oraculum.MainWindow
{
	public sealed class MainWindowViewModel : ViewModelBase
	{
		public MainWindowViewModel()
		{
			AllSets = new SetsViewModel();
			m_openSets = new ObservableCollection<SetViewModel>();
			OpenSets = new ReadOnlyObservableCollection<SetViewModel>(m_openSets);

			RandomValue = 100;

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

		public SetsViewModel AllSets { get; }

		public ReadOnlyObservableCollection<SetViewModel> OpenSets { get; }

		public SetViewModel? SelectedSet
		{
			get => VerifyAccess(m_selectedSet);
			set
			{
				if (SetPropertyField(value, ref m_selectedSet))
				{
					if (m_selectedSet is not null)
					{
						m_loadSelectedSetWork?.Cancel();
						m_loadSelectedSetWork = TaskWatcher.Create(m_selectedSet.LoadTablesIfNeededAsync, AppModel.Instance.TaskGroup);
					}
				}
			}
		}

		public bool ShouldAnimateRandomValue
		{
			get => VerifyAccess(m_shouldAnimateRandomValue);
			private set => SetPropertyField(value, ref m_shouldAnimateRandomValue);
		}

		public int RandomValue
		{
			get => VerifyAccess(m_randomValue);
			private set => SetPropertyField(value, ref m_randomValue);
		}

		public void SetRandomValue()
		{
			ShouldAnimateRandomValue = true;
			RandomValue = AppModel.Instance.Random.NextRoll(1, 100);
			ShouldAnimateRandomValue = false;
		}

		public void SetLightMode() =>
			AppModel.Instance.CurrentTheme = new Uri(@"/Themes/Default/Default.xaml", UriKind.Relative);

		public void SetDarkMode() =>
			AppModel.Instance.CurrentTheme = new Uri(@"/Themes/Dark/Dark.xaml", UriKind.Relative);

		public void ToggleIsSetsPanelVisible() =>
			IsSetsPanelVisible = !IsSetsPanelVisible;

		public void OnRandomValueDisplayed()
		{
			Log.Info($"Finished rolling, got a {RandomValue}");
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

		private bool m_isSetsPanelVisible;
		private ObservableCollection<SetViewModel> m_openSets;
		private int m_randomValue;
		private bool m_shouldAnimateRandomValue;
		private SetViewModel? m_selectedSet;
		private TaskWatcher m_loadSelectedSetWork;
	}
}
