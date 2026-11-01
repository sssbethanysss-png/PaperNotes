using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PaperNotes;

public static class TxtTransfer
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public static string Read(string path)
    {
        // Strict UTF-8; BOM detection may accept UTF-16/32. No lossy encoding guesses.
        using var reader = new StreamReader(path, Utf8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
    public static string FileStem(Note note)
    {
        string firstLine = note.Content.Split(new[] { '\r', '\n' }, 2)[0].Trim();
        if (string.IsNullOrEmpty(firstLine)) return "Untitled Note";
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(firstLine.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().TrimEnd('.', ' ');
        if (name.Length > 80) name = name[..80];
        if (name.Length > 0 && char.IsHighSurrogate(name[^1])) name = name[..^1];
        name = name.TrimEnd('.', ' ');
        if (name.Length == 0) name = "Untitled Note";
        if (Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)", RegexOptions.IgnoreCase)) name = "_" + name;
        return name;
    }
    public static void ExportSingle(Note note, string path)
    {
        // Write temp then replace so a failed export never leaves a partial destination file.
        string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, ".papernotes-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            WriteNew(temp, note.Content);
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static string ExportUnique(Note note, string directory)
    {
        string stem = FileStem(note);
        byte[] bytes = Utf8.GetBytes(note.Content);
        for (int suffix = 1; ; suffix++)
        {
            string path = Path.Combine(directory, stem + (suffix == 1 ? "" : $" ({suffix})") + ".txt");
            FileStream stream;
            try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough); }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && (File.Exists(path) || Directory.Exists(path))) { continue; }
            try { using (stream) { stream.Write(bytes); stream.Flush(true); } return path; }
            catch { if (File.Exists(path)) File.Delete(path); throw; }
        }
    }
    private static void WriteNew(string path, string content)
    {
        bool created = false;
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            created = true;
            byte[] bytes = Utf8.GetBytes(content); stream.Write(bytes); stream.Flush(true);
        }
        catch
        {
            if (created && File.Exists(path)) File.Delete(path);
            throw;
        }
    }
}
