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
    private const double SelectionViewWidth = 560;
    private const double SelectionViewHeight = 580;

    private readonly List<PinnedSkillWindow> _pinnedWindows = [];
    public static readonly StyledProperty<bool> CloseOnDoubleTapProperty =
        AvaloniaProperty.Register<SkillSelectorView, bool>(nameof(CloseOnDoubleTap), true);
    public static readonly StyledProperty<bool> IsSelectionModeProperty =
        AvaloniaProperty.Register<SkillSelectorView, bool>(nameof(IsSelectionMode), true);

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

    public SkillSelectorView()
    {
        InitializeComponent();
        UpdateSelectionModeLayout();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    public static SkillSelectorView CreateSelectionView()
    {
        var view = new SkillSelectorView
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
            if (CloseOnDoubleTap)
                DialogHelper.CloseDialogWithOK(this);
            else
                vm.IsEditMode = true;
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
