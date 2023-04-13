﻿using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Oraculum.Data;
using Oraculum.MainWindow;

namespace Oraculum
{
    public sealed class AppModel : ViewModelBase
	{
		public static AppModel Instance => s_appModel.Value;

		private AppModel()
		{
			LogManager.Initialize(new DebugLogDestination());

			m_currentTheme = new Uri(@"/Themes/Default/Default.xaml", UriKind.Relative);
			Data = new DataManager();
		}

		public MainWindowViewModel? MainWindow { get; set; }

		public DataManager Data { get; }

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

		public async Task StartupAsync()
		{
			m_taskGroup = new TaskGroup();

			await Data.InitializeAsync(CancellationToken.None);

			MainWindow = new MainWindowViewModel();
			await MainWindow.OpenSetAsync(new Guid("599d53df-5076-4f1e-af03-0abe36991eba"), CancellationToken.None);
			await MainWindow.OpenSetAsync(new Guid("04e1a881-9650-4cbb-8781-9f0b31391f83"), CancellationToken.None);
		}

		public async Task ShutdownAsync()
		{
			DisposableUtility.Dispose(ref m_taskGroup);
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

		private Uri m_currentTheme;
		private TaskGroup m_taskGroup;
	}
}
