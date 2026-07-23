using Avalonia;
using Avalonia.Controls;

namespace FEHagemu.Views;

public partial class SkillDetailsView : UserControl
{
    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<SkillDetailsView, bool>(nameof(IsEditing));

    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    public SkillDetailsView()
    {
        InitializeComponent();
    }
}
