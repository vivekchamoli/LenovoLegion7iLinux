using Avalonia.Controls;
using Avalonia.ReactiveUI;
using LenovoLegionToolkit.Avalonia.ViewModels;

namespace LenovoLegionToolkit.Avalonia.Views
{
    public partial class PowerView : ReactiveUserControl<PowerViewModel>
    {
        public PowerView()
        {
            InitializeComponent();
        }
    }
}