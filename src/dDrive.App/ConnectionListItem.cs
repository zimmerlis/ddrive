using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using dDrive.Core.Connections;

namespace dDrive.App;

public sealed class ConnectionListItem(ConnectionProfile profile, string language = "English") : INotifyPropertyChanged
{
    private string _status = LocalizationService.TranslateText("Getrennt", language);

    private static readonly System.Windows.Media.Brush MountedBadgeBrush = CreateBadgeBrush(34, 139, 34);
    private static readonly System.Windows.Media.Brush IdleBadgeBrush = CreateBadgeBrush(128, 128, 128);

    public event PropertyChangedEventHandler? PropertyChanged;

    public ConnectionProfile Profile { get; private set; } = profile;
    public string Name => Profile.Name;
    public ConnectionProtocol Protocol => Profile.Protocol;
    public string Host => Profile.Host;
    public string RemotePath => Profile.RemotePath;
    public char DriveLetter => Profile.DriveLetter;
    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    public bool IsMounted { get; private set; }
    public System.Windows.Media.Brush StatusBrush => IsMounted ? System.Windows.Media.Brushes.ForestGreen : System.Windows.Media.Brushes.Gray;
    public System.Windows.Media.Brush StatusBackgroundBrush => IsMounted ? MountedBadgeBrush : IdleBadgeBrush;
    public bool IsDefault => Profile.IsDefault;
    public string DefaultMark => IsDefault ? "★" : string.Empty;
    public ImageSource? IconSource { get; private set; }

    public void UpdateProfile(ConnectionProfile profile)
    {
        Profile = profile;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Protocol));
        OnPropertyChanged(nameof(Host));
        OnPropertyChanged(nameof(RemotePath));
        OnPropertyChanged(nameof(DriveLetter));
        OnPropertyChanged(nameof(IsDefault));
        OnPropertyChanged(nameof(DefaultMark));
        RefreshIcon();
    }

    public void RefreshIcon()
    {
        ImageSource? icon = null;
        var path = WebsiteIconService.GetEffectiveIconPath(Profile);
        if (path is not null)
        {
            try
            {
                var frame = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                frame.Freeze();
                icon = frame;
            }
            catch (Exception)
            {
                icon = null;
            }
        }

        IconSource = icon;
        OnPropertyChanged(nameof(IconSource));
    }

    public void SetStatus(string status, bool isMounted)
    {
        Status = status;
        IsMounted = isMounted;
        OnPropertyChanged(nameof(IsMounted));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StatusBackgroundBrush));
    }

    private static System.Windows.Media.Brush CreateBadgeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, r, g, b));
        brush.Freeze();
        return brush;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}