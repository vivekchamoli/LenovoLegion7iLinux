using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ReactiveUI;

namespace LenovoLegionToolkit.Avalonia.Models
{
    public class FeatureCard : ReactiveObject
    {
        private bool _isEnabled;
        private bool _isLoading;
        private string _status = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public HardwareFeature Feature { get; set; }
        public FeatureCardType Type { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public ObservableCollection<string> Options { get; set; } = new();
        public string? SelectedOption { get; set; }
        public Func<FeatureCard, Task>? OnToggled { get; set; }
        public Func<FeatureCard, string, Task>? OnOptionSelected { get; set; }
    }

    public enum FeatureCardType
    {
        Toggle,
        Dropdown,
        Button,
        Display,
        Slider
    }

    public class FeatureGroup : ReactiveObject
    {
        private bool _isExpanded = true;

        public string Name { get; set; } = string.Empty;
        public ObservableCollection<FeatureCard> Features { get; set; } = new();

        public bool IsExpanded
        {
            get => _isExpanded;
            set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
        }
    }
}