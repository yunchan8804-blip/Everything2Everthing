using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Xunit;

namespace Everything2Everything.Tests;

public class AgyChatClientTests
{
    private sealed class FakeStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _d = new();
        public string? Get(string key) => _d.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, string value) => _d[key] = value;
        public void Remove(string key) => _d.Remove(key);
        public bool Contains(string key) => _d.ContainsKey(key);
    }

    [Fact]
    public void ExternalToolDetector_CanDetectAgy()
    {
        var available = ExternalToolDetector.IsAgyAvailable(out var path);
        if (!available) return;
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.EndsWith("agy.exe", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgyChatClient_HasCorrectName()
    {
        var client = new AgyChatClient();
        Assert.Equal("Antigravity CLI (agy)", client.Name);
    }

    [Fact]
    public void LlmProvider_ResolveClient_AutoSelectsAgyWhenNoKey()
    {
        var store = new FakeStore();
        var provider = new LlmProvider(store);
        var (client, model) = provider.ResolveClientForTesting(new AiOptions { Backend = "agy" });
        Assert.NotNull(client);
        Assert.IsType<AgyChatClient>(client);
    }

    [Fact]
    public async Task LlmProvider_CheckAvailability_IsReadyWhenAgyAvailable()
    {
        if (!ExternalToolDetector.IsAgyAvailable(out _)) return;
        var store = new FakeStore();
        var provider = new LlmProvider(store);
        var availability = await provider.CheckAvailabilityAsync();
        Assert.True(availability.IsReady);
    }
}

