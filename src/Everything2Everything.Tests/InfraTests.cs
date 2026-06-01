using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Xunit;

namespace Everything2Everything.Tests;

public class SettingsStoreTests
{
    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "e2e_settings_" + Guid.NewGuid().ToString("N") + ".dat");

    [Fact]
    public void Set_Get_Roundtrip_AndPersists()
    {
        var path = TempFile();
        try
        {
            var store = new DpapiSettingsStore(path);
            store.Set("openai.key", "sk-secret-123");
            Assert.Equal("sk-secret-123", store.Get("openai.key"));
            Assert.True(store.Contains("openai.key"));

            // 새 인스턴스로 다시 로드 — 영속 + 복호화 확인
            var reloaded = new DpapiSettingsStore(path);
            Assert.Equal("sk-secret-123", reloaded.Get("openai.key"));
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void File_IsEncrypted_NotPlaintext()
    {
        var path = TempFile();
        try
        {
            new DpapiSettingsStore(path).Set("k", "PLAINTEXT_MARKER");
            var bytes = File.ReadAllBytes(path);
            var asText = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("PLAINTEXT_MARKER", asText);
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void Remove_DeletesKey()
    {
        var path = TempFile();
        try
        {
            var store = new DpapiSettingsStore(path);
            store.Set("k", "v");
            store.Remove("k");
            Assert.Null(store.Get("k"));
            Assert.False(store.Contains("k"));
        }
        finally { try { File.Delete(path); } catch { } }
    }
}

public class ExternalProcessRunnerTests
{
    [Fact]
    public async Task Echo_CapturesStdout_AndExitZero()
    {
        var r = await ExternalProcessRunner.RunAsync(
            "cmd.exe", new[] { "/c", "echo", "hello_e2e" },
            timeout: TimeSpan.FromSeconds(10), cancellationToken: CancellationToken.None);
        Assert.True(r.Success);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("hello_e2e", r.StdOut);
    }

    [Fact]
    public async Task NonZeroExit_IsReported()
    {
        var r = await ExternalProcessRunner.RunAsync(
            "cmd.exe", new[] { "/c", "exit", "3" },
            timeout: TimeSpan.FromSeconds(10), cancellationToken: CancellationToken.None);
        Assert.False(r.Success);
        Assert.Equal(3, r.ExitCode);
    }

    [Fact]
    public async Task Timeout_IsDetected_AndProcessKilled()
    {
        var r = await ExternalProcessRunner.RunAsync(
            "cmd.exe", new[] { "/c", "ping", "127.0.0.1", "-n", "10" },
            timeout: TimeSpan.FromMilliseconds(400), cancellationToken: CancellationToken.None);
        Assert.True(r.TimedOut);
        Assert.False(r.Success);
    }
}
