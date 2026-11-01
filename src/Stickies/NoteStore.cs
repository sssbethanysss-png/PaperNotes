using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PaperNotes;

// Atomic write per note: a failed write never replaces the last good file.
public sealed class NoteStore
{
    public string DirectoryPath { get; }
    public List<string> Warnings { get; } = new();
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };
    public NoteStore(string directory) { DirectoryPath = directory; Directory.CreateDirectory(directory); }
    public void Save(Note note) => WriteJson(Path.Combine(DirectoryPath, note.Id + ".json"), note);
    public void WriteJson<T>(string path, T data)
    {
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, data, options);
            stream.Flush(true);
        }
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
        else File.Move(temp, path);
    }
    public List<Note> Load()
    {
        var notes = new List<Note>();
        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.json"))
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var id)) continue;
            try { notes.Add(Read(file, id)); }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
            {
                try
                {
                    var recovered = Read(file + ".bak", id);
                    File.Copy(file, file + ".corrupt-" + DateTime.UtcNow.Ticks);
                    File.Copy(file + ".bak", file, true);
                    notes.Add(recovered);
                    Warnings.Add(Loc.T("Recovery.NoteRestoredPrefix") + file);
                }
                catch (Exception backupEx) when (backupEx is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
                { Warnings.Add(Loc.T("Recovery.NoteReadFailedPrefix") + file); }
            }
        }
        return notes;
    }
    private static Note Read(string path, Guid expected)
    {
        var note = JsonSerializer.Deserialize<Note>(File.ReadAllText(path)) ?? throw new InvalidDataException();
        if (note.Id != expected || note.Content == null) throw new InvalidDataException();
        if (!Palette.Colors.ContainsKey(note.Color ?? "")) note.Color = "Yellow";
        note.CategoryId = string.IsNullOrWhiteSpace(note.CategoryId) ? "" : note.CategoryId.Trim();
        if (!double.IsFinite(note.X)) note.X = 160;
        if (!double.IsFinite(note.Y)) note.Y = 160;
        note.ExpandedWidth = double.IsFinite(note.ExpandedWidth) ? Math.Clamp(note.ExpandedWidth, 180, 4000) : 0;
        note.ExpandedHeight = double.IsFinite(note.ExpandedHeight) ? Math.Clamp(note.ExpandedHeight, Note.MinimumHeight, 4000) : 0;
        if (note.IsCollapsed)
        {
            if (note.ExpandedWidth < 180) note.ExpandedWidth = 300;
            if (note.ExpandedHeight < Note.MinimumHeight) note.ExpandedHeight = 300;
            note.Width = Note.CollapsedWidth;
            note.Height = Note.CollapsedHeight;
        }
        else
        {
            note.Width = double.IsFinite(note.Width) ? Math.Clamp(note.Width, 180, 4000) : 300;
            note.Height = double.IsFinite(note.Height) ? Math.Clamp(note.Height, Note.MinimumHeight, 4000) : 300;
            if (note.ExpandedWidth < 180) note.ExpandedWidth = note.Width;
            if (note.ExpandedHeight < Note.MinimumHeight) note.ExpandedHeight = note.Height;
        }
        return note;
    }
    public void Delete(Note note)
    {
        var path = Path.Combine(DirectoryPath, note.Id + ".json");
        // Delete .bak first so a mid-delete failure still leaves the active note.
        if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        File.Delete(path);
    }
}
