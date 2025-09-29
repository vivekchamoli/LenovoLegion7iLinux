using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.AI;

/// <summary>
/// Advanced AI-powered thermal management for Legion Slim 7i Gen 9
/// Uses predictive algorithms to prevent thermal throttling and optimize performance
/// </summary>
public class ThermalOptimizer
{
    private readonly Gen9ECController _ecController;
    private readonly List<ThermalState> _thermalHistory = new();
    private readonly object _historyLock = new();
    private const int MaxHistorySize = 300; // 5 minutes at 1Hz sampling
    private const int PredictionHorizonSeconds = 60;

    public ThermalOptimizer(Gen9ECController ecController)
    {
        _ecController = ecController ?? throw new ArgumentNullException(nameof(ecController));
    }

    /// <summary>
    /// Optimize thermal performance in real-time for current workload
    /// </summary>
    public async Task<ThermalOptimizationResult> OptimizeThermalPerformanceAsync(WorkloadType workloadType)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting thermal optimization for workload: {workloadType}");

        var optimizationResult = new ThermalOptimizationResult
        {
            WorkloadType = workloadType,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Collect current thermal state
            var currentState = await CollectThermalStateAsync();

            // Add to history
            lock (_historyLock)
            {
                _thermalHistory.Add(currentState);
                while (_thermalHistory.Count > MaxHistorySize)
                    _thermalHistory.RemoveAt(0);
            }

            // Predict future thermal state
            var predictions = PredictThermalState(_thermalHistory, PredictionHorizonSeconds);

            // Generate workload-specific optimizations
            var settings = workloadType switch
            {
                WorkloadType.Gaming => OptimizeForGaming(predictions),
                WorkloadType.Productivity => OptimizeForProductivity(predictions),
                WorkloadType.AIWorkload => OptimizeForAI(predictions),
                WorkloadType.Balanced => OptimizeBalanced(predictions),
                _ => OptimizeBalanced(predictions)
            };

            // Apply optimizations
            await ApplyOptimizationsAsync(settings);

            optimizationResult.AppliedSettings = settings;
            optimizationResult.PredictedTemperatures = predictions;
            optimizationResult.ThrottleRisk = CalculateThrottleRisk(predictions);
            optimizationResult.Recommendations = GenerateRecommendations(predictions, currentState);
            optimizationResult.Success = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Thermal optimization completed successfully. Throttle risk: {optimizationResult.ThrottleRisk:P1}");

        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Thermal optimization failed", ex);

            optimizationResult.Success = false;
            optimizationResult.ErrorMessage = ex.Message;
        }

