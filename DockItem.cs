using System.Text.Json.Serialization;
using System.Windows.Media;

namespace SidebarDock;

/// <summary>Una singola scorciatoia mostrata nella sidebar.</summary>
public class DockItem
{
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    /// <summary>Argomenti opzionali passati a Process.Start.</summary>
    public string Arguments { get; set; } = "";

    /// <summary>
    /// Percorso opzionale di un'immagine (png/ico/jpg/...) da usare come icona al posto di
    /// quella estratta automaticamente da ExecutablePath. Lascia vuoto per usare l'estrazione
    /// automatica.
    /// </summary>
    public string IconPath { get; set; } = "";

    /// <summary>Icona risolta a runtime (da IconPath se impostato, altrimenti da ExecutablePath) — non salvata in config.json.</summary>
    [JsonIgnore]
    public ImageSource? IconSource { get; set; }

    /// <summary>Se true, l'applicazione verrà avviata come amministratore.</summary>
    public bool RunAsAdministrator { get; set; }

    /// <summary>Percorso di working directory da usare all'avvio (opzionale).</summary>
    public string WorkingDirectory { get; set; } = "";
}
