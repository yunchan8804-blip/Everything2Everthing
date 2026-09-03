using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 초엄격 디자인 & UX 감사 TDD 테스트 스위트 (XAML 정적 AST 분석)
/// UI에 시각적 치우침, 폰트 미가독, 컨테이너 오버플로, 인풋 패딩 오류, 아이콘-텍스트 수직 불일치, 이모지 혼용이 없는지 정밀 검증한다.
/// </summary>
public class DesignAuditAstTests
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

    private static IEnumerable<string> GetXamlFiles()
    {
        return Directory.GetFiles(ViewsDir, "*.xaml", SearchOption.AllDirectories);
    }

    [Fact]
    public void AllXamlFiles_ExistAndAreWellFormedXml()
    {
        var files = GetXamlFiles().ToList();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var doc = XDocument.Parse(content);
            Assert.NotNull(doc.Root);
        }
    }

    [Fact]
    public void ButtonContent_MustNotContainRawUnicodeEmojis()
    {
        // ⚙, 📁, 🗑, ✖, 🔧 등의 유니코드 이모지가 Button Content 속성에 날것으로 들어있으면 안 된다.
        // Fluent SymbolIcon이나 분리된 벡터 Path를 사용해야 베이스라인이 깨지지 않는다.
        var rawEmojis = new[] { "⚙", "📁", "🗑", "✖", "🔧", "✨", "🚀", "⚡" };
        var violations = new List<string>();

        foreach (var file in GetXamlFiles())
        {
            var doc = XDocument.Parse(File.ReadAllText(file));
            var buttons = doc.Descendants().Where(e => e.Name.LocalName == "Button" || e.Name.LocalName == "ToggleButton");

            foreach (var btn in buttons)
            {
                var content = btn.Attribute("Content")?.Value;
                if (!string.IsNullOrEmpty(content))
                {
                    foreach (var emoji in rawEmojis)
                    {
                        if (content.Contains(emoji))
                        {
                            var fileName = Path.GetFileName(file);
                            violations.Add($"[{fileName}] 버튼 Content에 날것의 이모지 '{emoji}' 발견: \"{content}\"");
                        }
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"유니코드 이모지 혼용 버튼이 발견되었습니다 (Fluent SymbolIcon 또는 분리된 Path로 수정 필요):\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void HorizontalStackPanels_WithIconAndText_MustHaveVerticalAlignmentCenter()
    {
        // Horizontal StackPanel 안에 Icon/Image/Path/Symbol/Ellipse 와 TextBlock 이 함께 들어갈 때,
        // 부모 StackPanel 또는 자식 요소들에 VerticalAlignment="Center" 가 누락되면 아이콘과 텍스트가 위아래로 어긋난다.
        var violations = new List<string>();

        foreach (var file in GetXamlFiles())
        {
            var fileName = Path.GetFileName(file);
            var doc = XDocument.Parse(File.ReadAllText(file));
            var stackPanels = doc.Descendants().Where(e => e.Name.LocalName == "StackPanel" &&
                                                           e.Attribute("Orientation")?.Value == "Horizontal");

            foreach (var sp in stackPanels)
            {
                var children = sp.Elements().ToList();
                bool hasIcon = children.Any(c => c.Name.LocalName is "Image" or "Path" or "SymbolIcon" or "Ellipse" or "Border");
                bool hasText = children.Any(c => c.Name.LocalName == "TextBlock");

                if (hasIcon && hasText)
                {
                    string? parentVAlign = sp.Attribute("VerticalAlignment")?.Value;
                    bool allChildrenCentered = children.All(c => c.Attribute("VerticalAlignment")?.Value == "Center");

                    if (parentVAlign != "Center" && !allChildrenCentered)
                    {
                        var lineInfo = (System.Xml.IXmlLineInfo)sp;
                        violations.Add($"[{fileName}:L{lineInfo.LineNumber}] 아이콘과 텍스트가 함께 들어있는 가로 StackPanel에 VerticalAlignment=\"Center\" 누락");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"아이콘-텍스트 수직 정렬 불일치가 발견되었습니다 (VerticalAlignment=\"Center\" 필수):\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void InputBoxStyles_MustHaveAdequatePadding()
    {
        // TextBox, PasswordBox 스타일은 텍스트 글리프가 테두리에 닿지 않도록
        // 수평 최소 8px, 수직 최소 4px 이상의 패딩을 가져야 한다.
        var themeFile = Path.Combine(ViewsDir, "FormatShiftTheme.xaml");
        var doc = XDocument.Parse(File.ReadAllText(themeFile));

        var inputStyles = doc.Descendants()
            .Where(e => e.Name.LocalName == "Style" &&
                        e.Attribute("TargetType")?.Value is "TextBox" or "PasswordBox")
            .ToList();

        Assert.NotEmpty(inputStyles);

        foreach (var style in inputStyles)
        {
            var styleKey = style.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
            var paddingSetter = style.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Setter" && e.Attribute("Property")?.Value == "Padding");

            Assert.NotNull(paddingSetter);
            var paddingVal = paddingSetter.Attribute("Value")?.Value;
            Assert.NotNull(paddingVal);

            var parts = paddingVal.Split(',').Select(p => double.Parse(p.Trim())).ToArray();
            double hPad = parts[0];
            double vPad = parts.Length > 1 ? parts[1] : parts[0];

            Assert.True(hPad >= 8, $"스타일 [{styleKey}]의 수평 패딩({hPad})이 최소 규격(8px) 미만입니다.");
            Assert.True(vPad >= 4, $"스타일 [{styleKey}]의 수직 패딩({vPad})이 최소 규격(4px) 미만입니다.");
        }
    }

    [Fact]
    public void TopNavigationButtons_MustHaveConsistentKoreanLabels()
    {
        // MainWindow 상단 네비게이션 액션 버튼들은 한국어로 일관성 있게 통일되어야 한다.
        // ("⚙ 설정", "Register Menu", "Diagnose", "Export Log", "Clear All" 같은 영한 혼용 방지)
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var actionsPanel = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "StackPanel" &&
                                 e.Attribute("Grid.Column")?.Value == "1" &&
                                 e.Attribute("Orientation")?.Value == "Horizontal" &&
                                 e.Elements().Any(c => c.Name.LocalName == "Button" && c.Attribute("Command")?.Value?.Contains("SettingsCommand") == true));

        Assert.NotNull(actionsPanel);

        var buttons = actionsPanel.Elements().Where(e => e.Name.LocalName == "Button").ToList();
        Assert.True(buttons.Count >= 4, "상단 액션 버튼이 최소 4개 이상이어야 합니다.");

        var englishKeywords = new[] { "Register Menu", "Diagnose", "Export Log", "Clear All" };
        var rawMixedButtons = new List<string>();

        foreach (var btn in buttons)
        {
            var content = btn.Attribute("Content")?.Value ?? "";
            if (englishKeywords.Any(k => content.Equals(k, StringComparison.OrdinalIgnoreCase)) || content.Contains("⚙"))
            {
                rawMixedButtons.Add(content);
            }
        }

        Assert.True(rawMixedButtons.Count == 0,
            $"상단 네비게이션 버튼에 혼용 또는 영문 레이블이 발견되었습니다 (정제된 한국어 표준으로 통일 필요):\n" +
            string.Join(", ", rawMixedButtons));
    }

    [Fact]
    public void PastResultsView_MustHaveEmptyStateIndicator()
    {
        // PastResultsView(변환 이력) 화면이 비어 있을 때 아무것도 안 나오는 시커먼 공백(Zero-void)이 되지 않도록
        // Empty State 안내 컨테이너(PastEmpty 또는 동등한 플레이스홀더)가 존재해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var pastResultsView = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PastResultsView");

        Assert.NotNull(pastResultsView);

        // 부모 Grid 또는 PastResultsView 내부에 Empty State를 위한 요소가 존재하는지 검사
        var emptyState = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value is "PastResultsEmpty" or "PastEmpty");

        Assert.NotNull(emptyState);
    }

    [Fact]
    public void DialogFooterButtons_MustHaveConsistentPadding()
    {
        // SettingsWindow 및 QuickOptionsWindow의 확인/취소 푸터 버튼은 동일 위계이므로
        // 일치하는 패딩 규격을 가져야 한다 (예: 닫기 18,8 vs 저장 22,8 비대칭 금지).
        var settingsFile = Path.Combine(ViewsDir, "SettingsWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(settingsFile));

        var footer = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" && e.Attribute("Grid.Row")?.Value == "2");

        Assert.NotNull(footer);

        var buttons = footer.Descendants().Where(e => e.Name.LocalName == "Button").ToList();
        Assert.True(buttons.Count >= 2);

        var paddings = buttons.Select(b => b.Attribute("Padding")?.Value).Distinct().ToList();
        Assert.True(paddings.Count == 1,
            $"SettingsWindow 푸터 버튼들의 패딩이 서로 다릅니다 (동일 위계 버튼은 패딩 통일 필수): {string.Join(", ", paddings)}");
    }
}
