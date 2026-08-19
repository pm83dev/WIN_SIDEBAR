using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;

namespace SidebarDock;

/// <summary>Snapshot delle metriche mostrate in fondo alla sidebar.</summary>
public record ResourceSnapshot(
    double CpuPercent,
    double RamUsedPercent,
    double DiskFreeGb,
    double DiskTotalGb,
    double NetDownKbps,
    double NetUpKbps);

/// <summary>
/// Legge le metriche di sistema. La temperatura NON è inclusa: richiede LibreHardwareMonitorLib
/// (vedi commento nel .csproj) perché .NET non espone sensori hardware nativamente.
/// </summary>
public class ResourceMonitor : IDisposable
{
    private readonly PerformanceCounter _cpuCounter = new("Processore", "% Tempo processore", "_Total");
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastSampleTime = DateTime.UtcNow;

    public ResourceMonitor()
    {
        // Prima lettura del contatore CPU è sempre 0: la "scaldiamo" subito.
        _cpuCounter.NextValue();

        var (rx, tx) = GetTotalNetworkBytes();
        _lastBytesReceived = rx;
        _lastBytesSent = tx;
    }

    public ResourceSnapshot Sample()
    {
        var cpu = _cpuCounter.NextValue();

        var ram = GetRamUsage();
        var disk = GetPrimaryDiskUsage();
        var (down, up) = GetNetworkSpeedKbps();

        return new ResourceSnapshot(
            CpuPercent: Math.Round(cpu, 0),
            RamUsedPercent: Math.Round(ram, 0),
            DiskFreeGb: disk.freeGb,
            DiskTotalGb: disk.totalGb,
            NetDownKbps: down,
            NetUpKbps: up);
    }

    private static double GetRamUsage()
    {
        // GlobalMemoryStatusEx darebbe un dato più preciso; per lo scheletro basta questo.
        var gcInfo = GC.GetGCMemoryInfo();
        var totalBytes = gcInfo.TotalAvailableMemoryBytes;
        if (totalBytes <= 0) return 0;

        using var pc = new PerformanceCounter("Memoria", "MByte disponibili");
        var availableMb = pc.NextValue();
        var totalMb = totalBytes / 1024.0 / 1024.0;
        var usedMb = totalMb - availableMb;
        return usedMb / totalMb * 100.0;
    }

    private static (double freeGb, double totalGb) GetPrimaryDiskUsage()
    {
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
        var freeGb = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
        var totalGb = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
        return (Math.Round(freeGb, 1), Math.Round(totalGb, 1));
    }

    private static (long rx, long tx) GetTotalNetworkBytes()
    {
        var stats = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.GetIPv4Statistics())
            .ToList();

        return (stats.Sum(s => s.BytesReceived), stats.Sum(s => s.BytesSent));
    }

    private (double downKbps, double upKbps) GetNetworkSpeedKbps()
    {
        var now = DateTime.UtcNow;
        var (rx, tx) = GetTotalNetworkBytes();

        var elapsedSeconds = Math.Max((now - _lastSampleTime).TotalSeconds, 0.001);
        var downKbps = (rx - _lastBytesReceived) / 1024.0 / elapsedSeconds;
        var upKbps = (tx - _lastBytesSent) / 1024.0 / elapsedSeconds;

        _lastBytesReceived = rx;
        _lastBytesSent = tx;
        _lastSampleTime = now;

        return (Math.Round(Math.Max(downKbps, 0), 0), Math.Round(Math.Max(upKbps, 0), 0));
    }

    public void Dispose() => _cpuCounter.Dispose();
}
