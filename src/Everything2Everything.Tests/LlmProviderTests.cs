using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Xunit;

namespace Everything2Everything.Tests;

public class LlmProviderTests
{
    private sealed class FakeStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _d = new();
        public string? Get(string key) => _d.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, string value) => _d[key] = value;
        public void Remove(string key) => _d.Remove(key);
        public bool Contains(string key) => _d.ContainsKey(key);
    }

    private static bool EnvHasKey()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
           || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    [Fact]
    public async Task NoKey_IsNotReady()
    {
        if (EnvHasKey()) return; // 환경변수 키가 있으면 이 단언은 건너뜀
        var p = new LlmProvider(new FakeStore());
        var a = await p.CheckAvailabilityAsync();
        Assert.False(a.IsReady);
    }

    [Fact]
    public async Task WithKey_IsReady()
    {
        var store = new FakeStore();
        store.Set("openai.apikey", "sk-test-key");
        var p = new LlmProvider(store);
        var a = await p.CheckAvailabilityAsync();
        Assert.True(a.IsReady);
    }

    [Fact]
    public void BuildPrompt_Translate_IncludesTargetLanguage()
    {
        var (system, user) = LlmProvider.BuildPrompt(
            new AiOptions { Task = "translate", TargetLanguage = "일본어" }, "hello");
        Assert.Contains("일본어", system);
        Assert.Equal("hello", user);
    }

    [Fact]
    public void BuildPrompt_Summarize_MentionsSummary()
    {
        var (system, _) = LlmProvider.BuildPrompt(new AiOptions { Task = "summarize" }, "text");
        Assert.Contains("요약", system);
    }

    [Fact]
    public void DefaultGraph_HasAiTextSelfEdge()
    {
        var graph = Everything2EverythingBootstrap.CreateDefault().Providers.Graph;
        // AI self-edge(txt→txt)가 그래프에 존재 — 키 없으면 실행 시 NotReady로 비활성되지만 엣지는 등록됨
        Assert.NotNull(graph.FindBestPath(".txt", ".txt"));
        Assert.NotNull(graph.FindBestPath(".md", ".md"));
    }
}
