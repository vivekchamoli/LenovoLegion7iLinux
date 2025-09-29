// Optimized polling for LinuxBatteryService.cs

public class LinuxBatteryService : IBatteryService
{
    private bool _hasBattery = true; // Assume true initially
    private int _noBatteryCount = 0;
    private const int MAX_NO_BATTERY_RETRIES = 3;

    public LinuxBatteryService(IFileSystemService fileSystem, IProcessRunner processRunner)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));

        // Start with faster polling, adjust based on battery presence
        _updateTimer = new System.Timers.Timer(5000); // Initial 5 seconds
        _updateTimer.Elapsed += async (s, e) => await UpdateBatteryInfoAsync();
        _updateTimer.Start();
    }

    private async Task UpdateBatteryInfoAsync()
    {
        var info = await GetBatteryInfoAsync();

        if (info != null)
        {
            // Battery found - keep normal polling
            _hasBattery = true;
            _noBatteryCount = 0;
            _updateTimer.Interval = 5000; // 5 seconds for battery systems
            BatteryInfoChanged?.Invoke(this, info);
        }
        else
        {
            // No battery detected
            _noBatteryCount++;

            if (_noBatteryCount >= MAX_NO_BATTERY_RETRIES)
            {
                // After 3 failures, assume no battery and slow down polling
                _hasBattery = false;
                _updateTimer.Interval = 60000; // 1 minute for desktop systems
                Logger.Info("No battery detected, reducing polling frequency");
            }
        }
    }

    // Alternative: Completely disable battery monitoring for desktop systems
    private void DisableBatteryMonitoring()
    {
        if (!_hasBattery && _noBatteryCount >= MAX_NO_BATTERY_RETRIES)
        {
            _updateTimer?.Stop();
            Logger.Info("Battery monitoring disabled for desktop system");
        }
    }
}