using System.IO;
using System.Text.Json;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Infrastructure.Native;

namespace DotNetCad.Demo.Services;

public enum RecoveryChoice
{
    Recover,
    Discard,
    Later
}

public sealed class RecoverySnapshot
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string? NativePath { get; set; }
    public string DrawingJson { get; set; } = string.Empty;
    public string SnapshotFilePath { get; set; } = string.Empty;
}

public class CadRecoveryService
{
    private readonly string _recoveryDirectory;
    public string SessionId { get; }

    public static string DefaultRecoveryDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotNetCadEngine", "Recovery");

    public CadRecoveryService(string? recoveryDirectory = null, string? sessionId = null)
    {
        _recoveryDirectory = recoveryDirectory ?? DefaultRecoveryDirectory;
        SessionId = sessionId ?? Guid.NewGuid().ToString("N");
    }

    public string ActiveSnapshotPath => Path.Combine(_recoveryDirectory, $"{SessionId}.cad-recovery");

    public bool SaveSnapshot(CadDocument doc, string? nativePath)
    {
        try
        {
            Directory.CreateDirectory(_recoveryDirectory);
            var snapshot = new RecoverySnapshot
            {
                SessionId = SessionId,
                Timestamp = DateTime.UtcNow,
                DocumentTitle = doc.Title,
                NativePath = nativePath,
                DrawingJson = CadDrawingSerializer.Serialize(doc)
            };

            var tempFile = ActiveSnapshotPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, ActiveSnapshotPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    public void DeleteSnapshot()
    {
        try
        {
            if (File.Exists(ActiveSnapshotPath))
            {
                File.Delete(ActiveSnapshotPath);
            }
        }
        catch (Exception)
        {
            // Ignore cleanup errors
        }
    }

    public List<RecoverySnapshot> FindUnsavedSnapshots()
    {
        var result = new List<RecoverySnapshot>();
        if (!Directory.Exists(_recoveryDirectory)) return result;

        try
        {
            var files = Directory.GetFiles(_recoveryDirectory, "*.cad-recovery");
            foreach (var file in files)
            {
                // Don't treat the active session as a crashed session
                if (Path.GetFileName(file).Equals($"{SessionId}.cad-recovery", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var json = File.ReadAllText(file);
                    var snapshot = JsonSerializer.Deserialize<RecoverySnapshot>(json);
                    if (snapshot != null)
                    {
                        snapshot.SnapshotFilePath = file;
                        result.Add(snapshot);
                    }
                }
                catch (Exception)
                {
                    // Ignore damaged files
                }
            }
        }
        catch (Exception)
        {
            // Ignore directory listing failures
        }

        return result;
    }

    public bool DiscardSnapshot(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
        }
        catch (Exception)
        {
        }
        return false;
    }
}
