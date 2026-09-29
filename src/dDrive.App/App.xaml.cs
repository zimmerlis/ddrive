namespace dDrive.App;

public partial class App : System.Windows.Application
{
	protected override void OnStartup(System.Windows.StartupEventArgs e)
	{
		base.OnStartup(e);
		MainWindow = new MainWindow(e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase));
		MainWindow.Show();
	}
}