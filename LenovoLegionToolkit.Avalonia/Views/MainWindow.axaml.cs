using Avalonia.Controls;
using System;

namespace LenovoLegionToolkit.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Ensure window is visible on startup
            this.WindowState = WindowState.Normal;
            this.ShowInTaskbar = true;
            this.CanResize = true;

            // Force show the window
            this.Opened += MainWindow_Opened;
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            // Make absolutely sure the window is visible
            this.Show();
            this.Activate();
            this.Focus();
        }
    }
}
