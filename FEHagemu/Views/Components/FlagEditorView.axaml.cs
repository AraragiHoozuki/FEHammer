using Avalonia;
using Avalonia.Controls;

namespace FEHagemu.Views.Components
{
    public partial class FlagEditorView : UserControl
    {
        public static readonly StyledProperty<bool> IsEditableProperty =
            AvaloniaProperty.Register<FlagEditorView, bool>(nameof(IsEditable), true);

        public bool IsEditable
        {
            get => GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }

        public FlagEditorView()
        {
            InitializeComponent();
        }
    }
}
