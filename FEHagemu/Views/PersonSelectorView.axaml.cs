using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FEHagemu.ViewModels;
using Ursa.Controls;

namespace FEHagemu.Views;

public partial class PersonSelectorView : UserControl
{
    private const double SelectionViewWidth = 560;
    private const double SelectionViewHeight = 580;

    public static readonly StyledProperty<bool> CloseOnDoubleTapProperty =
        AvaloniaProperty.Register<PersonSelectorView, bool>(nameof(CloseOnDoubleTap), true);
    public static readonly StyledProperty<bool> IsSelectionModeProperty =
        AvaloniaProperty.Register<PersonSelectorView, bool>(nameof(IsSelectionMode), true);

    public bool CloseOnDoubleTap
    {
        get => GetValue(CloseOnDoubleTapProperty);
        set => SetValue(CloseOnDoubleTapProperty, value);
    }

    public bool IsSelectionMode
    {
        get => GetValue(IsSelectionModeProperty);
        set => SetValue(IsSelectionModeProperty, value);
    }

    public PersonSelectorView()
    {
        InitializeComponent();
        UpdateSelectionModeLayout();
    }

    public static PersonSelectorView CreateSelectionView()
    {
        var view = new PersonSelectorView
        {
            IsSelectionMode = true
        };
        DialogHelper.SetResponsiveInitialSize(view, SelectionViewWidth, SelectionViewHeight);
        return view;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectionModeProperty)
            UpdateSelectionModeLayout();
    }

    private void UpdateSelectionModeLayout()
    {
        if (this.FindControl<Grid>("LayoutRoot") is not { } layout) return;

        layout.ColumnDefinitions[0].Width = new GridLength(
            IsSelectionMode ? 1 : 2,
            GridUnitType.Star);
        layout.ColumnDefinitions[1].Width = new GridLength(
            IsSelectionMode ? 0 : 6,
            GridUnitType.Pixel);
        layout.ColumnDefinitions[2].Width = new GridLength(
            IsSelectionMode ? 0 : 3,
            GridUnitType.Star);
    }

    private void OnPersonCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Panel panel && panel.DataContext is PersonViewModel pvm
            && DataContext is PersonSelectorViewModel vm)
        {
            vm.SelectedPerson = pvm;
        }
    }

    private void OnPersonCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Panel panel && panel.DataContext is PersonViewModel pvm
            && DataContext is PersonSelectorViewModel vm)
        {
            vm.SelectedPerson = pvm;
            if (CloseOnDoubleTap)
                DialogHelper.CloseDialogWithOK(this);
            else
                vm.IsEditMode = true;
        }
    }
}

/// <summary>
/// Shared helper to close Ursa Dialog/OverlayDialog from code-behind.
/// Works for both Window-based (Dialog.ShowModal) and overlay-based (OverlayDialog.ShowModal).
/// </summary>
internal static class DialogHelper
{
    public static void SetResponsiveInitialSize(Control content, double width, double height)
    {
        content.Width = width;
        content.Height = height;

        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            content.Loaded -= OnLoaded;
            Dispatcher.UIThread.Post(() =>
            {
                Window? window = content.FindAncestorOfType<Window>();
                if (window is not null && window.GetType().Name.Contains("DialogWindow"))
                {
                    double actualWidth = window.Bounds.Width > 0 ? window.Bounds.Width : width;
                    double actualHeight = window.Bounds.Height > 0 ? window.Bounds.Height : height;
                    window.SizeToContent = SizeToContent.Manual;
                    window.Width = actualWidth;
                    window.Height = actualHeight;
                }
                else if (FindAncestorByTypeName(content, "OverlayDialogControl") is Control overlay)
                {
                    overlay.Width = overlay.Bounds.Width > 0 ? overlay.Bounds.Width : width;
                    overlay.Height = overlay.Bounds.Height > 0 ? overlay.Bounds.Height : height;
                }

                content.ClearValue(Control.WidthProperty);
                content.ClearValue(Control.HeightProperty);
            }, DispatcherPriority.Background);
        }

        content.Loaded += OnLoaded;
    }

    public static void CloseDialogWithOK(Control source)
    {
        // Case 1: Try overlay or dialog control first
        // Find the OK button in the dialog's button panel and programmatically click it
        var dialogControl = FindAncestorByTypeName(source, "DialogControl")
                         ?? FindAncestorByTypeName(source, "OverlayDialogControl");
        if (dialogControl is ContentControl cc)
        {
            // Walk the dialog control tree to find OK button
            var okButton = FindButtonByContent(cc, "OK")
                        ?? FindButtonByContent(cc, "确定");
            if (okButton is not null)
            {
                okButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return;
            }
        }

        // Case 2: Window-based dialog (Dialog.ShowModal) fallback
        var window = source.FindAncestorOfType<Window>();
        if (window is not null && window.GetType().Name.Contains("DialogWindow"))
        {
            // The Ursa DefaultDialogWindow hosts an OK button; close with result
            window.Close(DialogResult.OK);
            return;
        }

        // Fallback: try closing any parent window if it's not the main Window
        if (window is not null && window.GetType() != typeof(Window) && !window.GetType().Name.Contains("EditorWindow"))
        {
            window.Close(DialogResult.OK);
        }
    }

    private static Control? FindAncestorByTypeName(Control source, string typeName)
    {
        var current = source.GetVisualParent();
        while (current is not null)
        {
            if (current.GetType().Name.Contains(typeName))
                return current as Control;
            current = current.GetVisualParent();
        }
        return null;
    }

    private static Button? FindButtonByContent(Visual root, string content)
    {
        foreach (var child in root.GetVisualDescendants())
        {
            if (child is Button btn)
            {
                var btnContent = btn.Content?.ToString();
                if (btnContent == content)
                    return btn;
            }
        }
        return null;
    }
}
