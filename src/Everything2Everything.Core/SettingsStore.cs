using System.Security.Cryptography;
using System.Text.Json;

namespace Everything2Everything.Core;

/// <summary>키-값 설정 저장소. API 키·외부 도구 경로 등 민감 정보를 영속화한다.</summary>
public interface ISettingsStore
{
    string? Get(string key);
    void Set(string key, string value);
    void Remove(string key);
    bool Contains(string key);
}

/// <summary>
/// DPAPI(CurrentUser)로 암호화해 %LOCALAPPDATA%\Everything2Everything\settings.dat 에 저장한다.
/// 현재 사용자 계정에서만 복호화 가능하므로 API 키 저장에 적합하다. Windows 전용.
/// </summary>
public sealed class DpapiSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly Dictionary<string, string> _cache;
    private readonly object _lock = new();

    public DpapiSettingsStore(string? filePath = null)
    {
        _path = filePath ?? DefaultPath();
        _cache = Load(_path);
    }

    public static string DefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Everything2Everything");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.dat");
    }

    public string? Get(string key)
    {
        lock (_lock) return _cache.TryGetValue(key, out var v) ? v : null;
    }

    public bool Contains(string key)
    {
        lock (_lock) return _cache.ContainsKey(key);
    }

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            _cache[key] = value;
            Save();
        }
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            if (_cache.Remove(key)) Save();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(_cache);
        var encrypted = ProtectedData.Protect(json, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var tmp = _path + ".tmp";
        File.WriteAllBytes(tmp, encrypted);
        if (File.Exists(_path)) File.Delete(_path);
        File.Move(tmp, _path);
    }

    private static Dictionary<string, string> Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new(StringComparer.Ordinal);
            var encrypted = File.ReadAllBytes(path);
            var json = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(StringComparer.Ordinal);
        }
        catch
        {
            // 손상/복호화 실패 시 빈 저장소로 시작(설정은 비치명적)
            return new(StringComparer.Ordinal);
        }
    }
}
