using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FEHagemu.ViewModels;

namespace FEHagemu.Views;

public partial class GameBoardView : UserControl
{
    public GameBoardView()
    {
        InitializeComponent();
    }

    private void Binding(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void OnCellPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var control = sender as Control;
        if (control?.DataContext is BoardCellViewModel cellVm)
        {
            var mainVm = this.DataContext as GameBoardViewModel;
            mainVm?.SelectCell(cellVm);
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                if (mainVm?.PasteUnitCommand.CanExecute(cellVm) == true)
                {
                    mainVm.PasteUnitCommand.Execute(cellVm);
                }
            }
        }
    }
}