using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using LenovoLegionToolkit.Avalonia.ViewModels;
using LenovoLegionToolkit.Avalonia.Views;

namespace LenovoLegionToolkit.Avalonia
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? data)
        {
            if (data is null)
                return null;

            var name = data.GetType().Name;

            // Direct ViewModel to View mapping
            return name switch
            {
                nameof(MainViewModel) => new MainWindow(),
                nameof(DashboardViewModel) => new DashboardView(),
                nameof(SettingsViewModel) => new SettingsView(),
                nameof(PowerViewModel) => new PowerView(),
                nameof(BatteryViewModel) => new BatteryView(),
                nameof(ThermalViewModel) => new ThermalView(),
                nameof(KeyboardViewModel) => new KeyboardView(),
                nameof(DisplayViewModel) => new DisplayView(),
                nameof(AutomationViewModel) => new AutomationView(),
                // Add more mappings as views are created
                // nameof(AboutViewModel) => new AboutView(),
                _ => CreateFallbackView(name)
            };
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }

        private Control CreateFallbackView(string viewModelName)
        {
            return new Border
            {
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"View not found for {viewModelName}",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "This view is not yet implemented.",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontSize = 12,
                            Opacity = 0.6,
                            Margin = new Thickness(0, 10, 0, 0)
                        }
                    },
                    Spacing = 5,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
        }
    }
}