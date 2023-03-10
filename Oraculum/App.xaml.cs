using GoldenAnvil.Utility.Logging;
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

		protected override void OnStartup(StartupEventArgs e) => OnStartupAsync(e).Wait();

		private async Task OnStartupAsync(StartupEventArgs e)
		{
			var stopwatch = Stopwatch.StartNew();

			base.OnStartup(e);

			FrameworkElement.StyleProperty.OverrideMetadata(typeof(Window), new FrameworkPropertyMetadata
			{
				DefaultValue = FindResource(typeof(Window))
			});

			await AppModel.Instance.StartupAsync();

			new MainWindowView(AppModel.Instance.MainWindow).Show();

			Log.Info($"Finished starting up in {stopwatch.Elapsed}");
		}

		protected override void OnExit(ExitEventArgs e) => OnShutdownAsync(e).Wait();

		private async Task OnShutdownAsync(ExitEventArgs e)
		{
			using var logScope = Log.TimedInfo("Shutting down");

			await AppModel.Instance.ShutdownAsync();

			base.OnExit(e);
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(App));
	}
}
