using System.Windows;
using System.Windows.Controls;
using dDrive.Core.Configuration;
using Wpf.Ui.Controls;

namespace dDrive.App;

public partial class SettingsWindow : FluentWindow
{
    public bool StartWithWindows { get; private set; }
    public bool StartMinimized { get; private set; }
    public bool RcloneEnabled { get; private set; }
    public string SelectedLanguage { get; private set; }

    public SettingsWindow(AppConfiguration configuration)
    {
        InitializeComponent();
        LocalizationService.Apply(this, configuration.Language);
        StartWithWindows = configuration.StartWithWindows;
        StartMinimized = configuration.StartMinimized;
        RcloneEnabled = configuration.RcloneEnabled;
        SelectedLanguage = string.IsNullOrWhiteSpace(configuration.Language) ? "English" : configuration.Language;
        StartWithWindowsCheckBox.IsChecked = StartWithWindows;
        StartMinimizedCheckBox.IsChecked = StartMinimized;
        RcloneEnabledCheckBox.IsChecked = RcloneEnabled;
        LanguageComboBox.SelectedItem = LanguageComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), SelectedLanguage, StringComparison.OrdinalIgnoreCase))
            ?? LanguageComboBox.Items[0];
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        StartMinimized = StartMinimizedCheckBox.IsChecked == true;
        RcloneEnabled = RcloneEnabledCheckBox.IsChecked == true;
        SelectedLanguage = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "English";
        DialogResult = true;
    }
}
