using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace SidebarDock;

public partial class MainWindow : Window
{
    private readonly DockConfig _config = ConfigStore.Load();
    private readonly ResourceMonitor _monitor = new();
    private readonly DispatcherTimer _metricsTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private double? _anchoredLeft;
    private double? _anchoredTop;
    private double? _anchoredWidth;
    private double? _anchoredHeight;

    public MainWindow()
    {
        InitializeComponent();

        if (_config.Settings.Mode == DockMode.Floating)
            ApplyFloatingChrome();
        else
            ApplyLayoutForEdge(_config.Settings.Edge);

        foreach (var item in _config.Items)
        {
            item.IconSource = string.IsNullOrWhiteSpace(item.IconPath)
                ? IconExtractor.GetIcon(item.ExecutablePath)
                : IconExtractor.LoadCustomImage(item.IconPath) ?? IconExtractor.GetIcon(item.ExecutablePath);
        }

        AppItemsControl.ItemsSource = _config.Items;
        ConfigStore.Save(_config); // garantisce che config.json esista subito, anche prima di una chiusura pulita

        if (_config.Settings.Mode == DockMode.Anchored)
        {
            ContentRendered += (_, _) => RegisterAnchoredAndRemember();

            // Dopo la registrazione, qualcosa (Windows/WPF, non il nostro codice) rimuove ancora
            // la finestra dal posto/dimensione assegnati non appena cambia l'area di lavoro dello
            // schermo — probabilmente un riallineamento automatico legato al fatto che la finestra
            // stessa ha appena reso "riservata" la zona in cui si trova (capita sia al primo avvio
            // sia quando si cambia spessore/bordo a caldo dalle Impostazioni). Come guardia: se
            // posizione o dimensione si allontanano da quelle assegnate da RegisterAppBar, si
            // riportano subito a posto.
            LocationChanged += (_, _) => ReassertAnchoredBounds();
            SizeChanged += (_, _) => ReassertAnchoredBounds();
        }

        Closed += (_, _) =>
        {
            if (_config.Settings.Mode == DockMode.Anchored)
            {
                // Niente da salvare qui: se nel frattempo l'utente ha modificato config.json a
                // mano (es. con "Apri config.json" dalle Impostazioni), riscrivere lo stato
                // caricato all'avvio cancellerebbe quella modifica. Le uniche scritture legittime
                // sono quelle immediate della finestra Impostazioni.
                AppBarHelper.UnregisterAppBar(this);
            }
            else
            {
                // Ricorda posizione/dimensione scelte dall'utente per il prossimo avvio, ma
                // ricaricando prima da disco: se config.json è stato modificato a mano mentre
                // l'app era aperta, si aggiornano solo i campi Floating senza cancellare il resto.
                var onDisk = ConfigStore.Load();
                onDisk.Settings.FloatingLeft = Left;
                onDisk.Settings.FloatingTop = Top;
                onDisk.Settings.FloatingWidth = Width;
                onDisk.Settings.FloatingHeight = Height;
                ConfigStore.Save(onDisk);
            }

            _monitor.Dispose();
        };

        _metricsTimer.Tick += (_, _) => UpdateMetrics();
        _metricsTimer.Start();
        UpdateMetrics();
    }

    /// <summary>
    /// Modalità Floating: niente AppBar, finestra normale con bordo/titolo nativi di Windows,
    /// così l'utente la trascina e ridimensiona col mouse come una finestra qualsiasi.
    /// Posizione e dimensione vengono salvate alla chiusura e riproposte al riavvio.
    /// </summary>
    private void ApplyFloatingChrome()
    {
        // AllowsTransparency=True richiede WindowStyle=None: vanno disattivati insieme
        // prima che la finestra crei il proprio HWND (quindi qui, non a runtime).
        AllowsTransparency = false;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = true;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
        Title = "SidebarDock";

        Left = _config.Settings.FloatingLeft;
        Top = _config.Settings.FloatingTop;
        Width = _config.Settings.FloatingWidth;
        Height = _config.Settings.FloatingHeight;
    }

    private void UpdateMetrics()
    {
        var snap = _monitor.Sample();

        DateText.Text = DateTime.Now.ToString("dd-MM-yy");
        TimeText.Text = DateTime.Now.ToString("HH:mm");
        CpuText.Text = $"CPU {snap.CpuPercent:0}%";
        RamText.Text = $"RAM {snap.RamUsedPercent:0}%";
        DiskText.Text = $"{snap.DiskFreeGb:0}/{snap.DiskTotalGb:0} GB";
        NetText.Text = $"↓{FormatSpeed(snap.NetDownKbps)} ↑{FormatSpeed(snap.NetUpKbps)}";
    }

    private static string FormatSpeed(double kbps) =>
        kbps >= 1024 ? $"{kbps / 1024:0.#}M" : $"{kbps:0}K";

