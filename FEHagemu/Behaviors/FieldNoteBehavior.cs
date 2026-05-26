using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FEHagemu.Services;
using FEHagemu.Views.Tools;
using System.Collections.Generic;
using System.Linq;

namespace FEHagemu.Behaviors
{
    public class FieldNoteBehavior
    {
        public static readonly AttachedProperty<string> KeyProperty =
            AvaloniaProperty.RegisterAttached<FieldNoteBehavior, Control, string>("Key");

        public static string GetKey(Control element) => element.GetValue(KeyProperty);
        public static void SetKey(Control element, string value) => element.SetValue(KeyProperty, value);

        static FieldNoteBehavior()
        {
            KeyProperty.Changed.AddClassHandler<Control>(OnKeyChanged);
        }

        private static void OnKeyChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            var key = e.NewValue as string;
            if (string.IsNullOrEmpty(key))
            {
                control.ContextMenu = null;
                return;
            }

            UpdateContextMenu(control, key);
            
            // Subscribe to note changes
            FieldNoteService.Instance.NoteChanged += (k) => 
            {
                if (k == key)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateContextMenu(control, key));
                }
            };
        }

        private static void UpdateContextMenu(Control control, string key)
        {
            var menu = new ContextMenu();
            var items = new List<Control>();

            bool hasNotes = FieldNoteService.Instance.HasNotes(key);

            var editItem = new MenuItem 
            { 
                Header = hasNotes ? "📝 编辑条目说明..." : "📝 添加条目说明...",
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            };
            editItem.Click += (s, e) => OpenEditor(key);
            items.Add(editItem);

            if (hasNotes)
            {
                items.Add(new Separator());

                var notes = FieldNoteService.Instance.GetNotes(key);
                
                // Create a beautiful unified layout for all items
                var infoContainer = new Border
                {
                    Background = GetBrush(control, "SemiColorBackground1", "#10888888"),
                    CornerRadius = new Avalonia.CornerRadius(6),
                    Padding = new Avalonia.Thickness(12, 10),
                    Margin = new Avalonia.Thickness(2)
                };

                var stackPanel = new StackPanel { Spacing = 10 };

                foreach (var note in notes)
                {
                    if (string.IsNullOrWhiteSpace(note.Value) && string.IsNullOrWhiteSpace(note.Meaning))
                        continue;

                    var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                    
                    if (!string.IsNullOrWhiteSpace(note.Value))
                    {
                        var valueBorder = new Border 
                        {
                            Background = GetBrush(control, "SemiColorPrimaryLight", "#3355AAFF"),
                            CornerRadius = new Avalonia.CornerRadius(4),
                            Padding = new Avalonia.Thickness(6, 2),
                            Margin = new Avalonia.Thickness(0, 0, 12, 0),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
                        };
                        var valueText = new TextBlock 
                        { 
                            Text = note.Value, 
                            FontWeight = Avalonia.Media.FontWeight.Bold,
                            Foreground = GetBrush(control, "SemiColorPrimary", "#55AAFF"),
                            FontSize = 12,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        };
                        valueBorder.Child = valueText;
                        Grid.SetColumn(valueBorder, 0);
                        row.Children.Add(valueBorder);
                    }

                    var meaningText = new TextBlock 
                    { 
                        Text = note.Meaning,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        MaxWidth = 300,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                        FontSize = 13,
                        Foreground = GetBrush(control, "SemiColorText0", "#EEEEEE"),
                        LineHeight = 18
                    };
                    Grid.SetColumn(meaningText, 1);
                    row.Children.Add(meaningText);

                    stackPanel.Children.Add(row);
                }
                
                infoContainer.Child = stackPanel;

                var infoMenuItem = new MenuItem
                {
                    Header = infoContainer,
                    Focusable = false,
                    IsHitTestVisible = false
                };

                items.Add(infoMenuItem);
                
                // Add a tooltip
                var tooltipText = string.Join("\n", notes.Select(n => $"[{n.Value}] {n.Meaning}"));
                ToolTip.SetTip(control, tooltipText);
            }
            else
            {
                ToolTip.SetTip(control, null);
            }

            menu.ItemsSource = items;
            control.ContextMenu = menu;
        }

        private static Avalonia.Media.IBrush GetBrush(Control control, string resourceKey, string fallbackHex)
        {
            if (control.TryFindResource(resourceKey, out var res) && res is Avalonia.Media.IBrush brush)
            {
                return brush;
            }
            return Avalonia.Media.SolidColorBrush.Parse(fallbackHex);
        }

        private static void OpenEditor(string key)
        {
            var window = new FieldNoteEditorWindow(key);
            window.Show();
        }
    }
}
