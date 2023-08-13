using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.MainWindow;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace Oraculum
{
	public partial class App : Application
	{
		public App()
		{
		}

		protected override void OnStartup(StartupEventArgs e) => TaskWatcher.Execute(c => OnStartupAsync(e, c), AppModel.Instance.TaskGroup);

		private async Task OnStartupAsync(StartupEventArgs e, TaskStateController state)
		{
			var stopwatch = Stopwatch.StartNew();

			base.OnStartup(e);

			FrameworkElement.StyleProperty.OverrideMetadata(typeof(Window), new FrameworkPropertyMetadata
			{
				DefaultValue = FindResource(typeof(Window))
			});

			await AppModel.Instance.StartupAsync(state).ConfigureAwait(false);

			await state.ToSyncContext();

			new MainWindowView(AppModel.Instance.MainWindow).Show();

			Log.Info($"Finished starting up in {stopwatch.Elapsed}");
		}

		protected override async void OnExit(ExitEventArgs e) => await OnShutdownAsync(e);

		private async Task OnShutdownAsync(ExitEventArgs e)
		{
			using var logScope = Log.TimedInfo("Shutting down");

			await AppModel.Instance.ShutdownAsync();

			base.OnExit(e);
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(App));
	}
}
