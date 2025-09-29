// Improved battery detection for LinuxBatteryService.cs

private string? _batteryPath = null;
private bool _batterySearchComplete = false;

private async Task<string?> FindBatteryPathAsync()
{
    if (_batterySearchComplete && _batteryPath != null)
        return _batteryPath;

    try
    {
        var powerSupplyPath = "/sys/class/power_supply";
        if (!_fileSystem.DirectoryExists(powerSupplyPath))
        {
            _batterySearchComplete = true;
            return null;
        }

        // Look for any directory starting with BAT
        var powerSupplies = _fileSystem.GetDirectories(powerSupplyPath);
        var batteryPaths = powerSupplies
            .Where(path => Path.GetFileName(path).StartsWith("BAT") ||
                          Path.GetFileName(path).StartsWith("CMB"))
            .OrderBy(path => path) // Prefer BAT0 over BAT1
            .ToList();

        foreach (var batteryPath in batteryPaths)
        {
            // Verify it's actually a battery by checking for capacity file
            var capacityPath = _fileSystem.CombinePath(batteryPath, "capacity");
            if (_fileSystem.FileExists(capacityPath))
            {
                _batteryPath = batteryPath;
                _batterySearchComplete = true;
                Logger.Info($"Battery found at: {batteryPath}");
                return batteryPath;
            }
        }

        _batterySearchComplete = true;
        Logger.Info("No battery detected (likely desktop system)");
        return null;
    }
    catch (Exception ex)
    {
        Logger.Error("Error discovering battery path", ex);
        _batterySearchComplete = true;
        return null;
    }
}

public async Task<BatteryInfo?> GetBatteryInfoAsync()
{
    try
    {
        var batteryPath = await FindBatteryPathAsync();
        if (batteryPath == null)
        {
            // Only log warning once, not every 5 seconds
            if (!_batterySearchComplete)
                Logger.Warning("No battery detected");
            return null;
        }

        // Rest of the existing battery reading code...
        // Replace BATTERY_PATH constant with batteryPath variable
    }
    catch (Exception ex)
    {
        Logger.Error("Failed to get battery info", ex);
        return null;
    }
}