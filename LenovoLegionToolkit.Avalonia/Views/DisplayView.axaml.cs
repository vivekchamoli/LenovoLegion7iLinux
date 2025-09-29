using Avalonia.Controls;
using Avalonia.ReactiveUI;
using LenovoLegionToolkit.Avalonia.ViewModels;

namespace LenovoLegionToolkit.Avalonia.Views
{
    public partial class DisplayView : ReactiveUserControl<DisplayViewModel>
    {
        public DisplayView()
        {
            InitializeComponent();
        }
    }
}