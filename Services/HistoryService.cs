using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using ConversionApp.Models;

namespace ConversionApp.Services;

public class HistoryService
{
    private static readonly string HistoryFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EezyPro", "history.json");

    private static HistoryService? _instance;
    public static HistoryService Instance => _instance ??= new HistoryService();

    public ObservableCollection<ConversionHistoryItem> Items { get; } = new();

    private HistoryService()
    {
        LoadHistory();
    }

    public void AddItem(string filePath, string type, string glyph)
    {
        // Ensure UI updates happen on the UI thread if possible
        var item = new ConversionHistoryItem
        {
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            ConversionType = type,
            Timestamp = DateTime.Now,
            IconGlyph = glyph
        };

        // We assume this is called on UI thread for now as it's called from ViewModel callbacks
        // that are already marshaled.
        
        var existing = Items.FirstOrDefault(i => i.FilePath == filePath);
        if (existing != null) Items.Remove(existing);

        Items.Insert(0, item);

        while (Items.Count > 10) Items.RemoveAt(Items.Count - 1);

        SaveHistory();
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFilePath)) return;

            var json = File.ReadAllText(HistoryFilePath);
            var items = JsonSerializer.Deserialize<List<ConversionHistoryItem>>(json);

            if (items != null)
            {
                Items.Clear();
                foreach (var item in items) Items.Add(item);
            }
        }
        catch { /* Ignore load errors */ }
    }

    private void SaveHistory()
    {
        try
        {
            var dir = Path.GetDirectoryName(HistoryFilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

            var json = JsonSerializer.Serialize(Items.ToList());
            File.WriteAllText(HistoryFilePath, json);
        }
        catch { /* Ignore save errors */ }
    }
}
