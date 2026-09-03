using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Everything2Everything.Tests;

public class PackagingSignatureTests
{
    private static string FindRepoRoot()
    {
        var current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Everything2Everything.slnx")) ||
                File.Exists(Path.Combine(current, "AGENTS.md")))
            {
                return current;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    [Fact]
    public void AppxManifest_Publisher_Matches_DevCert_Subject()
    {
        var repoRoot = FindRepoRoot();
        var manifestPath = Path.Combine(repoRoot, "packaging", "Package.appxmanifest");
        Assert.True(File.Exists(manifestPath), $"Package.appxmanifest must exist at {manifestPath}");

        var doc = XDocument.Load(manifestPath);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var identity = doc.Root?.Element(ns + "Identity");
        Assert.NotNull(identity);

        var publisher = identity.Attribute("Publisher")?.Value;
        Assert.Equal("CN=Everything2EverythingDev", publisher);
    }

    [Fact]
    public void EnvExample_Contains_CodeSigning_Variables()
    {
        var repoRoot = FindRepoRoot();
        var envExamplePath = Path.Combine(repoRoot, ".env.example");
        Assert.True(File.Exists(envExamplePath), ".env.example template file must exist");

        var content = File.ReadAllText(envExamplePath);
        Assert.Contains("CODE_SIGN_PFX_PATH", content);
        Assert.Contains("CODE_SIGN_PFX_PASSWORD", content);
        Assert.Contains("CODE_SIGN_THUMBPRINT", content);
    }

    [Fact]
    public void PublishReleaseScript_Enforces_Signing_Flag()
    {
        var repoRoot = FindRepoRoot();
        var publishScript = Path.Combine(repoRoot, "tools", "Publish-Release.ps1");
        Assert.True(File.Exists(publishScript), "Publish-Release.ps1 must exist");

        var content = File.ReadAllText(publishScript);
        // BuildMsix must be invoked with -Sign flag
        Assert.Contains("BuildMsix.ps1", content);
        Assert.Matches(@"(?i)-Sign\b", content);
    }

    [Fact]
    public void InstallCmd_Exists_For_OneClick_Elevation()
    {
        var repoRoot = FindRepoRoot();
        var installCmdPath = Path.Combine(repoRoot, "packaging", "Install.cmd");
        Assert.True(File.Exists(installCmdPath), "packaging/Install.cmd must exist for 1-click end-user installation");

        var content = File.ReadAllText(installCmdPath);
        Assert.Contains("powershell", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TrustedPeople", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InnoSetupScript_Exists_And_Configures_Lowest_Privileges_And_ContextMenu()
    {
        var repoRoot = FindRepoRoot();
        var issPath = Path.Combine(repoRoot, "packaging", "Everything2Everything.iss");
        Assert.True(File.Exists(issPath), "packaging/Everything2Everything.iss must exist for standard EXE installer");

        var content = File.ReadAllText(issPath);
        Assert.Contains("PrivilegesRequired=lowest", content);
        Assert.Contains("PrivilegesRequiredOverridesAllowed=dialog commandline", content);
        Assert.Contains("register", content);
        Assert.Contains("unregister", content);
    }
}
