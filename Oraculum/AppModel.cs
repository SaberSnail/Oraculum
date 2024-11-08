using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.Data;
using Oraculum.MainWindow;
using Oraculum.ViewModels;

namespace Oraculum
{
	public sealed class AppModel : ViewModelBase
	{
		public static AppModel Instance => s_appModel.Value;

		private AppModel()
		{
			m_taskGroup = new TaskGroup();
			m_rollLog = null!;

			LogManager.Initialize(new DebugLogDestination());

			m_currentTheme = new Uri(@"/Themes/Default/Default.xaml", UriKind.Relative);
			Data = new DataManager();
			Settings = new SettingsManager();
		}

		public MainWindowViewModel? MainWindow
		{
			get => VerifyAccess(m_mainWindow);
			set => SetPropertyField(value, ref m_mainWindow);
		}

		public RollLogViewModel RollLog
		{
			get => VerifyAccess(m_rollLog);
			set => SetPropertyField(value, ref m_rollLog);
		}

		public DataManager Data { get; }

		public SettingsManager Settings { get; }

		public TaskGroup TaskGroup => m_taskGroup;

		public Uri CurrentTheme
		{
			get
			{
				VerifyAccess();
				return m_currentTheme;
			}
			set
			{
				if (SetPropertyField(value, ref m_currentTheme))
					Log.Info($"Changing theme to \"{m_currentTheme.OriginalString}\"");
			}
		}

		public async Task StartupAsync(TaskStateController state)
		{
			await state.ToThreadPool();

			await Data.InitializeAsync(state).ConfigureAwait(false);
			await Settings.InitializeAsync(state.CancellationToken).ConfigureAwait(false);

			await state.ToSyncContext();

			m_mainWindow = new MainWindowViewModel();
			m_rollLog = new RollLogViewModel();

			var sets = await Data.GetAllSetMetadataAsync(state.CancellationToken).ConfigureAwait(false);
			foreach (var set in sets)
				await m_mainWindow.OpenSetAsync(set.Id, state.CancellationToken).ConfigureAwait(false);

			/*
			await m_mainWindow.OpenSetAsync(new Guid("599d53df-5076-4f1e-af03-0abe36991eba"), state.CancellationToken).ConfigureAwait(false);
			await m_mainWindow.OpenSetAsync(new Guid("04e1a881-9650-4cbb-8781-9f0b31391f83"), state.CancellationToken).ConfigureAwait(false);
			*/
		}

		public async Task ShutdownAsync()
		{
			DisposableUtility.Dispose(ref m_mainWindow);
			DisposableUtility.Dispose(ref m_taskGroup);
			await Data.WaitForWriteAsync().ConfigureAwait(false);
		}

		public void OpenTable(TableReference table)
		{
			if (m_mainWindow is null)
				return;
			TaskWatcher.Execute(controller => m_mainWindow.OpenTableAsync(table, null, controller), TaskGroup);
		}

		public void OpenTableFromRollLog(TableReference table, string text)
		{
			if (m_mainWindow is null)
				return;
			TaskWatcher.Execute(controller => m_mainWindow.OpenTableAsync(table, text, controller), TaskGroup);
		}

		public string GetOrCreateDataFolder()
		{
			var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var company = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
			var product = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()!.Product;
			var path = Path.Combine(appDataPath, company, product);
			Directory.CreateDirectory(path);
			return path;
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(AppModel));
		private static readonly Lazy<AppModel> s_appModel = new(() => new AppModel());

		private MainWindowViewModel? m_mainWindow;
		private RollLogViewModel m_rollLog;
		private Uri m_currentTheme;
		private TaskGroup m_taskGroup;
	}
}
