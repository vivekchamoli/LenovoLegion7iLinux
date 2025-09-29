using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LenovoLegionToolkit.Avalonia.Controls
{
    public partial class FeatureCardControl : UserControl
    {
        public static readonly StyledProperty<string> IconProperty =
            AvaloniaProperty.Register<FeatureCardControl, string>(nameof(Icon), "📦");

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<FeatureCardControl, string>(nameof(Title), "Feature");

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<FeatureCardControl, string>(nameof(Description), string.Empty);

        public static readonly StyledProperty<object?> ActionContentProperty =
            AvaloniaProperty.Register<FeatureCardControl, object?>(nameof(ActionContent));

        public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
            RoutedEvent.Register<FeatureCardControl, RoutedEventArgs>(
                nameof(Click),
                RoutingStrategies.Bubble);

        public string Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public object? ActionContent
        {
            get => GetValue(ActionContentProperty);
            set => SetValue(ActionContentProperty, value);
        }

        public event EventHandler<RoutedEventArgs>? Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        public FeatureCardControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            RaiseEvent(new RoutedEventArgs(ClickEvent));
        }
    }
}