using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Services.Interfaces
{
    public interface IAutomationService
    {
        event EventHandler<AutomationEvent>? RuleTriggered;
        event EventHandler<Profile>? ProfileApplied;
        event EventHandler<bool>? AutomationStateChanged;

        bool IsRunning { get; }
        AutomationConfiguration Configuration { get; }
        Profile? CurrentProfile { get; }

        // Profile management
        Task<List<Profile>> GetProfilesAsync();
        Task<Profile?> GetProfileAsync(Guid profileId);
        Task<Profile> CreateProfileAsync(Profile profile);
        Task<Profile> UpdateProfileAsync(Profile profile);
        Task<bool> DeleteProfileAsync(Guid profileId);
        Task<bool> ApplyProfileAsync(Guid profileId);
        Task<Profile> CreateProfileFromCurrentStateAsync(string name, string description);
        Task<bool> SetDefaultProfileAsync(Guid profileId);

        // Rule management
        Task<List<AutomationRule>> GetRulesAsync();
        Task<AutomationRule?> GetRuleAsync(Guid ruleId);
        Task<AutomationRule> CreateRuleAsync(AutomationRule rule);
        Task<AutomationRule> UpdateRuleAsync(AutomationRule rule);
        Task<bool> DeleteRuleAsync(Guid ruleId);
        Task<bool> EnableRuleAsync(Guid ruleId, bool enabled);
        Task<bool> TestRuleAsync(Guid ruleId);

        // Automation engine
        Task<bool> StartAutomationAsync();
        Task<bool> StopAutomationAsync();
        Task EvaluateRulesAsync();
        Task<bool> TriggerRuleManuallyAsync(Guid ruleId);

        // Context and history
        Task<AutomationContext> GetCurrentContextAsync();
        Task<List<AutomationEvent>> GetHistoryAsync(int count = 50);
        Task ClearHistoryAsync();

        // Configuration
        Task<bool> SaveConfigurationAsync();
        Task<bool> LoadConfigurationAsync();
        Task<bool> ExportConfigurationAsync(string filePath);
        Task<bool> ImportConfigurationAsync(string filePath);
    }
}