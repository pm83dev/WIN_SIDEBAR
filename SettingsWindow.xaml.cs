using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace SidebarDock;

public partial class SettingsWindow : Window
{
    private readonly DockConfig _config;
    private readonly MainWindow _owner;

    public SettingsWindow(DockConfig config, MainWindow owner)
    {
        InitializeComponent();
        _config = config;
        _owner = owner;

        FloatingCheckBox.IsChecked = _config.Settings.Mode == DockMode.Floating;
        EdgeComboBox.SelectedIndex = _config.Settings.Edge switch
        {
            AppBarEdge.Left => 0,
            AppBarEdge.Right => 1,
            AppBarEdge.Top => 2,
            AppBarEdge.Bottom => 3,
            _ => 0
        };
        ThicknessSlider.Value = _config.Settings.Thickness;
        UpdateAnchoredPanelState();
    }

    private void OnModeChanged(object sender, RoutedEventArgs e) => UpdateAnchoredPanelState();

    private void UpdateAnchoredPanelState() =>
        AnchoredOptionsPanel.IsEnabled = FloatingCheckBox.IsChecked != true;

    private void OnThicknessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThicknessValueText != null)
            ThicknessValueText.Text = $"{ThicknessSlider.Value:0} px";
    }

    private void OnOpenConfigClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Se il file non esiste ancora (mai salvato) lo si crea prima con lo stato corrente,
            // altrimenti l'editor predefinito si aprirebbe su un percorso inesistente.
            if (!System.IO.File.Exists(ConfigStore.ConfigFilePath))
                ConfigStore.Save(_config);

            Process.Start(new ProcessStartInfo(ConfigStore.ConfigFilePath) { UseShellExecute = true });
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(this,
                $"Impossibile aprire config.json:{System.Environment.NewLine}{ex.Message}",
                "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        var newMode = FloatingCheckBox.IsChecked == true ? DockMode.Floating : DockMode.Anchored;
        var newEdge = (AppBarEdge)System.Enum.Parse(typeof(AppBarEdge), (string)((ComboBoxItem)EdgeComboBox.SelectedItem).Tag);
        var newThickness = ThicknessSlider.Value;

        var modeChanged = newMode != _config.Settings.Mode;

        _config.Settings.Mode = newMode;
        _config.Settings.Edge = newEdge;
        _config.Settings.Thickness = newThickness;
        ConfigStore.Save(_config);

        if (modeChanged)
        {
            MessageBox.Show(this,
                "La modalità è cambiata: riavvia SidebarDock per applicarla.",
                "Riavvio necessario", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (_config.Settings.Mode == DockMode.Anchored)
        {
            _owner.ApplyAnchoredSettingsLive(newEdge, newThickness);
        }

        Close();
    }
}
