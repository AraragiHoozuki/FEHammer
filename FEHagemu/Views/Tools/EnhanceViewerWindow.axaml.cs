using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FEHagemu.ViewModels.Tools;

namespace FEHagemu.Views.Tools
{
    public partial class EnhanceViewerWindow : Window
    {
        public EnhanceViewerWindow()
        {
            InitializeComponent();
        }

        private void OnItemPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is EnhanceItemViewModel item)
            {
                item.IsExpanded = !item.IsExpanded;
            }
        }
    }
}
