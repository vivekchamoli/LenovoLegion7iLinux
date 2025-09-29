using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace LenovoLegionToolkit.Avalonia.Controls
{
    public partial class NavigationMenu : UserControl
    {
        public static readonly StyledProperty<IEnumerable?> NavigationItemsProperty =
            AvaloniaProperty.Register<NavigationMenu, IEnumerable?>(nameof(NavigationItems));

        public static readonly StyledProperty<object?> SelectedItemProperty =
            AvaloniaProperty.Register<NavigationMenu, object?>(nameof(SelectedItem));

        public static readonly StyledProperty<ICommand?> NavigationCommandProperty =
            AvaloniaProperty.Register<NavigationMenu, ICommand?>(nameof(NavigationCommand));

        public IEnumerable? NavigationItems
        {
            get => GetValue(NavigationItemsProperty);
            set => SetValue(NavigationItemsProperty, value);
        }

        public object? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public ICommand? NavigationCommand
        {
            get => GetValue(NavigationCommandProperty);
            set => SetValue(NavigationCommandProperty, value);
        }

        public NavigationMenu()
        {
            InitializeComponent();
        }
    }
}