using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using QRCoder;
using Wpf.Ui.Controls;

namespace dDrive.App;

public partial class AboutWindow : FluentWindow
{
    private const string SupportUrl = "https://buymeacoffee.com/oxekklfcg";
    private readonly string _language;

    public bool HasDonated => DonatedCheckBox.IsChecked == true;

    public AboutWindow(bool hasDonated = false, string language = "English")
    {
        InitializeComponent();
        var normalizedLanguage = language is "English" or "German" or "Mandarin-Chinese" or "Hindi" or "Spanish" or "French"
            ? language
            : "English";
        _language = normalizedLanguage;
        Loaded += (_, _) => LocalizationService.Apply(this, _language);
        DonatedCheckBox.IsChecked = hasDonated;
        QrCodeImage.Source = CreateQrCode();
    }

    private void SupportLink_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(SupportUrl);
    }

    private void BuyMeACoffeeLink_Click(object sender, RoutedEventArgs e) => OpenUrl(SupportUrl);

    private void GitHubSupport_Click(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/zimmerlis/ddrive");

    private void GitHubAuthor_Click(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/zimmerlis");

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    private void DonatedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
    }

    private static BitmapImage CreateQrCode()
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(SupportUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(8, drawQuietZones: true);
        using var stream = new MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
