using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using FEHagemu.ViewModels;
using System.Collections.Generic;
using Ursa.Controls;

namespace FEHagemu.Views;

public partial class SkillSelectorView : UserControl
{
    private readonly List<PinnedSkillWindow> _pinnedWindows = [];

    public SkillSelectorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SkillSelectorViewModel vm)
        {
            vm.OnSkillPinRequested = OpenPinnedWindow;
        }
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var count = _pinnedWindows.Count;
        // Close all pinned windows when the selector is closed
        for(int i = count -1; i >=0; i--)
        {
            var window = _pinnedWindows[i];
            try { window.Close(); } catch { }
        }
        //foreach (var w in _pinnedWindows)
        //{
        //    try { w.Close(); } catch { }
        //}
        _pinnedWindows.Clear();
    }

    private void OpenPinnedWindow(SkillViewModel svm)
    {
        var parentWindow = this.FindAncestorOfType<Window>();
        var win = new PinnedSkillWindow
        {
            DataContext = svm,
            SelectorVM = DataContext as SkillSelectorViewModel,
        };
        win.Closed += (_, _) => _pinnedWindows.Remove(win);
        _pinnedWindows.Add(win);
        if (parentWindow is not null)
            win.Show(parentWindow);
        else
            win.Show();
    }

    private void OnSkillItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is SkillViewModel svm
            && DataContext is SkillSelectorViewModel vm)
        {
            vm.SelectedSkill = svm;
            DialogHelper.CloseDialogWithOK(this);
        }
    }

    private void OnSkillIconTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is SkillViewModel svm
            && DataContext is SkillSelectorViewModel vm)
        {
            vm.SelectedSkill = svm;
        }
    }
}