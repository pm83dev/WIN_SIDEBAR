using System.Collections.Generic;

namespace SidebarDock;

/// <summary>Radice del file config.json: impostazioni della barra + elenco app.</summary>
public class DockConfig
{
    public DockSettings Settings { get; set; } = new();
    public List<DockItem> Items { get; set; } = new();
}
