using Avalonia.Controls;
using Avalonia.Interactivity;
using FEHagemu.Services;
using System;
using System.Collections.ObjectModel;

namespace FEHagemu.Views.Tools
{
    public partial class FieldNoteEditorWindow : Window
    {
        private string _key = string.Empty;
        public ObservableCollection<FieldNoteItem> Notes { get; set; } = new ObservableCollection<FieldNoteItem>();

        public FieldNoteEditorWindow()
        {
            InitializeComponent();
        }

        public FieldNoteEditorWindow(string key) : this()
        {
            _key = key;
            var title = this.FindControl<TextBlock>("TitleText")
                ?? throw new InvalidOperationException("TitleText control was not found.");
            title.Text = $"编辑条目 - {key}";
            
            var existing = FieldNoteService.Instance.GetNotes(key);
            foreach (var note in existing)
            {
                Notes.Add(note);
            }
            if (Notes.Count == 0)
            {
                Notes.Add(new FieldNoteItem());
            }

            var itemsControl = this.FindControl<ItemsControl>("NotesItemsControl")
                ?? throw new InvalidOperationException("NotesItemsControl was not found.");
            itemsControl.ItemsSource = Notes;
        }

        private void OnAddItemClick(object sender, RoutedEventArgs e)
        {
            Notes.Add(new FieldNoteItem());
        }

        private void OnMoveUpClick(object sender, RoutedEventArgs e)
        {
            if (sender is Control control && control.Tag is FieldNoteItem item)
            {
                int index = Notes.IndexOf(item);
                if (index > 0)
                {
                    Notes.Move(index, index - 1);
                }
            }
        }

        private void OnMoveDownClick(object sender, RoutedEventArgs e)
        {
            if (sender is Control control && control.Tag is FieldNoteItem item)
            {
                int index = Notes.IndexOf(item);
                if (index >= 0 && index < Notes.Count - 1)
                {
                    Notes.Move(index, index + 1);
                }
            }
        }

        private void OnRemoveItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is Control control && control.Tag is FieldNoteItem item)
            {
                Notes.Remove(item);
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            FieldNoteService.Instance.SetNotes(_key, new System.Collections.Generic.List<FieldNoteItem>(Notes));
            Close();
        }
    }
}
