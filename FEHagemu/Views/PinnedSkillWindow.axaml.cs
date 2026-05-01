using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FEHagemu.ViewModels;

namespace FEHagemu.Views;

public partial class PinnedSkillWindow : Window
{
    /// <summary>
    /// Reference to the parent SkillSelectorViewModel for navigation
    /// </summary>
    public SkillSelectorViewModel? SelectorVM { get; set; }

    public PinnedSkillWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Clicking a chain item switches this window to show that skill
    /// </summary>
    private void OnChainItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string skillId)
        {
            var newSvm = new SkillViewModel(skillId, 0);
            if (newSvm.skill is not null)
            {
                SelectorVM?.NavigateToSkill(skillId);
                DataContext = newSvm;
            }
        }
    }

    /// <summary>
    /// Allow dragging the window by pressing on the header area
    /// </summary>
    private void OnDragAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
