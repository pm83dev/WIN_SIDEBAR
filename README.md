# SidebarDock — scheletro

Sidebar verticale stile Ubuntu dock, in WPF/.NET 8. Nessuna dipendenza esterna
nello scheletro base (LibreHardwareMonitorLib è commentata nel .csproj per quando
vorrai aggiungere la temperatura CPU).

## Come compilare

```
cd SidebarDock
dotnet build
dotnet run
```

Non l'ho potuto compilare in questo ambiente (niente .NET SDK, rete sandbox
limitata) — provalo sul tuo T14/ProDesk e segnalami eventuali errori di
compilazione, li sistemiamo.

## Cosa fa già

- Finestra ancorata a sinistra, larghezza fissa 64px, sempre in primo piano
- Si registra come **AppBar di Windows** (`AppBarHelper.cs`): riserva lo spazio
  schermo come fa la taskbar, le finestre massimizzate non ci finiscono sotto
- Elenco app cliccabili caricato da `%appdata%\SidebarDock\config.json`
  (al primo avvio crea 2 voci placeholder: Esplora file, Terminale)
- Blocco metriche in fondo: CPU%, RAM%, spazio disco, velocità rete —
  aggiornamento ogni 2 secondi via `DispatcherTimer`

## Cosa manca (in ordine di probabile priorità)

1. **Icone vere** — ora c'è solo la prima lettera del nome come placeholder.
   Da estrarre con `System.Drawing.Icon.ExtractAssociatedIcon(path)` e convertire
   in `BitmapSource` per l'XAML, oppure `SHGetFileInfo` via P/Invoke se vuoi
   icone a risoluzione più alta.
2. **UI per aggiungere/rimuovere app** — oggi si modifica `config.json` a mano.
   Un drag&drop di .exe/.lnk sulla sidebar sarebbe la strada più "da dock".
3. **Indicatore app in esecuzione** — enumerare finestre con `EnumWindows` +
   `GetWindowThreadProcessId`, confrontare con `ExecutablePath` degli item, e
   disegnare un pallino/barretta a lato dell'icona attiva.
4. **Temperatura CPU** — aggiungi il pacchetto NuGet `LibreHardwareMonitorLib`
   (decommenta la riga nel `.csproj`), inizializzi un `Computer` con
   `IsCpuEnabled = true` e leggi i sensori di tipo `SensorType.Temperature`.
   Richiede privilegi amministratore per leggere i sensori hardware.
5. **RAM più precisa** — `ResourceMonitor.GetRamUsage()` usa un approccio
   approssimato via `PerformanceCounter`; per un dato affidabile su tutte le
   versioni di Windows conviene passare a `GlobalMemoryStatusEx` (P/Invoke).
6. **Autostart** — chiave di registro
   `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, stessa
   tecnica che usa TrafficMonitor.
7. **Multi-monitor** — `AppBarHelper` oggi usa solo `SystemParameters.PrimaryScreenWidth/Height`.
   Se vuoi la sidebar su un monitor specifico, serve passare per
   `System.Windows.Forms.Screen.AllScreens` e adattare il calcolo del rettangolo.

## Note tecniche

- **Multi-schermo/DPI**: se hai monitor con scaling diverso (il tuo caso, con
  due Philips 27" QHD), testa bene `AppBarHelper` — le coordinate di Windows
  sono in pixel fisici, WPF lavora in unità indipendenti dal DPI, quindi in
  setup misti potresti dover moltiplicare/dividere per il fattore di scala.
- **AppBar e riavvii**: se l'app crasha senza chiamare `UnregisterAppBar`,
  Windows a volte lascia lo spazio "riservato" finché non risistemi manualmente
  la finestra o riavvii explorer.exe. Non è un bug del codice, è comportamento
  noto dell'API.
