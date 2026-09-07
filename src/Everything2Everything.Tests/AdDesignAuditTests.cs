using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Everything2Everything.Tests;

public class AdDesignAuditTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();
    private static readonly string ViewsDir = Path.Combine(SolutionRoot, "src", "Everything2Everything.App", "Views");

    private static string FindSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "Everything2Everything.slnx")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? throw new DirectoryNotFoundException("솔루션 루트를 찾을 수 없습니다.");
    }

    [Fact]
    public void AdControls_MustExistAndBeWellFormed()
    {
        var bannerPath = Path.Combine(ViewsDir, "AdBannerControl.xaml");
        var largeCardPath = Path.Combine(ViewsDir, "AdLargeCardControl.xaml");

        Assert.True(File.Exists(bannerPath), "AdBannerControl.xaml 이 존재해야 합니다.");
        Assert.True(File.Exists(largeCardPath), "AdLargeCardControl.xaml 이 존재해야 합니다.");

        var bannerDoc = XDocument.Parse(File.ReadAllText(bannerPath));
        var largeCardDoc = XDocument.Parse(File.ReadAllText(largeCardPath));

        Assert.NotNull(bannerDoc.Root);
        Assert.NotNull(largeCardDoc.Root);
    }

    [Fact]
    public void AdControls_MustComplyWithStrictDesignAudit()
    {
        var targetFiles = new[]
        {
            Path.Combine(ViewsDir, "AdBannerControl.xaml"),
            Path.Combine(ViewsDir, "AdLargeCardControl.xaml")
        };

        var rawEmojis = new[] { "⚙", "📁", "🗑", "✖", "🔧", "✨", "🚀", "⚡" };
        var violations = new List<string>();

        foreach (var file in targetFiles)
        {
            if (!File.Exists(file)) continue;

            var doc = XDocument.Parse(File.ReadAllText(file));
            var fileName = Path.GetFileName(file);

            // 1. 유니코드 이모지 금지 검증
            var buttons = doc.Descendants().Where(e => e.Name.LocalName is "Button" or "ToggleButton");
            foreach (var btn in buttons)
            {
                var content = btn.Attribute("Content")?.Value;
                if (!string.IsNullOrEmpty(content) && rawEmojis.Any(emoji => content.Contains(emoji)))
                {
                    violations.Add($"[{fileName}] 버튼에 이모지 사용 감지: {content}");
                }
            }

            // 2. 가로 StackPanel 내 아이콘/텍스트 VerticalAlignment="Center" 검증
            var stackPanels = doc.Descendants().Where(e => e.Name.LocalName == "StackPanel" &&
                                                           e.Attribute("Orientation")?.Value == "Horizontal");
            foreach (var sp in stackPanels)
            {
                foreach (var child in sp.Elements())
                {
                    var va = child.Attribute("VerticalAlignment")?.Value;
                    if (va != "Center")
                    {
                        violations.Add($"[{fileName}] 가로 StackPanel의 자식 <{child.Name.LocalName}>에 VerticalAlignment=\"Center\" 누락");
                    }
                }
            }

            // 3. TextBlock 오버플로 방지 (TextTrimming 또는 TextWrapping) 검증
            var textBlocks = doc.Descendants().Where(e => e.Name.LocalName == "TextBlock");
            foreach (var tb in textBlocks)
            {
                var text = tb.Attribute("Text")?.Value ?? "";
                // 바인딩된 동적 텍스트는 Trimming이나 Wrapping 필수
                if (text.Contains("{Binding") || text.Contains("{x:Bind"))
                {
                    var trimming = tb.Attribute("TextTrimming")?.Value;
                    var wrapping = tb.Attribute("TextWrapping")?.Value;
                    if (string.IsNullOrEmpty(trimming) && string.IsNullOrEmpty(wrapping))
                    {
                        violations.Add($"[{fileName}] 바인딩 TextBlock에 TextTrimming 또는 TextWrapping 누락: {text}");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            "광고 컨트롤 디자인 감사 위반이 발견되었습니다:\n" + string.Join("\n", violations));
    }
}
