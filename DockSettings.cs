namespace SidebarDock;

/// <summary>Come la sidebar occupa lo schermo.</summary>
public enum DockMode
{
    /// <summary>Ancorata a un bordo dello schermo tramite l'API AppBar (spazio riservato, non spostabile).</summary>
    Anchored,

    /// <summary>Finestra libera: posizione e dimensione decise dall'utente trascinando/ridimensionando.</summary>
    Floating
}

/// <summary>Impostazioni di dimensione e posizione della sidebar, salvate in config.json.</summary>
public class DockSettings
{
    public DockMode Mode { get; set; } = DockMode.Anchored;

    /// <summary>Bordo dello schermo su cui ancorare la sidebar (usato solo con Mode = Anchored).</summary>
    public AppBarEdge Edge { get; set; } = AppBarEdge.Left;

    /// <summary>Spessore della barra in pixel (usato solo con Mode = Anchored): larghezza se Left/Right, altezza se Top/Bottom.</summary>
    public double Thickness { get; set; } = 64;

    // Usate solo con Mode = Floating: posizione/dimensione libera, aggiornate ad ogni chiusura.
    public double FloatingLeft { get; set; } = 100;
    public double FloatingTop { get; set; } = 100;
    public double FloatingWidth { get; set; } = 64;
    public double FloatingHeight { get; set; } = 420;
}