    /// <summary>
    /// Applica dal vivo un nuovo bordo/spessore alla barra già ancorata: la deregistra,
    /// riadatta il layout e la registra di nuovo come AppBar. Chiamata dalla finestra Impostazioni.
    /// </summary>
    public void ApplyAnchoredSettingsLive(AppBarEdge edge, double thickness)
    {
        AppBarHelper.UnregisterAppBar(this);
        ApplyLayoutForEdge(edge);
        RegisterAnchoredAndRemember();
    }

    private void RegisterAnchoredAndRemember()
    {
        AppBarHelper.RegisterAppBar(this, _config.Settings.Edge, _config.Settings.Thickness);
        _anchoredLeft = Left;
        _anchoredTop = Top;
        _anchoredWidth = Width;
        _anchoredHeight = Height;
    }

    private void ReassertAnchoredBounds()
    {
        if (_config.Settings.Mode != DockMode.Anchored || _anchoredLeft is null) return;
        if (Left != _anchoredLeft.Value) Left = _anchoredLeft.Value;
        if (Top != _anchoredTop!.Value) Top = _anchoredTop.Value;
        if (Width != _anchoredWidth!.Value) Width = _anchoredWidth.Value;
        if (Height != _anchoredHeight!.Value) Height = _anchoredHeight.Value;
    }

    /// <summary>
    /// Adatta orientamento del layout e dimensione iniziale della finestra al bordo scelto:
    /// verticale (icone impilate) per Left/Right, orizzontale per Top/Bottom.
    /// Resetta sempre da zero righe/colonne così può essere richiamata anche a runtime
    /// per passare da un orientamento all'altro (es. Top -> Left) senza stato residuo.
    /// </summary>
    private void ApplyLayoutForEdge(AppBarEdge edge)
    {
        var horizontal = edge is AppBarEdge.Top or AppBarEdge.Bottom;
        var thickness = _config.Settings.Thickness;

        RootGrid.RowDefinitions.Clear();
        RootGrid.ColumnDefinitions.Clear();

        if (horizontal)
        {
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetRow(AppScrollViewer, 0);
            Grid.SetColumn(AppScrollViewer, 0);
            Grid.SetRow(MetricsPanel, 0);
            Grid.SetColumn(MetricsPanel, 1);

            AppScrollViewer.Margin = new Thickness(12, 0, 0, 0);
            AppScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            AppScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            var appPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            appPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            AppItemsControl.ItemsPanel = new ItemsPanelTemplate(appPanelFactory);

            MetricsPanel.Orientation = Orientation.Horizontal;
            MetricsPanel.Margin = new Thickness(0, 4, 12, 4);
            MetricsPanel.VerticalAlignment = VerticalAlignment.Center;

            Height = thickness;
            Width = double.NaN;
        }
        else
        {
            RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(AppScrollViewer, 0);
            Grid.SetColumn(AppScrollViewer, 0);
            Grid.SetRow(MetricsPanel, 1);
            Grid.SetColumn(MetricsPanel, 0);

            AppScrollViewer.Margin = new Thickness(0, 12, 0, 0);
            AppScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            AppScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            var appPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            appPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            AppItemsControl.ItemsPanel = new ItemsPanelTemplate(appPanelFactory);

            MetricsPanel.Orientation = Orientation.Vertical;
            MetricsPanel.Margin = new Thickness(4, 0, 4, 12);
            MetricsPanel.VerticalAlignment = VerticalAlignment.Bottom;
            MetricsPanel.HorizontalAlignment = HorizontalAlignment.Center;

            Width = thickness;
            Height = double.NaN;
        }
    }

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (_config.Settings.Mode == DockMode.Anchored)
            Topmost = false;
    }

    private void OnContextMenuClosed(object sender, RoutedEventArgs e)
    {
        if (_config.Settings.Mode == DockMode.Anchored)
            Topmost = true;
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow(_config, this) { Owner = this };
        settings.ShowDialog();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnAppIconClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DockItem item }) return;

        try
        {
            if (item.ExecutablePath.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) &&
                item.Arguments.StartsWith("search-ms:", StringComparison.OrdinalIgnoreCase))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start {item.Arguments}",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                Process.Start(psi);
            }
            else
            {
                // Se il percorso è una cartella, aprila con explorer.exe invece di tentare di eseguirla
                if (Directory.Exists(item.ExecutablePath))
                {
                    Process.Start("explorer.exe", item.ExecutablePath);
                }
                else
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = item.ExecutablePath,
                        Arguments = item.Arguments,
                        UseShellExecute = true
                    };
                    // Usa il verbo "runas" solo se RunAsAdministrator è true
                    if (item.RunAsAdministrator)
                    {
                        psi.Verb = "runas";
                    }
                    // Imposta la working directory se specificata
                    if (!string.IsNullOrWhiteSpace(item.WorkingDirectory))
                    {
                        psi.WorkingDirectory = item.WorkingDirectory;
                    }
                    Process.Start(psi);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Impossibile avviare {item.Name}: {ex.Message}");
        }
    }
}
