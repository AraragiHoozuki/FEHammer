using Avalonia.Controls;
using Avalonia.Input;
using FEHagemu.ViewModels;
using System.Threading.Tasks;
using Ursa.Controls;

namespace FEHagemu.Views;

public partial class GameBoardView : UserControl
{
    private bool unitEditorOpen;

    public GameBoardView()
    {
        InitializeComponent();
    }

    private async void OnCellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var control = sender as Control;
        if (control?.DataContext is BoardCellViewModel cellVm)
        {
            var mainVm = this.DataContext as GameBoardViewModel;
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                if (mainVm?.PasteUnitCommand.CanExecute(cellVm) == true)
                    mainVm.PasteUnitCommand.Execute(cellVm);
                return;
            }

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                mainVm?.SelectCell(cellVm);

                if (e.ClickCount == 2)
                    await OpenUnitEditorAsync(mainVm);
            }
        }
    }

    private async Task OpenUnitEditorAsync(GameBoardViewModel? viewModel)
    {
        if (viewModel is null || unitEditorOpen) return;

        unitEditorOpen = true;
        try
        {
            await Dialog.ShowModal(
                new MapCellUnitEditorView(),
                viewModel,
                null,
                new DialogOptions
                {
                    Title = "格子 Unit 编辑",
                    CanResize = true,
                    StartupLocation = WindowStartupLocation.CenterScreen,
                    Button = DialogButton.None
                });
        }
        finally
        {
            unitEditorOpen = false;
            viewModel.SelectedCell?.CallFirstPersonChange();
        }
    }
}
