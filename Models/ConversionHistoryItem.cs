using System;

namespace ConversionApp.Models;

public class ConversionHistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ConversionType { get; set; } = string.Empty; // e.g., "Word to PDF"
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string IconGlyph { get; set; } = "&#xE8A5;";
}
