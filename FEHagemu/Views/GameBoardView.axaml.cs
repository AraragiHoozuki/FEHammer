using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using FEHagemu.ViewModels;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace FEHagemu.Views;

public partial class GameBoardView : UserControl
{
    private Transitions? _savedCardTransitions;
    private bool _overlayOpen;

    private Grid _overlayRoot = null!;
    private Border _overlayMask = null!;
    private Border _overlayCard = null!;

    public GameBoardView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _overlayRoot = this.FindControl<Grid>("OverlayRoot")!;
        _overlayMask = this.FindControl<Border>("OverlayMask")!;
        _overlayCard = this.FindControl<Border>("OverlayCard")!;
        _savedCardTransitions = _overlayCard.Transitions;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is GameBoardViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(GameBoardViewModel.IsPopupOpen) && !vm.IsPopupOpen)
                {
                    CloseOverlay();
                }
            };
        }
    }

    private void Binding(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void OnCellPointerPressed(object? sender, PointerPressedEventArgs e)
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
                {
                    var clickPos = e.GetPosition(this);
                    OpenOverlay(clickPos.X, clickPos.Y);
                }
            }
        }
    }

    private void OnMaskPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var mainVm = this.DataContext as GameBoardViewModel;
        mainVm?.ClosePopup();
        CloseOverlay();
        e.Handled = true;
    }

    private async void OpenOverlay(double clickX, double clickY)
    {
        if (_overlayOpen)
            return; // already open, just update content
        _overlayOpen = true;

        // Disable transitions to set initial state instantly
        _overlayCard.Transitions = null;

        // Show overlay (need bounds)
        _overlayRoot.IsVisible = true;
        // Wait for layout
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        double centerX = _overlayRoot.Bounds.Width / 2;
        double centerY = _overlayRoot.Bounds.Height / 2;
        double dx = clickX - centerX;
        double dy = clickY - centerY;

        // Set initial state: small + offset to click point
        string startTf = string.Format(CultureInfo.InvariantCulture,
            "translate({0:F0}px,{1:F0}px) scale(0.05)", dx, dy);
        _overlayCard.RenderTransform = TransformOperations.Parse(startTf);
        _overlayCard.Opacity = 0;
        _overlayMask.Opacity = 0;

        // Wait one frame so initial state is rendered
        await Task.Delay(30);

        // Restore transitions and animate to final state
        _overlayCard.Transitions = _savedCardTransitions;
        _overlayCard.RenderTransform = TransformOperations.Parse("translate(0px,0px) scale(1)");
        _overlayCard.Opacity = 1;
        _overlayMask.Opacity = 0.55;
    }

    private async void CloseOverlay()
    {
        if (!_overlayOpen) return;
        _overlayOpen = false;

        _overlayCard.RenderTransform = TransformOperations.Parse("translate(0px,0px) scale(0.9)");
        _overlayCard.Opacity = 0;
        _overlayMask.Opacity = 0;

        await Task.Delay(350);

        if (!_overlayOpen)
            _overlayRoot.IsVisible = false;
    }
}