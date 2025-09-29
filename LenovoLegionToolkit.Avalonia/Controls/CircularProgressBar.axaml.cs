using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Shapes;

namespace LenovoLegionToolkit.Avalonia.Controls
{
    public partial class CircularProgressBar : UserControl
    {
        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(Value), 0.0);

        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(Minimum), 0.0);

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(Maximum), 100.0);

        public static readonly StyledProperty<double> SizeProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(Size), 100.0);

        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(StrokeThickness), 8.0);

        public static readonly StyledProperty<IBrush> ProgressBrushProperty =
            AvaloniaProperty.Register<CircularProgressBar, IBrush>(
                nameof(ProgressBrush),
                new SolidColorBrush(Color.Parse("#0078D4")));

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<CircularProgressBar, string>(nameof(Label), string.Empty);

        public static readonly StyledProperty<bool> ShowLabelProperty =
            AvaloniaProperty.Register<CircularProgressBar, bool>(nameof(ShowLabel), true);

        public static readonly StyledProperty<string> FormattedValueProperty =
            AvaloniaProperty.Register<CircularProgressBar, string>(nameof(FormattedValue), "0");

        public static readonly StyledProperty<double> ValueFontSizeProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(ValueFontSize), 20.0);

        public static readonly StyledProperty<double> LabelFontSizeProperty =
            AvaloniaProperty.Register<CircularProgressBar, double>(nameof(LabelFontSize), 10.0);

        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public double StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public IBrush ProgressBrush
        {
            get => GetValue(ProgressBrushProperty);
            set => SetValue(ProgressBrushProperty, value);
        }

        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public bool ShowLabel
        {
            get => GetValue(ShowLabelProperty);
            set => SetValue(ShowLabelProperty, value);
        }

        public string FormattedValue
        {
            get => GetValue(FormattedValueProperty);
            private set => SetValue(FormattedValueProperty, value);
        }

        public double ValueFontSize
        {
            get => GetValue(ValueFontSizeProperty);
            set => SetValue(ValueFontSizeProperty, value);
        }

        public double LabelFontSize
        {
            get => GetValue(LabelFontSizeProperty);
            set => SetValue(LabelFontSizeProperty, value);
        }

        public CircularProgressBar()
        {
            InitializeComponent();
            UpdateProgress();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty ||
                change.Property == MinimumProperty ||
                change.Property == MaximumProperty ||
                change.Property == SizeProperty ||
                change.Property == StrokeThicknessProperty)
            {
                UpdateProgress();
            }
        }

        private void UpdateProgress()
        {
            var progressPath = this.FindControl<Path>("PART_ProgressPath");
            if (progressPath == null)
                return;

            // Calculate percentage
            var range = Maximum - Minimum;
            var percentage = range > 0 ? (Value - Minimum) / range : 0;
            percentage = Math.Max(0, Math.Min(1, percentage));

            // Update formatted value
            FormattedValue = $"{(int)(percentage * 100)}%";

            // Calculate arc geometry
            var radius = (Size - StrokeThickness) / 2;
            var centerX = Size / 2;
            var centerY = Size / 2;

            if (percentage <= 0)
            {
                progressPath.Data = PathGeometry.Parse("");
                return;
            }

            var angle = percentage * 2 * Math.PI - Math.PI / 2; // Start from top
            var startAngle = -Math.PI / 2;

            var startX = centerX + radius * Math.Cos(startAngle);
            var startY = centerY + radius * Math.Sin(startAngle);

            var endX = centerX + radius * Math.Cos(angle);
            var endY = centerY + radius * Math.Sin(angle);

            var largeArc = percentage > 0.5 ? 1 : 0;

            // Create arc path
            var pathData = $"M {startX:F2},{startY:F2} " +
                          $"A {radius:F2},{radius:F2} 0 {largeArc} 1 {endX:F2},{endY:F2}";

            progressPath.Data = PathGeometry.Parse(pathData);

            // Update color based on value
            UpdateProgressColor(percentage);
        }

        private void UpdateProgressColor(double percentage)
        {
            if (percentage >= 0.8)
            {
                ProgressBrush = new SolidColorBrush(Color.Parse("#F44336")); // Red
            }
            else if (percentage >= 0.6)
            {
                ProgressBrush = new SolidColorBrush(Color.Parse("#FF9800")); // Orange
            }
            else if (percentage >= 0.4)
            {
                ProgressBrush = new SolidColorBrush(Color.Parse("#FFC107")); // Amber
            }
            else
            {
                ProgressBrush = new SolidColorBrush(Color.Parse("#4CAF50")); // Green
            }
        }
    }
}