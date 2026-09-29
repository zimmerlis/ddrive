using System.Windows;
using System.Windows.Controls;
using System.Security;
using dDrive.Core.Connections;
using dDrive.Core.Security;
using dDrive.Storage;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace dDrive.App;

public partial class ConnectionEditorWindow : FluentWindow
{
    private readonly ConnectionProfile? _original;
    private readonly ISecretStore _secretStore;
    private bool _isInitializing;

    public ConnectionProfile Connection { get; private set; } = new();
    public bool DeleteRequested { get; private set; }

    public ConnectionEditorWindow(ISecretStore secretStore, ConnectionProfile? connection = null, bool rcloneEnabled = false, string language = "English")
    {
        _isInitializing = true;
        InitializeComponent();
        LocalizationService.Apply(this, language);
        _secretStore = secretStore;
        _original = connection;
        DialogTitle.Text = connection is null ? "Neue Verbindung" : "Verbindung bearbeiten";
        DeleteConnectionButton.Visibility = connection is null ? Visibility.Collapsed : Visibility.Visible;
        DeleteConnectionButton.Visibility = connection is null ? Visibility.Collapsed : Visibility.Visible;
        SftpProtocolItem.Visibility = rcloneEnabled ? Visibility.Visible : Visibility.Collapsed;
        SmbProtocolItem.Visibility = rcloneEnabled ? Visibility.Visible : Visibility.Collapsed;

        var protocol = connection?.Protocol ?? ConnectionProtocol.WebDav;
        ProtocolComboBox.SelectedIndex = protocol switch
        {
            ConnectionProtocol.WebDav => 0,
            ConnectionProtocol.Ftp => 1,
            ConnectionProtocol.Sftp when rcloneEnabled => 2,
            ConnectionProtocol.Smb when rcloneEnabled => 3,
            _ => 0
        };
        NameTextBox.Text = connection?.Name ?? string.Empty;
        HostTextBox.Text = connection?.Host ?? string.Empty;
        PortTextBox.Text = (connection?.Port ?? DefaultPort(protocol)).ToString();
        RemotePathTextBox.Text = connection?.RemotePath ?? "/";
        DriveLetterTextBox.Text = (connection?.DriveLetter ?? 'Z').ToString();
        UsernameTextBox.Text = connection?.Username ?? string.Empty;
        ReadOnlyCheckBox.IsChecked = connection?.ReadOnly ?? false;
        AutoMountCheckBox.IsChecked = connection?.AutoMount ?? false;
        VerifyTlsCheckBox.IsChecked = connection?.VerifyTlsCertificate ?? true;
        UseTlsCheckBox.IsChecked = connection?.UseTls ?? true;
        UpdateSynologyOption(protocol, connection?.Port ?? DefaultPort(protocol));
        _isInitializing = false;
    }

