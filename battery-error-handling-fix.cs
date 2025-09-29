// Enhanced error handling for LinuxBatteryService.cs

public async Task<BatteryInfo?> GetBatteryInfoAsync()
{
    try
    {
        var batteryPath = await FindBatteryPathAsync();
        if (batteryPath == null)
        {
            return null; // Silent return for desktop systems
        }

        var info = new BatteryInfo();
        bool hasValidData = false;

        // Read charge level with error handling
        try
        {
            var capacityPath = _fileSystem.CombinePath(batteryPath, "capacity");
            if (_fileSystem.FileExists(capacityPath))
            {
                var value = (await _fileSystem.ReadFileAsync(capacityPath)).Trim();
                if (int.TryParse(value, out var capacity) && capacity >= 0 && capacity <= 100)
                {
                    info.ChargeLevel = capacity;
                    hasValidData = true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Failed to read battery capacity: {ex.Message}");
        }

        // Read status with error handling
        try
        {
            var statusPath = _fileSystem.CombinePath(batteryPath, "status");
            if (_fileSystem.FileExists(statusPath))
            {
                var status = (await _fileSystem.ReadFileAsync(statusPath)).Trim();
                info.IsCharging = status?.ToLower() == "charging";
                hasValidData = true;
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Failed to read battery status: {ex.Message}");
        }

        // Only return battery info if we got at least some valid data
        return hasValidData ? info : null;
    }
    catch (Exception ex)
    {
        Logger.Error("Failed to get battery info", ex);
        return null;
    }
}

// Add system type detection
private async Task<bool> IsDesktopSystemAsync()
{
    try
    {
        // Check for desktop indicators
        var chassisTypePath = "/sys/class/dmi/id/chassis_type";
        if (_fileSystem.FileExists(chassisTypePath))
        {
            var chassisType = (await _fileSystem.ReadFileAsync(chassisTypePath)).Trim();
            // Chassis types: 3=Desktop, 9=Laptop, 10=Notebook
            return chassisType == "3";
        }

        // Fallback: check for AC adapter without battery
        var powerSupplyPath = "/sys/class/power_supply";
        if (_fileSystem.DirectoryExists(powerSupplyPath))
        {
            var supplies = _fileSystem.GetDirectories(powerSupplyPath);
            bool hasAC = supplies.Any(p => Path.GetFileName(p).StartsWith("AC"));
            bool hasBattery = supplies.Any(p => Path.GetFileName(p).StartsWith("BAT"));

            return hasAC && !hasBattery; // AC power but no battery = desktop
        }

        return false;
    }
    catch
    {
        return false;
    }
}