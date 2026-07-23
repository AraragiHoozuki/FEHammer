using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FEHagemu.ViewModels;

namespace FEHagemu.Views;

public partial class BoardUnitView : UserControl
{
    public static readonly StyledProperty<BoardCellViewModel?> CellProperty =
        AvaloniaProperty.Register<BoardUnitView, BoardCellViewModel?>(nameof(Cell));

    public BoardCellViewModel? Cell
    {
        get => GetValue(CellProperty);
        set => SetValue(CellProperty, value);
    }

    public BoardUnitView()
    {
        InitializeComponent();
    }
}
