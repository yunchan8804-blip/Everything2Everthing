using Everything2Everything.App.ViewModels;
using Everything2Everything.Core.Converters;
using Xunit;

namespace Everything2Everything.Tests;

public class ExternalToolInstallerTests
{
    [Fact]
    public void Catalog_CoversAllKnownTools_WithVerifiedWingetIds()
    {
        var byKey = ExternalToolCatalog.All.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(7, byKey.Count);

        Assert.Equal("Gyan.FFmpeg", byKey["ffmpeg"].WingetId);
        Assert.Equal("TheDocumentFoundation.LibreOffice", byKey["libreoffice"].WingetId);
        Assert.Equal("JohnMacFarlane.Pandoc", byKey["pandoc"].WingetId);
        Assert.Equal("ImageMagick.ImageMagick", byKey["imagemagick"].WingetId);
        Assert.Equal("Google.AntigravityCLI", byKey["agy"].WingetId);
        Assert.Equal("OpenAI.Codex", byKey["codex"].WingetId);

        Assert.Equal(ExternalToolInstallKind.Manual, byKey["h2orestart"].Kind);
        Assert.False(string.IsNullOrWhiteSpace(byKey["h2orestart"].ManualNote));
    }

    [Fact]
    public async Task InstallAsync_WingetTool_RunsSilentWinget_AndReportsInstalled()
    {
        var runner = new CapturingRunner();
        var installer = new ExternalToolInstaller(runner);
        var tool = WingetTool("test-tool", "Contoso.Demo", () => true);

        var result = await installer.InstallAsync(tool, CancellationToken.None);

        Assert.Equal("winget", runner.LastFileName);
        Assert.Contains("--id Contoso.Demo", runner.LastArgs);
        Assert.Contains("--silent", runner.LastArgs);
        Assert.Contains("--accept-package-agreements", runner.LastArgs);
        Assert.Contains("--accept-source-agreements", runner.LastArgs);
        Assert.Contains("--disable-interactivity", runner.LastArgs);
        Assert.True(result.Success);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task InstallAsync_WhenStillNotDetectedAfterRun_ReportsFailure()
    {
        var runner = new CapturingRunner { ExitCode = 1, Output = "installer failed" };
        var installer = new ExternalToolInstaller(runner);
        var tool = WingetTool("test-tool", "Contoso.Demo", () => false);

        var result = await installer.InstallAsync(tool, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("실패", result.Message);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task InstallAsync_ManualKind_DoesNotRunAnything_ReturnsGuidance()
    {
        var runner = new CapturingRunner();
        var installer = new ExternalToolInstaller(runner);
        var tool = new ExternalToolDefinition
        {
            Key = "manual",
            DisplayName = "Manual",
            Description = "d",
            Kind = ExternalToolInstallKind.Manual,
            ManualNote = "직접 추가하세요",
            IsInstalled = () => false,
        };

        var result = await installer.InstallAsync(tool, CancellationToken.None);

        Assert.Null(runner.LastFileName);
        Assert.False(result.Success);
        Assert.Equal("직접 추가하세요", result.Message);
    }

    [Fact]
    public void ToolSetupViewModel_DefaultsToAllSelected_AndCoversCatalog()
    {
        var vm = new ToolSetupViewModel(
            ExternalToolCatalog.All,
            new ExternalToolInstaller(new CapturingRunner()));

        Assert.Equal(ExternalToolCatalog.All.Count, vm.Tools.Count);
        Assert.All(vm.Tools, t => Assert.True(t.IsSelected));
        Assert.True(vm.SelectAll);
    }

    private static ExternalToolDefinition WingetTool(string key, string id, Func<bool> installed) => new()
    {
        Key = key,
        DisplayName = key,
        Description = key,
        Kind = ExternalToolInstallKind.Winget,
        WingetId = id,
        IsInstalled = installed,
    };

    private sealed class CapturingRunner : IExternalCommandRunner
    {
        public string? LastFileName;
        public string? LastArgs;
        public int ExitCode { get; set; }
        public string Output { get; set; } = "";

        public Task<ExternalCommandResult> RunAsync(string fileName, string arguments, string? workingDirectory, CancellationToken ct)
        {
            LastFileName = fileName;
            LastArgs = arguments;
            return Task.FromResult(new ExternalCommandResult(ExitCode, Output));
        }
    }
}
