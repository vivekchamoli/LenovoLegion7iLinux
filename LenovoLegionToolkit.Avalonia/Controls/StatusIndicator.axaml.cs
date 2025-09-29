using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace LenovoLegionToolkit.Avalonia.Controls
{
    public partial class StatusIndicator : UserControl
    {
        public enum StatusType
        {
            Offline,
            Online,
            Warning,
            Error,
            Busy,
            Success
        }

        public static readonly StyledProperty<StatusType> StatusProperty =
            AvaloniaProperty.Register<StatusIndicator, StatusType>(nameof(Status), StatusType.Offline);

        public static readonly StyledProperty<double> SizeProperty =
            AvaloniaProperty.Register<StatusIndicator, double>(nameof(Size), 12.0);

        public static readonly StyledProperty<bool> IsBlinkingProperty =
            AvaloniaProperty.Register<StatusIndicator, bool>(nameof(IsBlinking), false);

        public static readonly StyledProperty<bool> ShowInnerDotProperty =
            AvaloniaProperty.Register<StatusIndicator, bool>(nameof(ShowInnerDot), false);

        public static readonly StyledProperty<IBrush> StatusBrushProperty =
            AvaloniaProperty.Register<StatusIndicator, IBrush>(nameof(StatusBrush), Brushes.Gray);

        public StatusType Status
        {
            get => GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public double Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public bool IsBlinking
        {
            get => GetValue(IsBlinkingProperty);
            set => SetValue(IsBlinkingProperty, value);
        }

        public bool ShowInnerDot
        {
            get => GetValue(ShowInnerDotProperty);
            set => SetValue(ShowInnerDotProperty, value);
        }

        public IBrush StatusBrush
        {
            get => GetValue(StatusBrushProperty);
            private set => SetValue(StatusBrushProperty, value);
        }

        public double InnerSize => Size * 0.4;

        public StatusIndicator()
        {
            InitializeComponent();
            UpdateStatusAppearance();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == StatusProperty || change.Property == IsBlinkingProperty)
            {
                UpdateStatusAppearance();
            }
        }

        private void UpdateStatusAppearance()
        {
            var indicator = this.FindControl<Ellipse>("PART_Indicator");
            if (indicator == null)
                return;

            // Set color based on status
            StatusBrush = Status switch
            {
                StatusType.Offline => new SolidColorBrush(Color.Parse("#808080")),
                StatusType.Online => new SolidColorBrush(Color.Parse("#4CAF50")),
                StatusType.Warning => new SolidColorBrush(Color.Parse("#FFC107")),
                StatusType.Error => new SolidColorBrush(Color.Parse("#F44336")),
                StatusType.Busy => new SolidColorBrush(Color.Parse("#2196F3")),
                StatusType.Success => new SolidColorBrush(Color.Parse("#4CAF50")),
                _ => new SolidColorBrush(Colors.Gray)
            };

            // Apply blinking class
            if (IsBlinking)
            {
                indicator.Classes.Add("Blinking");
            }
            else
            {
                indicator.Classes.Remove("Blinking");
            }

            // Auto-enable inner dot for certain states
            ShowInnerDot = Status == StatusType.Online || Status == StatusType.Success;
        }
    }
}