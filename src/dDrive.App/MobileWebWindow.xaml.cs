using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace dDrive.App;

public partial class MobileWebWindow : Window
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Mobile Safari/537.36";

    private readonly Uri _startUri;
    private readonly string _language;
    private readonly int _popupWidth;
    private readonly int _popupHeight;
    private readonly string _popupPosition;
    private bool _browserReady;

    public MobileWebWindow(Uri startUri, string language, string? iconPath, int popupWidth, int popupHeight, string popupPosition)
    {
        _startUri = startUri;
        _language = language;
        _popupWidth = popupWidth;
        _popupHeight = popupHeight;
        _popupPosition = popupPosition;
        InitializeComponent();
        LocalizationService.Apply(this, language);
        SetHeaderIcon(iconPath);
        ConfigureSize();
        Loaded += async (_, _) => await InitializeBrowserAsync();
    }

    public void Navigate(Uri uri)
    {
        if (_browserReady)
        {
            Browser.CoreWebView2.Navigate(uri.AbsoluteUri);
        }
    }

    private void SetHeaderIcon(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(iconPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            HeaderIcon.Source = image;
            HeaderIcon.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            HeaderIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void ConfigureSize()
    {
        const double margin = 12;
        var workArea = SystemParameters.WorkArea;
        Width = Math.Clamp(_popupWidth, (int)MinWidth, (int)workArea.Width - (int)margin * 2);
        Height = Math.Min(Math.Max(_popupHeight, (int)MinHeight), workArea.Height - margin * 2);
        if (string.Equals(_popupPosition, "center", StringComparison.OrdinalIgnoreCase))
        {
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
            return;
        }

        // Kleines Flyout wie bei Systemmenüs: unten rechts, direkt über der Taskleiste.
        Left = workArea.Right - Width - margin;
        Top = workArea.Bottom - Height - margin;
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "dDrive",
                "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await Browser.EnsureCoreWebView2Async(environment);

            var core = Browser.CoreWebView2;
            core.Settings.UserAgent = MobileUserAgent;
            core.Settings.IsStatusBarEnabled = false;
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                OpenExternally(args.Uri);
            };
            core.NavigationStarting += (_, args) =>
            {
                if (!args.IsUserInitiated || IsSameSite(args.Uri))
                {
                    return;
                }

                args.Cancel = true;
                OpenExternally(args.Uri);
            };
            core.SourceChanged += (_, _) => UpdateAddress();

            _browserReady = true;
            core.Navigate(_startUri.AbsoluteUri);
        }
        catch (Exception)
        {
            System.Windows.MessageBox.Show(
                this,
                LocalizationService.TranslateText(
                    "Die Webansicht benötigt die Microsoft WebView2-Laufzeit. Die Seite wird stattdessen im Standardbrowser geöffnet.",
                    _language),
                "WebView2",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            OpenExternally(_startUri.AbsoluteUri);
            Close();
        }
    }

    private void UpdateAddress()
    {
        if (_browserReady && Uri.TryCreate(Browser.CoreWebView2.Source, UriKind.Absolute, out var uri))
        {
            AddressText.Text = uri.Host;
        }
    }

    private bool IsSameSite(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               string.Equals(uri.Host, _startUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    private void OpenInBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        var current = _browserReady && !string.IsNullOrEmpty(Browser.CoreWebView2.Source)
            ? Browser.CoreWebView2.Source
            : _startUri.AbsoluteUri;
        OpenExternally(current);
    }

    private static void OpenExternally(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
