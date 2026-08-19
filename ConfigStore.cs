using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SidebarDock;

public static class ConfigStore
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SidebarDock");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    /// <summary>Percorso di config.json, usato dalla finestra Impostazioni per aprirlo con l'editor predefinito.</summary>
    public static string ConfigFilePath => ConfigPath;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static DockConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return DefaultConfig();

        var json = File.ReadAllText(ConfigPath);
        var config = JsonSerializer.Deserialize<DockConfig>(json, SerializerOptions);
        return config ?? DefaultConfig();
    }

    public static void Save(DockConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(ConfigPath, json);
    }

    // Placeholder di partenza: modifica pure a mano config.json una volta generato,
    // oppure aggiungi un'UI di gestione in un secondo momento.
    private static DockConfig DefaultConfig() => new()
    {
        Settings = new DockSettings(),
        Items =
        [
            new DockItem { Name = "Esplora file", ExecutablePath = "explorer.exe", RunAsAdministrator = false },
            new DockItem { Name = "Terminale", ExecutablePath = "wt.exe", RunAsAdministrator = false, WorkingDirectory = "C:\\" },
        ]
    };
}