    private void ProtocolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || !IsInitialized)
        {
            return;
        }

        var protocol = GetSelectedProtocol();
        PortTextBox.Text = DefaultPort(protocol).ToString();
        UpdateSynologyOption(protocol, DefaultPort(protocol));
    }

    private void SynologyNasCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || GetSelectedProtocol() != ConnectionProtocol.WebDav)
        {
            return;
        }

        PortTextBox.Text = SynologyNasCheckBox.IsChecked == true
            ? "5006"
            : DefaultPort(ConnectionProtocol.WebDav).ToString();
    }

    private void UpdateSynologyOption(ConnectionProtocol protocol, int port)
    {
        var isWebDav = protocol == ConnectionProtocol.WebDav;
        SynologyNasCheckBox.Visibility = isWebDav ? Visibility.Visible : Visibility.Collapsed;
        SynologyNasCheckBox.IsChecked = isWebDav && port == 5006;
    }

    private void DeleteConnection_Click(object sender, RoutedEventArgs e)
    {
        if (_original is null)
        {
            return;
        }

        if (MessageBox.Show(
                this,
                $"Möchtest du die Verbindung '{_original.Name}' wirklich löschen?",
                "Verbindung löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteRequested = true;
        DialogResult = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        var host = HostTextBox.Text.Trim();
        var remotePath = RemotePathTextBox.Text.Trim();
        var driveText = DriveLetterTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(host))
        {
            ShowValidationError("Anzeigename und Host müssen ausgefüllt sein.");
            return;
        }

        if (!int.TryParse(PortTextBox.Text, out var port) || port is < 1 or > 65535)
        {
            ShowValidationError("Der Port muss eine Zahl zwischen 1 und 65535 sein.");
            return;
        }

        if (driveText.Length != 1 || !char.IsAsciiLetter(driveText[0]))
        {
            ShowValidationError("Der Laufwerksbuchstabe muss ein einzelner Buchstabe sein.");
            return;
        }

        var connectionId = _original?.Id ?? Guid.NewGuid();
        var protocol = GetSelectedProtocol();
        Connection = new ConnectionProfile
        {
            Id = connectionId,
            Name = name,
            Protocol = protocol,
            Host = host,
            Port = port,
            RemotePath = string.IsNullOrEmpty(remotePath) ? "/" : remotePath,
            DriveLetter = char.ToUpperInvariant(driveText[0]),
            Username = UsernameTextBox.Text.Trim(),
            WinCredManagerEntry = $"dDrive:{name}",
            ReadOnly = ReadOnlyCheckBox.IsChecked == true,
            AutoMount = AutoMountCheckBox.IsChecked == true,
            UseTls = UseTlsCheckBox.IsChecked == true,
            VerifyTlsCertificate = VerifyTlsCheckBox.IsChecked == true,
        };

        try
        {
            using var secret = PasswordBox.SecurePassword;
            var previousEntry = _original?.WinCredManagerEntry;
            if (secret.Length > 0)
            {
                _secretStore.Save(Connection.WinCredManagerEntry, secret);
                if (!string.IsNullOrWhiteSpace(previousEntry) && !string.Equals(previousEntry, Connection.WinCredManagerEntry, StringComparison.Ordinal))
                {
                    _secretStore.Delete(previousEntry);
                }
            }
            else if (RemovePasswordCheckBox.IsChecked == true)
            {
                _secretStore.Delete(Connection.WinCredManagerEntry);
                if (!string.IsNullOrWhiteSpace(previousEntry) && !string.Equals(previousEntry, Connection.WinCredManagerEntry, StringComparison.Ordinal))
                {
                    _secretStore.Delete(previousEntry);
                }
            }
            else if (!string.IsNullOrWhiteSpace(previousEntry) && !string.Equals(previousEntry, Connection.WinCredManagerEntry, StringComparison.Ordinal))
            {
                using var previousSecret = _secretStore.Read(previousEntry);
                if (previousSecret is not null && previousSecret.Length > 0)
                {
                    _secretStore.Save(Connection.WinCredManagerEntry, previousSecret);
                    _secretStore.Delete(previousEntry);
                }
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"Das Passwort konnte nicht sicher gespeichert werden.\n\n{exception.Message}",
                "Credential-Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        var host = HostTextBox.Text.Trim();
        var username = UsernameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
        {
            ShowValidationError("Anzeigename, Host und Benutzername müssen für den Verbindungstest ausgefüllt sein.");
            return;
        }

        if (!int.TryParse(PortTextBox.Text, out var port) || port is < 1 or > 65535)
        {
            ShowValidationError("Der Port muss eine Zahl zwischen 1 und 65535 sein.");
            return;
        }

        var connectionId = _original?.Id ?? Guid.NewGuid();
        var credentialKey = _original?.WinCredManagerEntry ?? $"dDrive:{name}";
        SecureString? secret = null;
        try
        {
            secret = PasswordBox.SecurePassword;
            if (secret.Length == 0)
            {
                secret.Dispose();
                secret = _secretStore.Read(credentialKey);
            }

            if (secret is null || secret.Length == 0)
            {
                ShowValidationError("Gib ein Passwort ein oder speichere die Verbindung zuerst mit einem Passwort.");
                return;
            }

            var protocol = GetSelectedProtocol();
            if (protocol is ConnectionProtocol.Sftp or ConnectionProtocol.Smb)
            {
                MessageBox.Show(this, "SFTP und SMB werden über Rclone gemountet. Speichere die Verbindung und verwende anschließend den Rclone-Modus.", "Rclone erforderlich", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var connection = new ConnectionProfile
            {
                Id = connectionId,
                Name = name,
                Protocol = protocol,
                Host = host,
                Port = port,
                RemotePath = string.IsNullOrWhiteSpace(RemotePathTextBox.Text) ? "/" : RemotePathTextBox.Text.Trim(),
                Username = username,
                WinCredManagerEntry = credentialKey,
                UseTls = UseTlsCheckBox.IsChecked == true,
                VerifyTlsCertificate = VerifyTlsCheckBox.IsChecked == true,
            };

            IsEnabled = false;
            try
            {
                await using var backend = StorageBackendFactory.Create(protocol);
                await backend.ConnectAsync(connection, secret);
                MessageBox.Show(this, $"Die {protocol}-Verbindung wurde erfolgreich hergestellt.", "Verbindung erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                IsEnabled = true;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"Die Verbindung konnte nicht hergestellt werden.\n\n{exception.Message}",
                "Verbindung fehlgeschlagen",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            secret?.Dispose();
        }
    }

    private ConnectionProtocol GetSelectedProtocol()
    {
        var selected = (ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        return selected switch
        {
            "WebDAV" => ConnectionProtocol.WebDav,
            "FTP" => ConnectionProtocol.Ftp,
            "SFTP" => ConnectionProtocol.Sftp,
            "SMB" => ConnectionProtocol.Smb,
            _ => ConnectionProtocol.WebDav
        };
    }

    private void ShowValidationError(string message)
    {
        MessageBox.Show(this, message, "Ungültige Verbindung", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static int DefaultPort(ConnectionProtocol protocol) => protocol switch
    {
        ConnectionProtocol.WebDav => 443,
        ConnectionProtocol.Ftp => 21,
        ConnectionProtocol.Sftp => 22,
        ConnectionProtocol.Smb => 445,
        _ => 443
    };
}