using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using FEHagemu.ViewModels;
using Ursa.Controls;

namespace FEHagemu.Views;

public partial class PersonSelectorView : UserControl
{
    public PersonSelectorView()
    {
        InitializeComponent();
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
            DialogHelper.CloseDialogWithOK(this);
        }
    }
}

/// <summary>
/// Shared helper to close Ursa Dialog/OverlayDialog from code-behind.
/// Works for both Window-based (Dialog.ShowModal) and overlay-based (OverlayDialog.ShowModal).
/// </summary>
internal static class DialogHelper
{
    public static void CloseDialogWithOK(Control source)
    {
        // Case 1: Window-based dialog (Dialog.ShowModal)
        var window = source.FindAncestorOfType<Window>();
        if (window is not null && window.GetType() != typeof(Window))
        {
            // The Ursa DefaultDialogWindow hosts an OK button; close with result
            window.Close(DialogResult.OK);
            return;
        }

        // Case 2: Overlay-based dialog (OverlayDialog.ShowModal)
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

        // Fallback: try closing any parent window
        window?.Close(DialogResult.OK);
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