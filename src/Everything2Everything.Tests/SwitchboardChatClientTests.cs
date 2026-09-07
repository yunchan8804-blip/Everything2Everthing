using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Xunit;

namespace Everything2Everything.Tests;

public class SwitchboardChatClientTests
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
    public void SwitchboardChatClient_HasCorrectNameAndEndpoint()
    {
        var client = new SwitchboardChatClient();
        Assert.Equal("Switchboard Gateway", client.Name);
        Assert.Equal("http://127.0.0.1:8787", client.Endpoint);
    }

    [Fact]
    public void SwitchboardChatClient_CustomEndpoint_IsPreserved()
    {
        var client = new SwitchboardChatClient("http://192.168.1.100:8787/");
        Assert.Equal("http://192.168.1.100:8787", client.Endpoint);
    }

    [Fact]
    public void LlmProvider_ResolveClient_SelectsSwitchboardWhenRequested()
    {
        var store = new FakeStore();
        store.Set("switchboard.endpoint", "http://127.0.0.1:8787");
        var provider = new LlmProvider(store);
        var (client, model) = provider.ResolveClientForTesting(new AiOptions { Backend = "switchboard" });
        Assert.NotNull(client);
        Assert.IsType<SwitchboardChatClient>(client);
    }

    [Fact]
    public async Task CheckSwitchboardGatewayHealthAsync_ReturnsFalseForUnreachablePort()
    {
        var (available, _, _) = await ExternalToolDetector.CheckSwitchboardGatewayHealthAsync("http://127.0.0.1:59999");
        Assert.False(available);
    }

    [Fact]
    public void ExternalToolDetector_IsSwitchboardGatewayAvailable_DoesNotThrow()
    {
        var available = ExternalToolDetector.IsSwitchboardGatewayAvailable(out var ep);
        Assert.Equal("http://127.0.0.1:8787", ep);
        // Returns bool without hanging or throwing
    }

    [Fact]
    public void LlmProvider_CustomEndpoint_IsUsedWhenConfigured()
    {
        var store = new FakeStore();
        store.Set("switchboard.endpoint", "http://192.168.0.25:8787");
        var provider = new LlmProvider(store);
        var (client, _) = provider.ResolveClientForTesting(new AiOptions { Backend = "switchboard" });
        Assert.NotNull(client);
        var sbClient = Assert.IsType<SwitchboardChatClient>(client);
        Assert.Equal("http://192.168.0.25:8787", sbClient.Endpoint);
    }

    [Fact]
    public void ExternalToolDetector_IsSwitchboardGatewayAvailable_WithCustomEndpoint_PreservesEndpoint()
    {
        var available = ExternalToolDetector.IsSwitchboardGatewayAvailable(out var ep, "http://192.168.0.25:8787/");
        Assert.Equal("http://192.168.0.25:8787", ep);
        Assert.False(available);
    }
}

