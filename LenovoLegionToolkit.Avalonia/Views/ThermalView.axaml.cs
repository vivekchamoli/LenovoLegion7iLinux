using Avalonia.Controls;
using Avalonia.ReactiveUI;
using LenovoLegionToolkit.Avalonia.ViewModels;

namespace LenovoLegionToolkit.Avalonia.Views
{
    public partial class ThermalView : ReactiveUserControl<ThermalViewModel>
    {
        public ThermalView()
        {
            InitializeComponent();
        }
    }
}