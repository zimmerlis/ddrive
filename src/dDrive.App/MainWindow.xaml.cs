using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using dDrive.Core.Configuration;
using dDrive.Core.Connections;
using dDrive.Core.Diagnostics;
using dDrive.Mounting;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace dDrive.App;

public partial class MainWindow : FluentWindow
{
    private readonly ConfigurationStore _configurationStore = new();
    private readonly FileAppLogger _logger = new();
    private readonly WindowsCredentialStore _credentialStore = new();
    private readonly MountService _mountService = new();
    private readonly ObservableCollection<ConnectionListItem> _connections = [];
    private ICollectionView _connectionsView = null!;
    private readonly System.Drawing.Icon _trayApplicationIcon;
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.ContextMenuStrip _trayMenu = new();
    private AppConfiguration _configuration = new();
    private bool _allowClose;
    private bool _isLoadingConfiguration;
    private readonly bool _startHidden;
    private string _searchText = string.Empty;

    public MainWindow(bool startHidden = false)
    {
        _startHidden = startHidden;
        InitializeComponent();
        _connectionsView = CollectionViewSource.GetDefaultView(_connections);
        _connectionsView.Filter = FilterConnection;
        _connections.CollectionChanged += (_, _) => UpdateConnectionCount();
        ConnectionsGrid.ItemsSource = _connectionsView;
        var openMenuItem = _trayMenu.Items.Add("dDrive öffnen", null, (_, _) => ShowWindow());
        openMenuItem.Tag = "open";
        _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        var exitMenuItem = _trayMenu.Items.Add("Beenden", null, async (_, _) => await ShutdownAsync());
        exitMenuItem.Tag = "exit";
        _trayApplicationIcon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "dDrive.ico"));
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _trayApplicationIcon,
            Text = "dDrive",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _trayIcon.MouseDoubleClick += (_, _) => ShowWindow();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isLoadingConfiguration = true;
            _configuration = await _configurationStore.LoadAsync();
            var language = _configuration.Language is "English" or "German" or "Mandarin-Chinese" or "Hindi" or "Spanish" or "French"
                ? _configuration.Language
                : "English";
            if (!string.Equals(language, _configuration.Language, StringComparison.Ordinal))
            {
                _configuration = new AppConfiguration
                {
                    SchemaVersion = _configuration.SchemaVersion,
                    Connections = _configuration.Connections,
                    StartWithWindows = _configuration.StartWithWindows,
                    StartMinimized = _configuration.StartMinimized,
                    RcloneEnabled = _configuration.RcloneEnabled,
                    HasDonated = _configuration.HasDonated,
                    Language = language,
                    UseDarkMode = _configuration.UseDarkMode
                };
            }
            LocalizationService.Apply(this, language);
            ApplyTheme(_configuration.UseDarkMode);
            _connections.Clear();
            foreach (var connection in _configuration.Connections)
            {
                _connections.Add(new ConnectionListItem(connection, _configuration.Language));
            }

            _connectionsView.Refresh();
            ConnectionsGrid.Items.Refresh();
            RefreshTrayMenu();
            ApplyTrayLanguage(_configuration.Language);
            StatusText.Text = $"{_connections.Count} Verbindung(en) geladen";
            _isLoadingConfiguration = false;

            var restoredRcloneMounts = await _mountService.RestoreRcloneMountsAsync(_configuration.Connections);
            foreach (var item in _connections.Where(item => restoredRcloneMounts.Contains(item.Profile.Id)))
            {
                item.SetStatus(LocalizationService.TranslateText("Verbunden", _configuration.Language), true);
            }

            var startMinimized = _startHidden || _configuration.StartMinimized;
            if (!startMinimized && _configuration.RcloneEnabled && !new RcloneRuntimeInstaller().IsReady())
            {
                await OfferRcloneRuntimeAsync();
            }

            if (!startMinimized)
            {
                await ShowAboutAsync(force: false);
            }

            foreach (var item in _connections.Where(item => item.Profile.AutoMount && !restoredRcloneMounts.Contains(item.Profile.Id)))
            {
                await MountAsync(item);
            }

            if (startMinimized)
            {
                Hide();
            }
        }
        catch (Exception exception)
        {
            _isLoadingConfiguration = false;
            _logger.LogError("Configuration.Load", "Die Verbindungskonfiguration konnte nicht geladen werden.", exception);
            StatusText.Text = "Konfiguration konnte nicht geladen werden";
            if (!_startHidden)
            {
                MessageBox.Show(
                    this,
                    $"Die Verbindungskonfiguration konnte nicht geladen werden.\n\n{exception.Message}",
                    "Ladefehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private async Task OfferRcloneRuntimeAsync()
    {
        var installer = new RcloneRuntimeInstaller();
        if (installer.IsReady())
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "Möchtest du den optionalen Rclone-Modus aktivieren? Dafür werden Rclone und WinFsp über WinGet installiert. Native WebDAV-Mounts funktionieren weiterhin ohne diese Komponenten.",
            "Optionalen Rclone-Modus einrichten",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            StatusText.Text = "Rclone und WinFsp werden installiert ...";
            await installer.InstallAsync();
            StatusText.Text = "Rclone-Modus ist verfügbar";
            MessageBox.Show(
                this,
                "Rclone und WinFsp wurden installiert. Bitte starte dDrive neu, damit die neue Umgebung vollständig erkannt wird.",
                "Neustart erforderlich",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            _logger.LogError("Rclone.Install", "Rclone und WinFsp konnten nicht installiert werden.", exception);
            StatusText.Text = "Rclone-Installation fehlgeschlagen";
            MessageBox.Show(
                this,
                $"Rclone und WinFsp konnten nicht installiert werden. Native WebDAV-Mounts bleiben verfügbar.\n\n{exception.Message}",
                "Rclone-Installation fehlgeschlagen",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void AddConnection_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ConnectionEditorWindow(_credentialStore, rcloneEnabled: _configuration.RcloneEnabled, language: _configuration.Language) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            return;
        }

        if (HasDriveLetterConflict(editor.Connection))
        {
            MessageBox.Show(this, $"Das Laufwerk {editor.Connection.DriveLetter}: ist bereits einer anderen Verbindung zugewiesen.", "Laufwerkskonflikt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _connections.Add(new ConnectionListItem(editor.Connection, _configuration.Language));
        RefreshTrayMenu();
        await SaveConfigurationAsync();
    }

    private async void EditConnection_Click(object sender, RoutedEventArgs e)
    {
        if (ConnectionsGrid.SelectedItem is not ConnectionListItem selected)
        {
            return;
        }

        if (selected.IsMounted)
        {
            MessageBox.Show(this, "Trenne die Verbindung zuerst, bevor du ihre Einstellungen änderst.", "Verbindung aktiv", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new ConnectionEditorWindow(_credentialStore, selected.Profile, _configuration.RcloneEnabled, _configuration.Language) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            return;
        }

        if (editor.DeleteRequested)
        {
            await DeleteConnectionAsync(selected);
            return;
        }

        if (HasDriveLetterConflict(editor.Connection, selected.Profile.Id))
        {
            MessageBox.Show(this, $"Das Laufwerk {editor.Connection.DriveLetter}: ist bereits einer anderen Verbindung zugewiesen.", "Laufwerkskonflikt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        selected.UpdateProfile(editor.Connection);
        RefreshTrayMenu();
        await SaveConfigurationAsync();
    }

    private async Task DeleteConnectionAsync(ConnectionListItem selected)
    {
        if (selected.IsMounted)
        {
            MessageBox.Show(this, "Trenne die Verbindung zuerst, bevor du sie löschst.", "Verbindung aktiv", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _credentialStore.Delete(selected.Profile.WinCredManagerEntry);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Das gespeicherte Passwort konnte nicht entfernt werden.\n\n{exception.Message}", "Credential-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _connections.Remove(selected);
        RefreshTrayMenu();
        await SaveConfigurationAsync();
    }

    private async void ExportConfiguration_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Verbindungen exportieren",
            FileName = "dDrive-connections.json",
            DefaultExt = ".json",
            Filter = "JSON-Konfiguration (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await ConfigurationStore.ExportToFileAsync(dialog.FileName, _configuration);
            StatusText.Text = "Konfiguration exportiert (ohne Zugangsdaten)";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Die Konfiguration konnte nicht exportiert werden.\n\n{exception.Message}", "Export fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var lines = File.Exists(_logger.LogFilePath) ? await File.ReadAllLinesAsync(_logger.LogFilePath) : [];
            var text = FormatLogLines(lines.TakeLast(500));
            var logContent = new Grid();
            logContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            logContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var logTextBox = new System.Windows.Controls.TextBox
            {
                Text = string.IsNullOrEmpty(text) ? "Noch keine Protokolleinträge vorhanden." : text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 13,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(22, 14, 22, 18)
            };
            Grid.SetRow(logTextBox, 1);
            logContent.Children.Add(logTextBox);

            var titleBar = new TitleBar
            {
                Title = "dDrive-Protokoll",
                ShowMinimize = true,
                ShowMaximize = true,
                ShowClose = true
            };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(titleBar, 0);
            header.Children.Add(titleBar);

            var clearButton = new System.Windows.Controls.Button
            {
                Content = "Leeren",
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 8, 12, 8),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Alle Diagnoseprotokolle löschen"
            };
            clearButton.Click += (_, _) =>
            {
                if (MessageBox.Show(
                        this,
                        "Alle Diagnoseprotokolle löschen?",
                        "Protokoll leeren",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }

                _logger.Clear();
                logTextBox.Text = "Noch keine Protokolleinträge vorhanden.";
            };
            Grid.SetColumn(clearButton, 1);
            header.Children.Add(clearButton);
            Grid.SetRow(header, 0);
            logContent.Children.Add(header);

            var viewer = new FluentWindow
            {
                Owner = this,
                Title = "dDrive-Protokoll",
                Width = 900,
                Height = 520,
                MinWidth = 600,
                MinHeight = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ExtendsContentIntoTitleBar = true,
                Content = logContent
            };
            viewer.ShowDialog();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Das Protokoll konnte nicht geladen werden.\n\n{exception.Message}", "Protokoll", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FormatLogLines(IEnumerable<string> lines)
    {
        var formatted = new StringBuilder();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var entry = document.RootElement;
                var timestampText = ReadString(entry, "timestampUtc");
                var timestamp = DateTimeOffset.TryParse(timestampText, out var parsedTimestamp)
                    ? parsedTimestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : "Zeit unbekannt";
                var level = ReadString(entry, "level")?.ToUpperInvariant() ?? "INFO";
                var eventName = ReadString(entry, "eventName") ?? "Ereignis";
                var message = ReadString(entry, "message");

                formatted.Append(timestamp)
                    .Append("  ")
                    .Append(level.PadRight(7))
                    .Append("  ")
                    .AppendLine(eventName);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    formatted.AppendLine(message);
                }

                var exceptionType = ReadString(entry, "exceptionType");
                var exceptionMessage = ReadString(entry, "exceptionMessage");
                if (!string.IsNullOrWhiteSpace(exceptionType) || !string.IsNullOrWhiteSpace(exceptionMessage))
                {
                    formatted.Append("  ")
                        .Append(exceptionType ?? "Fehler")
                        .Append(": ")
                        .AppendLine(exceptionMessage);
                }

                formatted.AppendLine();
            }
            catch (JsonException)
            {
                formatted.AppendLine(line);
                formatted.AppendLine();
            }
        }

        return formatted.ToString().TrimEnd();
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private async void ImportConfiguration_Click(object sender, RoutedEventArgs e)
    {
        if (_connections.Any(item => item.IsMounted))
        {
            MessageBox.Show(this, "Trenne alle Laufwerke, bevor du eine Konfiguration importierst.", "Verbindung aktiv", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Verbindungen importieren",
            DefaultExt = ".json",
            Filter = "JSON-Konfiguration (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var imported = await ConfigurationStore.LoadFromFileAsync(dialog.FileName);
            ValidateImportedConfiguration(imported);
            if (MessageBox.Show(
                    this,
                    $"Die aktuelle Liste wird durch {imported.Connections.Count} importierte Verbindung(en) ersetzt. Passwörter werden nicht importiert und müssen bei Bedarf neu gespeichert werden. Fortfahren?",
                    "Konfiguration importieren",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            var existingKeys = _connections.ToDictionary(item => item.Profile.Id, item => item.Profile.WinCredManagerEntry);
            var connections = imported.Connections
                .Select(connection => connection with
                {
                    WinCredManagerEntry = existingKeys.TryGetValue(connection.Id, out var key) ? key : $"dDrive:{connection.Name}"
                })
                .ToList();
            var newConfiguration = new AppConfiguration
            {
                SchemaVersion = 1,
                StartWithWindows = _configuration.StartWithWindows,
                StartMinimized = _configuration.StartMinimized,
                RcloneEnabled = _configuration.RcloneEnabled,
                HasDonated = _configuration.HasDonated,
                Language = _configuration.Language,
                UseDarkMode = _configuration.UseDarkMode,
                Connections = connections
            };

            await _configurationStore.SaveAsync(newConfiguration);
            _configuration = newConfiguration;
            _connections.Clear();
            foreach (var connection in connections)
            {
                _connections.Add(new ConnectionListItem(connection, _configuration.Language));
            }

            RefreshTrayMenu();
            StatusText.Text = "Konfiguration importiert";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Die Konfiguration konnte nicht importiert werden.\n\n{exception.Message}", "Import fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void ValidateImportedConfiguration(AppConfiguration configuration)
    {
        if (configuration.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Die Konfigurationsversion {configuration.SchemaVersion} wird nicht unterstützt.");
        }

        if (configuration.Connections.Any(connection =>
                connection is null ||
                connection.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(connection.Name) ||
                string.IsNullOrWhiteSpace(connection.Host) ||
                !char.IsAsciiLetter(connection.DriveLetter)))
        {
            throw new InvalidDataException("Die Konfiguration enthält ungültige Verbindungsdaten.");
        }

        if (configuration.Connections.Select(connection => char.ToUpperInvariant(connection.DriveLetter)).Distinct().Count() != configuration.Connections.Count)
        {
            throw new InvalidDataException("Jeder Verbindung muss ein eindeutiger Laufwerksbuchstabe zugewiesen sein.");
        }

        if (configuration.Connections.Select(connection => connection.Id).Distinct().Count() != configuration.Connections.Count)
        {
            throw new InvalidDataException("Die Konfiguration enthält doppelte Verbindungs-IDs.");
        }
    }

    private bool HasDriveLetterConflict(ConnectionProfile candidate, Guid? excludedConnectionId = null) =>
        _connections.Any(item =>
            item.Profile.Id != excludedConnectionId &&
            char.ToUpperInvariant(item.DriveLetter) == char.ToUpperInvariant(candidate.DriveLetter));

    private async void MountConnection_Click(object sender, RoutedEventArgs e)
    {
        if (ConnectionsGrid.SelectedItem is ConnectionListItem selected)
        {
            await MountAsync(selected);
        }
    }

    private async Task MountAsync(ConnectionListItem item)
    {
        try
        {
            using var secret = _credentialStore.Read(item.Profile.WinCredManagerEntry);
            if (secret is null || secret.Length == 0)
            {
                item.SetStatus(LocalizationService.TranslateText("Passwort fehlt", _configuration.Language), false);
                StatusText.Text = $"{item.Name}: Passwort fehlt";
                if (IsVisible && !_startHidden)
                {
                    MessageBox.Show(this, $"Für '{item.Name}' ist kein Passwort im Windows Credential Manager gespeichert.", "Passwort fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return;
            }

            item.SetStatus(LocalizationService.TranslateText("Verbinde ...", _configuration.Language), false);
            StatusText.Text = $"Verbinde {item.Name} ...";
            RefreshTrayMenu();
            await _mountService.MountAsync(item.Profile, secret, _configuration.RcloneEnabled);
            item.SetStatus(LocalizationService.TranslateText("Verbunden", _configuration.Language), true);
            StatusText.Text = $"{item.Name} ist als {char.ToUpperInvariant(item.DriveLetter)}: eingebunden";
            if (!_configuration.HasDonated)
            {
                await ShowAboutAsync(force: true);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("Connection.Mount", $"Die Verbindung '{item.Name}' konnte nicht eingebunden werden.", exception);
            item.SetStatus(LocalizationService.TranslateText("Fehler", _configuration.Language), false);
            StatusText.Text = "Verbindung fehlgeschlagen";
            if (IsVisible && !_startHidden)
            {
                MessageBox.Show(this, $"Das Laufwerk konnte nicht eingebunden werden.\n\n{exception.Message}", "Mount fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            UpdateSelectionActions();
            RefreshTrayMenu();
        }
    }

    private async void UnmountConnection_Click(object sender, RoutedEventArgs e)
    {
        if (ConnectionsGrid.SelectedItem is not ConnectionListItem selected)
        {
            return;
        }

        try
        {
            var unmounted = await _mountService.UnmountAsync(selected.Profile.Id);
            if (unmounted)
            {
                selected.SetStatus(LocalizationService.TranslateText("Getrennt", _configuration.Language), false);
                StatusText.Text = $"{selected.Name} wurde getrennt";
            }
            else
            {
                StatusText.Text = "Diese Verbindung ist nicht eingebunden";
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("Connection.Unmount", $"Die Verbindung '{selected.Name}' konnte nicht getrennt werden.", exception);
            MessageBox.Show(this, $"Das Laufwerk konnte nicht getrennt werden.\n\n{exception.Message}", "Unmount fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            UpdateSelectionActions();
            RefreshTrayMenu();
        }
    }

    private async Task SaveConfigurationAsync()
    {
        var updatedConfiguration = new AppConfiguration
        {
            SchemaVersion = _configuration.SchemaVersion,
            StartWithWindows = _configuration.StartWithWindows,
            StartMinimized = _configuration.StartMinimized,
            RcloneEnabled = _configuration.RcloneEnabled,
            HasDonated = _configuration.HasDonated,
            Language = _configuration.Language,
            UseDarkMode = _configuration.UseDarkMode,
            Connections = _connections.Select(item => item.Profile).ToList()
        };

        try
        {
            await _configurationStore.SaveAsync(updatedConfiguration);
            _configuration = updatedConfiguration;
            StatusText.Text = "Änderungen gespeichert";
        }
        catch (Exception exception)
        {
            StatusText.Text = "Speichern fehlgeschlagen";
            MessageBox.Show(this, $"Die Änderungen konnten nicht gespeichert werden.\n\n{exception.Message}", "Speicherfehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshTrayMenu()
    {
        while (_trayMenu.Items.Count > 2)
        {
            _trayMenu.Items.RemoveAt(2);
        }

        foreach (var item in _connections)
        {
            var menuItem = new System.Windows.Forms.ToolStripMenuItem($"{item.Name}  {char.ToUpperInvariant(item.DriveLetter)}:");
            menuItem.Click += (_, _) =>
            {
                if (item.IsMounted)
                {
                    OpenDriveInExplorer(item.DriveLetter);
                }
                else
                {
                    ShowWindow();
                }
            };

            _trayMenu.Items.Add(menuItem);
        }

        var exitMenuItem = new System.Windows.Forms.ToolStripMenuItem("Beenden", null, async (_, _) => await ShutdownAsync())
        {
            Tag = "exit"
        };
        _trayMenu.Items.Add(exitMenuItem);
    }

    private void ShowWindow()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private static void OpenDriveInExplorer(char driveLetter)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"{char.ToUpperInvariant(driveLetter)}:\\",
            UseShellExecute = true
        });
    }

    private void ApplyTrayLanguage(string language)
    {
        var openItem = _trayMenu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().FirstOrDefault(item => Equals(item.Tag, "open"));
        if (openItem is not null)
        {
            openItem.Text = LocalizationService.TranslateText("dDrive öffnen", language);
        }

        var exitItem = _trayMenu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().FirstOrDefault(item => Equals(item.Tag, "exit"));
        if (exitItem is not null)
        {
            exitItem.Text = LocalizationService.TranslateText("Beenden", language);
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private async Task ShutdownAsync()
    {
        try
        {
            await _mountService.DisposeAsync(keepRcloneMounts: true);
        }
        finally
        {
            _allowClose = true;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayApplicationIcon.Dispose();
            _trayMenu.Dispose();
            Close();
            System.Windows.Application.Current.Shutdown();
        }
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_configuration) { Owner = this };
        if (settingsWindow.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (settingsWindow.RcloneEnabled && !new RcloneRuntimeInstaller().IsReady())
            {
                await OfferRcloneRuntimeAsync();
                if (!new RcloneRuntimeInstaller().IsReady())
                {
                    throw new InvalidOperationException("Rclone und WinFsp sind nicht verfügbar.");
                }
            }

            SetWindowsStartup(settingsWindow.StartWithWindows);
            var updatedConfiguration = new AppConfiguration
            {
                SchemaVersion = _configuration.SchemaVersion,
                StartWithWindows = settingsWindow.StartWithWindows,
                StartMinimized = settingsWindow.StartMinimized,
                RcloneEnabled = settingsWindow.RcloneEnabled,
                HasDonated = _configuration.HasDonated,
                Language = settingsWindow.SelectedLanguage,
                UseDarkMode = _configuration.UseDarkMode,
                Connections = _configuration.Connections
            };
            await _configurationStore.SaveAsync(updatedConfiguration);
            _configuration = updatedConfiguration;
            LocalizationService.Apply(this, _configuration.Language);
            ApplyTrayLanguage(_configuration.Language);
            StatusText.Text = "Settings saved";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void SetWindowsStartup(bool enabled)
    {
        const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var runKey = Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true)
            ?? throw new InvalidOperationException("Der Windows-Autostartbereich konnte nicht geöffnet werden.");

        if (!enabled)
        {
            runKey.DeleteValue("dDrive", throwOnMissingValue: false);
            return;
        }

        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Der aktuelle Anwendungspfad konnte nicht ermittelt werden.");
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        var commandLine = string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? $"\"{processPath}\" \"{assemblyPath}\" --background"
            : $"\"{processPath}\" --background";
        runKey.SetValue("dDrive", commandLine, RegistryValueKind.String);
    }

    private async void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoadingConfiguration)
        {
            return;
        }

        await SetThemeAsync(!_configuration.UseDarkMode);
    }

    private async void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowAboutAsync(force: true);
    }

    private async Task ShowAboutAsync(bool force)
    {
        if (!force && _configuration.HasDonated)
        {
            return;
        }

        var aboutWindow = new AboutWindow(_configuration.HasDonated, _configuration.Language) { Owner = this };
        aboutWindow.ShowDialog();
        if (aboutWindow.HasDonated == _configuration.HasDonated)
        {
            return;
        }

        var updatedConfiguration = new AppConfiguration
        {
            SchemaVersion = _configuration.SchemaVersion,
            StartWithWindows = _configuration.StartWithWindows,
            StartMinimized = _configuration.StartMinimized,
            RcloneEnabled = _configuration.RcloneEnabled,
            HasDonated = aboutWindow.HasDonated,
            Language = _configuration.Language,
            UseDarkMode = _configuration.UseDarkMode,
            Connections = _configuration.Connections
        };
        await _configurationStore.SaveAsync(updatedConfiguration);
        _configuration = updatedConfiguration;
    }

    private async Task SetThemeAsync(bool useDarkMode)
    {
        var previousConfiguration = _configuration;
        try
        {
            ApplyTheme(useDarkMode);
            var updatedConfiguration = new AppConfiguration
            {
                SchemaVersion = previousConfiguration.SchemaVersion,
                StartWithWindows = previousConfiguration.StartWithWindows,
                StartMinimized = previousConfiguration.StartMinimized,
                RcloneEnabled = previousConfiguration.RcloneEnabled,
                HasDonated = previousConfiguration.HasDonated,
                Language = previousConfiguration.Language,
                UseDarkMode = useDarkMode,
                Connections = previousConfiguration.Connections
            };
            await _configurationStore.SaveAsync(updatedConfiguration);
            _configuration = updatedConfiguration;
            StatusText.Text = useDarkMode ? "Dunkles Design aktiviert" : "Helles Design aktiviert";
        }
        catch (Exception exception)
        {
            _logger.LogError("Theme.Change", "Das Design konnte nicht geändert werden.", exception);
            _isLoadingConfiguration = true;
            ApplyTheme(previousConfiguration.UseDarkMode);
            _isLoadingConfiguration = false;
            MessageBox.Show(this, $"Das Design konnte nicht geändert werden.\n\n{exception.Message}", "Design", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyTheme(bool useDarkMode)
    {
        var theme = useDarkMode ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(theme);
        ApplicationAccentColorManager.Apply(System.Windows.Media.Color.FromRgb(73, 151, 176), theme);
        ThemeButton.Icon = new SymbolIcon(useDarkMode ? SymbolRegular.WeatherSunny20 : SymbolRegular.WeatherMoon20);
        ThemeButton.ToolTip = useDarkMode ? "Zum hellen Design wechseln" : "Zum dunklen Design wechseln";
        System.Windows.Automation.AutomationProperties.SetName(
            ThemeButton,
            useDarkMode ? "Zum hellen Design wechseln" : "Zum dunklen Design wechseln");
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text.Trim();
        _connectionsView.Refresh();
    }

    private bool FilterConnection(object item)
    {
        if (item is not ConnectionListItem connection)
        {
            return false;
        }

        if (string.IsNullOrEmpty(_searchText))
        {
            return true;
        }

        var searchableText = $"{connection.Name} {connection.Protocol} {connection.Host} {connection.RemotePath} {connection.DriveLetter}";
        return searchableText.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateConnectionCount()
    {
        if (ConnectionCountText is not null)
        {
            ConnectionCountText.Text = _connections.Count == 1
                ? "1 Speicherort"
                : $"{_connections.Count} Speicherorte";
        }
    }

    private void ConnectionsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateSelectionActions();
    }

    private void UpdateSelectionActions()
    {
        var hasSelection = ConnectionsGrid.SelectedItem is ConnectionListItem;
        var isMounted = ConnectionsGrid.SelectedItem is ConnectionListItem selected && selected.IsMounted;
        ConnectButton.IsEnabled = hasSelection && !isMounted;
        DisconnectButton.IsEnabled = isMounted;
        EditButton.IsEnabled = hasSelection && !isMounted;
    }
}
