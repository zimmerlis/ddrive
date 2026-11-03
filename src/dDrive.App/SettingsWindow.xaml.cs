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
    public string SelectedColorScheme { get; private set; }
    public int SelectedMobilePopupWidth { get; private set; }
    public int SelectedMobilePopupHeight { get; private set; }
    public string SelectedMobilePopupPosition { get; private set; }

    public SettingsWindow(AppConfiguration configuration)
    {
        InitializeComponent();
        LocalizationService.Apply(this, configuration.Language);
        StartWithWindows = configuration.StartWithWindows;
        StartMinimized = configuration.StartMinimized;
        RcloneEnabled = configuration.RcloneEnabled;
        SelectedLanguage = string.IsNullOrWhiteSpace(configuration.Language) ? "English" : configuration.Language;
        SelectedColorScheme = ColorSchemes.FromId(configuration.ColorScheme).Id;
        SelectedMobilePopupPosition = string.Equals(configuration.MobilePopupPosition, "center", StringComparison.OrdinalIgnoreCase)
            ? "center"
            : "bottomright";
        StartWithWindowsCheckBox.IsChecked = StartWithWindows;
        StartMinimizedCheckBox.IsChecked = StartMinimized;
        RcloneEnabledCheckBox.IsChecked = RcloneEnabled;
        PopupWidthTextBox.Text = configuration.MobilePopupWidth.ToString();
        PopupHeightTextBox.Text = configuration.MobilePopupHeight.ToString();
        LanguageComboBox.SelectedItem = LanguageComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), SelectedLanguage, StringComparison.OrdinalIgnoreCase))
            ?? LanguageComboBox.Items[0];
        ColorSchemeComboBox.SelectedItem = ColorSchemeComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), SelectedColorScheme, StringComparison.OrdinalIgnoreCase))
            ?? ColorSchemeComboBox.Items[0];
        PopupPositionComboBox.SelectedItem = PopupPositionComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), SelectedMobilePopupPosition, StringComparison.OrdinalIgnoreCase))
            ?? PopupPositionComboBox.Items[0];
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        StartMinimized = StartMinimizedCheckBox.IsChecked == true;
        RcloneEnabled = RcloneEnabledCheckBox.IsChecked == true;
        SelectedLanguage = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "English";
        SelectedColorScheme = (ColorSchemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ocean";
        SelectedMobilePopupWidth = ParseClamped(PopupWidthTextBox.Text, 600, 300, 2000);
        SelectedMobilePopupHeight = ParseClamped(PopupHeightTextBox.Text, 760, 360, 2000);
        SelectedMobilePopupPosition = (PopupPositionComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "bottomright";
        DialogResult = true;
    }

    private static int ParseClamped(string? text, int fallback, int min, int max)
    {
        if (!int.TryParse(text, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }
}
