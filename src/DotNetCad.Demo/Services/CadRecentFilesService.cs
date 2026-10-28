using System.IO;
using System.Text.Json;

namespace DotNetCad.Demo.Services;

public class CadRecentFilesService
{
    private readonly string _storagePath;
    private readonly List<string> _recentFiles = [];
    private const int MaxRecentFiles = 10;

    public static string DefaultUserPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotNetCadEngine", "recent-files.json");

    public CadRecentFilesService(string? storagePath = null)
    {
        _storagePath = storagePath ?? DefaultUserPath;
        Load();
    }

    public IReadOnlyList<string> RecentFiles
    {
        get
        {
            lock (_recentFiles)
            {
                return _recentFiles.ToList();
            }
        }
    }

    public void Load()
    {
        lock (_recentFiles)
        {
            _recentFiles.Clear();
            if (!File.Exists(_storagePath)) return;

            try
            {
                var json = File.ReadAllText(_storagePath);
                var items = JsonSerializer.Deserialize<List<string>>(json);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (!string.IsNullOrWhiteSpace(item) && !_recentFiles.Any(r => string.Equals(r, item, StringComparison.OrdinalIgnoreCase)))
                        {
                            _recentFiles.Add(item);
                            if (_recentFiles.Count >= MaxRecentFiles) break;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Graceful fallback on corrupt or inaccessible file
            }
        }
    }

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        lock (_recentFiles)
        {
            var fullPath = Path.GetFullPath(path);
            _recentFiles.RemoveAll(r => string.Equals(r, fullPath, StringComparison.OrdinalIgnoreCase));
            _recentFiles.Insert(0, fullPath);

            while (_recentFiles.Count > MaxRecentFiles)
            {
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
            }

            Save();
        }
    }

    public bool Remove(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        lock (_recentFiles)
        {
            int removed = _recentFiles.RemoveAll(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                Save();
                return true;
            }
            return false;
        }
    }

    public void Clear()
    {
        lock (_recentFiles)
        {
            _recentFiles.Clear();
            Save();
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var temp = _storagePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var json = JsonSerializer.Serialize(_recentFiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temp, json);
            File.Move(temp, _storagePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Graceful fallback
        }
    }
}