        optimizationResult.EndTime = DateTime.UtcNow;
        return optimizationResult;
    }

    /// <summary>
    /// Collect current thermal state from Gen 9 sensors
    /// </summary>
    private async Task<ThermalState> CollectThermalStateAsync()
    {
        var sensorData = await _ecController.ReadSensorDataAsync();

        return new ThermalState
        {
            CpuTemp = sensorData.CpuPackageTemp,
            GpuTemp = sensorData.GpuTemp,
            GpuHotspot = sensorData.GpuHotspot,
            GpuMemoryTemp = sensorData.GpuMemoryTemp,
            VrmTemp = sensorData.VrmTemp,
            SsdTemp = sensorData.SsdTemp,
            Fan1Speed = sensorData.Fan1Speed,
            Fan2Speed = sensorData.Fan2Speed,
            AmbientTemp = 25, // Estimated
            Timestamp = sensorData.Timestamp
        };
    }

    /// <summary>
    /// Predict future thermal state using trend analysis
    /// </summary>
    private ThermalPredictions PredictThermalState(List<ThermalState> history, int secondsAhead)
    {
        if (history.Count < 5)
            return GetDefaultPredictions();

        var recentHistory = history.TakeLast(30).ToList(); // Last 30 seconds

        // Calculate temperature trends
        var cpuTrend = CalculateTemperatureTrend(recentHistory.Select(h => h.CpuTemp).ToList());
        var gpuTrend = CalculateTemperatureTrend(recentHistory.Select(h => h.GpuTemp).ToList());

        var currentState = history.Last();

        return new ThermalPredictions
        {
            PredictedCpuTemp = Math.Max(0, currentState.CpuTemp + (cpuTrend * secondsAhead)),
            PredictedGpuTemp = Math.Max(0, currentState.GpuTemp + (gpuTrend * secondsAhead)),
            PredictedGpuHotspot = Math.Max(0, currentState.GpuHotspot + (gpuTrend * secondsAhead * 1.2)),
            PredictedVrmTemp = Math.Max(0, currentState.VrmTemp + ((cpuTrend + gpuTrend) * 0.5 * secondsAhead)),
            Confidence = CalculatePredictionConfidence(recentHistory)
        };
    }

    /// <summary>
    /// Calculate temperature trend (°C per second)
    /// </summary>
    private double CalculateTemperatureTrend(List<byte> temperatures)
    {
        if (temperatures.Count < 2)
            return 0;

        var x = Enumerable.Range(0, temperatures.Count).Select(i => (double)i).ToArray();
        var y = temperatures.Select(t => (double)t).ToArray();

        // Simple linear regression
        var n = temperatures.Count;
        var sumX = x.Sum();
        var sumY = y.Sum();
        var sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
        var sumX2 = x.Select(xi => xi * xi).Sum();

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return slope;
    }

    /// <summary>
    /// Gaming-specific optimizations
    /// </summary>
    private OptimizationSettings OptimizeForGaming(ThermalPredictions predictions)
    {
        return new OptimizationSettings
        {
            CpuPL1 = 55,  // Base power
            CpuPL2 = 140, // Turbo power
            GpuTGP = 140, // Max GPU power
            FanProfile = FanProfile.Aggressive,
            Recommendations = new List<string>
            {
                "Enable GPU overclock +150MHz core, +500MHz memory",
                "Set Windows to High Performance mode",
                "Disable CPU E-cores for gaming",
                "Enable Resizable BAR"
            }
        };
    }

    /// <summary>
    /// Productivity workload optimizations
    /// </summary>
    private OptimizationSettings OptimizeForProductivity(ThermalPredictions predictions)
    {
        return new OptimizationSettings
        {
            CpuPL1 = 65,  // Higher base for sustained loads
            CpuPL2 = 115, // Lower turbo for consistency
            GpuTGP = 60,  // Reduced GPU power
            FanProfile = FanProfile.Quiet,
            Recommendations = new List<string>
            {
                "Enable all CPU cores",
                "Optimize for battery life",
                "Enable Intel Speed Shift"
            }
        };
    }

    /// <summary>
    /// AI/ML workload optimizations
    /// </summary>
    private OptimizationSettings OptimizeForAI(ThermalPredictions predictions)
    {
        return new OptimizationSettings
        {
            CpuPL1 = 45,  // Lower CPU power
            CpuPL2 = 90,
            GpuTGP = 140, // Maximum GPU power for CUDA
            FanProfile = FanProfile.MaxPerformance,
            Recommendations = new List<string>
            {
                "Enable CUDA acceleration",
                "Set GPU to Prefer Maximum Performance",
                "Enable GPU memory overclocking",
                "Disable GPU power saving features"
            }
        };
    }

    /// <summary>
    /// Balanced optimizations
    /// </summary>
    private OptimizationSettings OptimizeBalanced(ThermalPredictions predictions)
    {
        var throttleRisk = CalculateThrottleRisk(predictions);

        if (throttleRisk > 0.7)
        {
            // High throttle risk - reduce power
            return new OptimizationSettings
            {
                CpuPL1 = 45,
                CpuPL2 = 100,
                GpuTGP = 100,
                FanProfile = FanProfile.Aggressive,
                Recommendations = new List<string> { "Reducing power to prevent throttling" }
            };
        }
        else if (throttleRisk < 0.3)
        {
            // Low throttle risk - increase performance
            return new OptimizationSettings
            {
                CpuPL1 = 55,
                CpuPL2 = 130,
                GpuTGP = 130,
                FanProfile = FanProfile.Balanced,
                Recommendations = new List<string> { "Increasing performance - low thermal risk" }
            };
        }

        return new OptimizationSettings
        {
            CpuPL1 = 50,
            CpuPL2 = 115,
            GpuTGP = 115,
            FanProfile = FanProfile.Balanced,
            Recommendations = new List<string> { "Maintaining balanced performance" }
        };
    }

    /// <summary>
    /// Apply optimization settings to hardware
    /// </summary>
    private async Task ApplyOptimizationsAsync(OptimizationSettings settings)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Applying optimization settings: PL1={settings.CpuPL1}W, PL2={settings.CpuPL2}W, GPU TGP={settings.GpuTGP}W");

        await _ecController.SetPowerLimitsAsync(settings.CpuPL1, settings.CpuPL2, settings.GpuTGP);

        // Apply fan profile if needed
        switch (settings.FanProfile)
        {
            case FanProfile.Aggressive:
                await _ecController.FixFanCurveAsync(); // Use optimized curve
                break;
            case FanProfile.Quiet:
                // Implement quiet fan profile
                break;
            case FanProfile.MaxPerformance:
                // Implement max performance fan profile
                break;
        }
    }

    /// <summary>
    /// Calculate throttle risk based on predictions
    /// </summary>
    private double CalculateThrottleRisk(ThermalPredictions predictions)
    {
        var risks = new List<double>();

        // CPU throttle risk (100°C limit)
        if (predictions.PredictedCpuTemp >= 100)
            risks.Add(1.0);
        else if (predictions.PredictedCpuTemp >= 95)
            risks.Add((predictions.PredictedCpuTemp - 95) / 5.0);
        else
            risks.Add(0.0);

        // GPU throttle risk (87°C limit)
        if (predictions.PredictedGpuTemp >= 87)
            risks.Add(1.0);
        else if (predictions.PredictedGpuTemp >= 82)
            risks.Add((predictions.PredictedGpuTemp - 82) / 5.0);
        else
            risks.Add(0.0);

        return risks.Max();
    }

    /// <summary>
    /// Generate actionable recommendations
    /// </summary>
    private List<string> GenerateRecommendations(ThermalPredictions predictions, ThermalState currentState)
    {
        var recommendations = new List<string>();

        if (predictions.PredictedCpuTemp > 90)
            recommendations.Add("CPU running hot - consider reducing workload or improving ventilation");

        if (predictions.PredictedGpuTemp > 80)
            recommendations.Add("GPU thermal limit approaching - reduce graphics settings or enable more aggressive fan curve");

        if (currentState.SsdTemp > 70)
            recommendations.Add("SSD temperature elevated - ensure adequate case ventilation");

        if (predictions.Confidence < 0.5)
            recommendations.Add("Thermal predictions have low confidence - continuing to gather data");

        return recommendations;
    }

    private double CalculatePredictionConfidence(List<ThermalState> history)
    {
        if (history.Count < 10)
            return 0.3;

        // Calculate temperature variance - higher variance = lower confidence
        var cpuVariance = CalculateVariance(history.Select(h => (double)h.CpuTemp).ToList());
        var gpuVariance = CalculateVariance(history.Select(h => (double)h.GpuTemp).ToList());

        var avgVariance = (cpuVariance + gpuVariance) / 2.0;

        // Convert variance to confidence (inverse relationship)
        return Math.Max(0.1, Math.Min(1.0, 1.0 - (avgVariance / 100.0)));
    }

    private double CalculateVariance(List<double> values)
    {
        if (values.Count < 2)
            return 0;

        var mean = values.Average();
        var variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
        return variance;
    }

    private ThermalPredictions GetDefaultPredictions()
    {
        return new ThermalPredictions
        {
            PredictedCpuTemp = 70,
            PredictedGpuTemp = 65,
            PredictedGpuHotspot = 75,
            PredictedVrmTemp = 65,
            Confidence = 0.5
        };
    }
}

