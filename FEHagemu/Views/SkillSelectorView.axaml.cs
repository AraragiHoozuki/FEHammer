using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FEHagemu.ViewModels;

namespace FEHagemu.Views;

public partial class SkillSelectorView : UserControl
{
    public SkillSelectorView()
    {
        InitializeComponent();
    }

    void WeaponFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Some logic here
        if (DataContext is SkillSelectorViewModel vm)
        {
            vm.DoSearch();
        }
    }
}