using Avalonia.Controls;
using Avalonia.ReactiveUI;
using LenovoLegionToolkit.Avalonia.ViewModels;

namespace LenovoLegionToolkit.Avalonia.Views
{
    public partial class KeyboardView : ReactiveUserControl<KeyboardViewModel>
    {
        public KeyboardView()
        {
            InitializeComponent();
        }
    }
}