#region Data Structures

/// <summary>
/// Current thermal state
/// </summary>
public struct ThermalState
{
    public byte CpuTemp { get; set; }
    public byte GpuTemp { get; set; }
    public byte GpuHotspot { get; set; }
    public byte GpuMemoryTemp { get; set; }
    public byte VrmTemp { get; set; }
    public byte SsdTemp { get; set; }
    public int Fan1Speed { get; set; }
    public int Fan2Speed { get; set; }
    public byte AmbientTemp { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Thermal predictions
/// </summary>
public struct ThermalPredictions
{
    public double PredictedCpuTemp { get; set; }
    public double PredictedGpuTemp { get; set; }
    public double PredictedGpuHotspot { get; set; }
    public double PredictedVrmTemp { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// Optimization settings
/// </summary>
public class OptimizationSettings
{
    public int CpuPL1 { get; set; }
    public int CpuPL2 { get; set; }
    public int GpuTGP { get; set; }
    public FanProfile FanProfile { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Thermal optimization result
/// </summary>
public class ThermalOptimizationResult
{
    public WorkloadType WorkloadType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public OptimizationSettings? AppliedSettings { get; set; }
    public ThermalPredictions PredictedTemperatures { get; set; }
    public double ThrottleRisk { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Workload types for optimization
/// </summary>
public enum WorkloadType
{
    Balanced,
    Gaming,
    Productivity,
    AIWorkload
}

/// <summary>
/// Fan profile types
/// </summary>
public enum FanProfile
{
    Quiet,
    Balanced,
    Aggressive,
    MaxPerformance
}

#endregion