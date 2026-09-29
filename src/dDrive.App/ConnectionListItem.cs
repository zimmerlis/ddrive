using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using dDrive.Core.Connections;

namespace dDrive.App;

public sealed class ConnectionListItem(ConnectionProfile profile, string language = "English") : INotifyPropertyChanged
{
    private string _status = LocalizationService.TranslateText("Getrennt", language);

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

    public void UpdateProfile(ConnectionProfile profile)
    {
        Profile = profile;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Protocol));
        OnPropertyChanged(nameof(Host));
        OnPropertyChanged(nameof(RemotePath));
        OnPropertyChanged(nameof(DriveLetter));
    }

    public void SetStatus(string status, bool isMounted)
    {
        Status = status;
        IsMounted = isMounted;
        OnPropertyChanged(nameof(IsMounted));
        OnPropertyChanged(nameof(StatusBrush));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}