using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FEHagemu.Services
{
    public class FieldNoteItem
    {
        public string Value { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
    }

    public class FieldNoteService
    {
        public static FieldNoteService Instance { get; } = new FieldNoteService();

        private Dictionary<string, List<FieldNoteItem>> _notes = new Dictionary<string, List<FieldNoteItem>>();
        private readonly string _filePath;

        public event Action<string>? NoteChanged;

        private FieldNoteService()
        {
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SkillEditorFieldNotes.json");
            Load();
        }

        public void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    _notes = JsonSerializer.Deserialize<Dictionary<string, List<FieldNoteItem>>>(json) ?? new Dictionary<string, List<FieldNoteItem>>();
                }
                catch { }
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_notes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        public bool HasNotes(string key) => _notes.ContainsKey(key) && _notes[key] != null && _notes[key].Any();
        
        public List<FieldNoteItem> GetNotes(string key) 
        {
            if (_notes.TryGetValue(key, out var list) && list != null)
            {
                return list.Select(x => new FieldNoteItem { Value = x.Value, Meaning = x.Meaning }).ToList();
            }
            return new List<FieldNoteItem>();
        }

        public void SetNotes(string key, List<FieldNoteItem> notes)
        {
            if (notes == null || notes.Count == 0)
            {
                _notes.Remove(key);
            }
            else
            {
                _notes[key] = notes.Where(x => !string.IsNullOrWhiteSpace(x.Value) || !string.IsNullOrWhiteSpace(x.Meaning)).ToList();
            }
            Save();
            NoteChanged?.Invoke(key);
        }
    }
}
