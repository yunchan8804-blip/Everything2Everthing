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

    [Fact]
    public void MainWindow_MustHave_Presets_Search_Batch_And_Inspector_Elements()
    {
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        // 1. Dynamic Smart Presets (SmartPresetCombo 및 SmartPresetChipsPanel 존재)
        var smartPresetCombo = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ComboBox" &&
                                 e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "SmartPresetCombo");
        Assert.NotNull(smartPresetCombo);

        var specChipsPanel = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "SmartPresetChipsPanel");
        Assert.NotNull(specChipsPanel);

        // 2. SearchBox (검색창 존재)
        var searchBox = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TextBox" &&
                                 e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "SearchBox");
        Assert.NotNull(searchBox);

        // 3. BatchActionBar (일괄 작업 툴바 존재)
        var batchBar = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "BatchActionBar");
        Assert.NotNull(batchBar);

        // 4. Right Inspector Open Button (PreviewOpenFileCommand)
        var openButtons = doc.Descendants()
            .Where(e => e.Name.LocalName == "Button" &&
                        e.Attribute("Command")?.Value.Contains("PreviewOpenFileCommand") == true)
            .ToList();
        Assert.NotEmpty(openButtons);
    }

    [Fact]
    public void MainWindow_MustHave_CollapsibleInspectorColumn_And_ToggleCommand()
    {
        // 우측 미리보기 인스펙터를 원클릭으로 접고 펼쳐 큐 가로폭을 극대화할 수 있도록
        // InspectorColumn 명명된 ColumnDefinition과 ToggleInspectorCommand 버튼이 존재해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var inspectorCol = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ColumnDefinition" &&
                                 e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "InspectorColumn");
        Assert.NotNull(inspectorCol);

        var toggleBtn = doc.Descendants()
            .FirstOrDefault(e => (e.Name.LocalName == "Button" || e.Name.LocalName == "ToggleButton") &&
                                 e.Attribute("Command")?.Value.Contains("ToggleInspectorCommand") == true);
        Assert.NotNull(toggleBtn);
    }

    [Fact]
    public void FormatShiftTheme_MustDefine_DoubleBezelAndCardStyles()
    {
        // Fluent 2 / Dark Instrument 디자인 고도화를 위해
        // 8px 그리드 기반의 카드(Double-Bezel) 및 툴바 표준 스타일이 FormatShiftTheme.xaml에 선언되어야 한다.
        var themeFile = Path.Combine(ViewsDir, "FormatShiftTheme.xaml");
        var doc = XDocument.Parse(File.ReadAllText(themeFile));

        var cardStyle = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Style" &&
                                 e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "FsCardStyle");
        Assert.NotNull(cardStyle);

        var toolbarBtnStyle = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Style" &&
                                 e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "FsToolbarButtonStyle");
        Assert.NotNull(toolbarBtnStyle);
    }

    [Fact]
    public void InspectorHeader_MustHave_DismissOrCloseButton_WithToggleCommand()
    {
        // 우측 미리보기 인스펙터 헤더에 직관적으로 닫을 수 있는 닫기 버튼이 제공되어야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var inspectorCol = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 e.Attribute("Grid.Column")?.Value == "1" &&
                                 e.Descendants().Any(d => d.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PreviewImageArea"));
        Assert.NotNull(inspectorCol);

        var header = inspectorCol.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" && e.Attribute("Grid.Row")?.Value == "0");
        Assert.NotNull(header);

        var closeBtn = header.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button" &&
                                 e.Attribute("Command")?.Value.Contains("ToggleInspectorCommand") == true);
        Assert.NotNull(closeBtn);
    }

    [Fact]
    public void EmptyStates_MustHave_ActionableCallToActions_And_Shortcuts()
    {
        // Zero-Void 원칙: DropZone과 PastResults 빈 상태 화면은 단순 텍스트뿐 아니라
        // 사용자의 즉각적인 행동을 유도하는 Action 버튼을 포함해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var dropEmpty = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "DropZoneEmpty");
        Assert.NotNull(dropEmpty);

        var dropCta = dropEmpty.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button" &&
                                 e.Attribute("Command")?.Value.Contains("AddFilesCommand") == true);
        Assert.NotNull(dropCta);

        var pastEmpty = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PastResultsEmpty");
        Assert.NotNull(pastEmpty);

        var pastCta = pastEmpty.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button" &&
                                 e.Attribute("Command")?.Value.Contains("TabCommand") == true);
        Assert.NotNull(pastCta);
    }

    [Fact]
    public void TopHeader_MustHave_EngineStatusTelemetryBadge()
    {
        // designpaca R1/R2: 정밀 계측기 상단 툴바에 실시간 엔진 가동 상태 및 프로바이더 텔레메트리 뱃지가 존재해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var engineTelemetry = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "EngineTelemetryBadge");
        Assert.NotNull(engineTelemetry);
    }

    [Fact]
    public void FormatShiftTheme_MustDefine_InstrumentTokens_And_CategoryBrushes()
    {
        // designpaca R2/R3: FormatShiftTheme.xaml에 포맷 카테고리별 시그니처 브러시와 정밀 카드 스타일이 선언되어 있어야 한다.
        var themeFile = Path.Combine(ViewsDir, "FormatShiftTheme.xaml");
        var doc = XDocument.Parse(File.ReadAllText(themeFile));

        var keys = doc.Descendants()
            .Select(e => e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value)
            .Where(k => k != null)
            .ToHashSet();

        Assert.Contains("FsBadgeImageBrush", keys);
        Assert.Contains("FsBadgeDocBrush", keys);
        Assert.Contains("FsBadgeVideoBrush", keys);
        Assert.Contains("FsBadgeAudioBrush", keys);
        Assert.Contains("FsInstrumentCardStyle", keys);
    }

    [Fact]
    public void ActiveQueueList_ItemTemplate_MustHave_InstrumentCard_With_CategoryAndTelemetry()
    {
        // designpaca R1/R3: 대기열 리스트 DataTemplate에 단순 텍스트가 아닌 정밀 계측 카드와 카테고리 뱃지가 구조화되어야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var activeList = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ActiveQueueList");
        Assert.NotNull(activeList);

        var dataTemplate = activeList.Descendants().FirstOrDefault(e => e.Name.LocalName == "DataTemplate");
        Assert.NotNull(dataTemplate);

        // 카테고리 뱃지 및 모노스페이스 텔레메트리 식별
        var hasCategoryPill = dataTemplate.Descendants().Any(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ItemCategoryBadge" ||
            e.Attribute("Tag")?.Value == "CategoryPill");
        Assert.True(hasCategoryPill, "대기열 카드에 포맷 카테고리 뱃지(ItemCategoryBadge)가 있어야 합니다.");
    }

    [Fact]
    public void SwissMinimal_Tokens_MustInclude_NeonCyanSignatureAccent()
    {
        // designpaca 사용자 인터뷰 결정: 스위스 미니멀 + 네온 시안(#06B6D4) 시그니처 악센트 토큰 검증
        var themeFile = Path.Combine(ViewsDir, "FormatShiftTheme.xaml");
        var doc = XDocument.Parse(File.ReadAllText(themeFile));

        var cyanBrush = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "FsAccentCyan");
        Assert.NotNull(cyanBrush);
        Assert.Equal("#06B6D4", cyanBrush.Attribute("Color")?.Value);

        var cyanBgBrush = doc.Descendants()
            .FirstOrDefault(e => e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "FsAccentCyanBg");
        Assert.NotNull(cyanBgBrush);
        Assert.Equal("#083344", cyanBgBrush.Attribute("Color")?.Value);
    }

    [Fact]
    public void InteractiveElements_MustHave_AutomationPropertiesAutomationId_For_E2E_Reliability()
    {
        // UI Automation 기반 Headful E2E 테스트 및 접근성(A11y) 신뢰성을 위해 핵심 인터랙티브 요소는 AutomationId가 지정되어야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var requiredAutomationIds = new[]
        {
            "ProcessQueueButton",
            "AdvancedOptionsExpander",
            "OutputFormatCombo",
            "ImageLosslessCheck",
            "PdfCompressLevelCombo",
            "PdfDpiCombo",
            "QualitySlider",
            "VideoCrfSlider",
            "AudioBitrateQuickCombo",
            "PdfCompressQuickCombo"
        };

        var foundAutomationIds = doc.Descendants()
            .Select(e => e.Attribute(XName.Get("AutomationId", "clr-namespace:System.Windows.Automation;assembly=PresentationCore"))?.Value
                         ?? e.Attribute("AutomationProperties.AutomationId")?.Value)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet();

        var missing = requiredAutomationIds.Where(req => !foundAutomationIds.Contains(req)).ToList();
        Assert.True(missing.Count == 0,
            $"핵심 인터랙티브 요소에 AutomationProperties.AutomationId가 누락되었습니다: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Sidebar_ConflictRuleSection_MustUsePureKoreanLabels()
    {
        // designpaca & AGENTS.md Rule 3:
        // 영문 대문자 'FILE CONFLICT RULE' 및 영문 버튼 'Skip', 'Rename', 'Replace'를 금지하고
        // 정제된 한국어 표준 '파일 충돌 해결', '건너뛰기', '이름 변경', '덮어쓰기'로 일관되게 제공해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var allTexts = doc.Descendants().Where(e => e.Name.LocalName == "TextBlock")
            .Select(t => t.Attribute("Text")?.Value ?? t.Value)
            .ToList();

        Assert.DoesNotContain(allTexts, t => t.Contains("FILE CONFLICT RULE", StringComparison.OrdinalIgnoreCase));

        var skipBtn = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ConflictSkipBtn");
        var renameBtn = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ConflictRenameBtn");
        var replaceBtn = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ConflictReplaceBtn");

        Assert.NotNull(skipBtn);
        Assert.NotNull(renameBtn);
        Assert.NotNull(replaceBtn);

        Assert.Equal("건너뛰기", skipBtn.Attribute("Content")?.Value);
        Assert.Equal("이름 변경", renameBtn.Attribute("Content")?.Value);
        Assert.Equal("덮어쓰기", replaceBtn.Attribute("Content")?.Value);
    }

    [Fact]
    public void FormatShiftTheme_MustDefine_IslandButtonStyle_And_DoubleBezelStyles()
    {
        // high-end-visual-design Section 4:
        // Island Button (Button-in-Button) 스타일 및 Double-Bezel 카드 쉘 스타일이 선언되어야 한다.
        var themeFile = Path.Combine(ViewsDir, "FormatShiftTheme.xaml");
        var doc = XDocument.Parse(File.ReadAllText(themeFile));

        var keys = doc.Descendants()
            .Select(e => e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value)
            .Where(k => k != null)
            .ToHashSet();

        Assert.Contains("FsIslandPrimaryButtonStyle", keys);
        Assert.Contains("FsDoubleBezelShellStyle", keys);
    }

    [Fact]
    public void SegmentedTabs_MustUsePureKorean_And_NotContainEnglishRawLabels()
    {
        // AGENTS.md Rule 3:
        // 상단 내비게이션 탭은 영문 'Active Queue', 'Past Results' 대신 '대기열', '변환 기록'을 사용해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var tabActive = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "TabActiveBtn");
        var tabPast = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "TabPastBtn");

        Assert.NotNull(tabActive);
        Assert.NotNull(tabPast);

        var activeText = tabActive.Descendants().Where(e => e.Name.LocalName == "TextBlock").Select(t => t.Attribute("Text")?.Value ?? t.Value).FirstOrDefault();
        var pastText = tabPast.Descendants().Where(e => e.Name.LocalName == "TextBlock").Select(t => t.Attribute("Text")?.Value ?? t.Value).FirstOrDefault();

        Assert.Equal("대기열", activeText);
        Assert.Equal("변환 기록", pastText);
    }

    [Fact]
    public void Sidebar_CombineCheck_MustWrapText_ToPreventClipping()
    {
        // Design Audit Invariant 4: 컨테이너 오버플로 및 텍스트 생략 방지
        // CombineToSingleCheck 내부의 텍스트가 잘리지 않도록 TextBlock에 TextWrapping="Wrap"이 선언되어야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var combineCheck = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "CombineToSingleCheck");
        Assert.NotNull(combineCheck);

        var childTb = combineCheck.Descendants().FirstOrDefault(e => e.Name.LocalName == "TextBlock");
        Assert.NotNull(childTb);
        Assert.Equal("Wrap", childTb.Attribute("TextWrapping")?.Value);
    }

    [Fact]
    public void ProcessQueueButton_DefaultText_MustBeCompact_ToPreventOverflow()
    {
        // 280px 너비의 사이드바 내 Island 버튼(버튼 내 원형 화살표 포함)에서
        // '대기 중 — 파일을 드래그하여 추가하세요'와 같은 긴 텍스트는 글자 잘림을 유발하므로
        // '파일을 드래그하여 추가' 등 15자 이하의 정갈한 텍스트를 사용해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var btn = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ProcessQueueButton");
        Assert.NotNull(btn);

        var content = btn.Attribute("Content")?.Value;
        Assert.NotNull(content);
        Assert.True(content.Length <= 15, $"ProcessQueueButton의 텍스트가 너무 길어 잘림이 발생합니다: '{content}' ({content.Length}자)");
        Assert.Equal("파일을 드래그하여 추가", content);
    }

    [Fact]
    public void Sidebar_AiTaskAndEncodingCards_MustUseDoubleBezelShell_ForUniformStyle()
    {
        // high-end-visual-design Section 4:
        // 사이드바의 AI 작업 및 상세 인코딩 설정 카드도 FsDoubleBezelShellStyle로 통일되어 일관된 머신 하드웨어 룩을 완성해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var aiPanel = doc.Descendants().FirstOrDefault(d => d.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "AiTaskPanel");
        Assert.NotNull(aiPanel);

        var outerBorder = aiPanel.Ancestors().Where(a => a.Name.LocalName == "Border").Skip(1).FirstOrDefault();
        Assert.NotNull(outerBorder);
        Assert.Equal("{StaticResource FsDoubleBezelShellStyle}", outerBorder.Attribute("Style")?.Value);
    }

    [Fact]
    public void ActiveQueueList_StateText_MustBindToDisplayStateText_ForKoreanLocalization()
    {
        // AGENTS.md Rule 3:
        // 대기열 목록의 상태 표시는 날것의 영문 'queued', 'done' 대신
        // 한국어 '대기 중', '변환 완료'를 제공하는 DisplayStateText에 바인딩되어야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var queueList = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ActiveQueueList");
        Assert.NotNull(queueList);

        var boundTexts = queueList.Descendants().Where(e => e.Name.LocalName == "TextBlock")
            .Select(t => t.Attribute("Text")?.Value)
            .Where(v => v != null)
            .ToList();

        Assert.Contains("{Binding DisplayStateText}", boundTexts);
    }

    [Fact]
    public void MainWindow_Title_MustBe_Everything2Everything_And_NotContainFormatShiftUtility()
    {
        // 윈도우 타이틀은 레거시 'FormatShift Utility'가 아니라 공식 브랜드명 'Everything2Everything'이어야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var root = doc.Root;
        Assert.NotNull(root);

        var title = root.Attribute("Title")?.Value;
        Assert.Equal("Everything2Everything", title);
    }

    [Fact]
    public void DropHintOverlay_MustUsePureKorean_And_NotContainEnglishRawLabels()
    {
        // 드래그앤드롭 오버레이 안내 텍스트는 영문 날것 'Drop to add to queue' 대신 정제된 한국어 '파일을 놓아 대기열에 추가'여야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var overlay = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "DropHintOverlay");
        Assert.NotNull(overlay);

        var texts = overlay.Descendants().Where(e => e.Name.LocalName == "TextBlock")
            .Select(t => t.Attribute("Text")?.Value)
            .Where(v => v != null)
            .ToList();

        Assert.DoesNotContain(texts, t => t!.Contains("Drop to add to queue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(texts, t => t!.Contains("대기열"));
    }

    [Fact]
    public void SmartConversionDeck_MustDirectlyContain_AdvancedOptionsExpander_Below_Presets()
    {
        // Fluent 2 점진적 공개(Progressive Disclosure) 및 사용자 피드백 원칙:
        // 상세 설정 폴드아웃(AdvancedOptionsExpander)은 저장 위치나 AI 작업 카드의 한참 아래가 아니라,
        // 프리셋 선택 즉시 펼쳐서 미세조정할 수 있도록 '스마트 변환 덱'(SmartPresetCombo가 있는 첫 번째 카드) 내부에 직결되어야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var presetCombo = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "SmartPresetCombo");
        Assert.NotNull(presetCombo);

        var expander = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "AdvancedOptionsExpander");
        Assert.NotNull(expander);

        // SmartPresetCombo의 가장 가까운 Border 부모 (스마트 변환 덱 카드)
        var deckCard = presetCombo.Ancestors().FirstOrDefault(a => a.Name.LocalName == "Border" && (a.Attribute("Style")?.Value?.Contains("FsCardStyle") == true || a.Attribute("Style")?.Value?.Contains("FsDoubleBezelShellStyle") == true));
        Assert.NotNull(deckCard);

        // AdvancedOptionsExpander가 바로 그 스마트 변환 덱 카드(deckCard)의 자손이어야 한다.
        var isInsideDeck = deckCard.Descendants().Any(d => d == expander);
        Assert.True(isInsideDeck, "AdvancedOptionsExpander는 스마트 프리셋 바로 아래에서 펼쳐질 수 있도록 스마트 변환 덱(deckCard) 내부에 위치해야 합니다.");
    }

    [Fact]
    public void SmartConversionDeck_MustContain_AllQuickPanels_For_MediaTypes()
    {
        // 사용자 피드백: 프리셋만 퉁쳐져 있지 않고 이미지 품질, 비디오 CRF, 오디오 비트레이트, PDF 압축률 등
        // 핵심 슬라이더와 선택기가 스마트 변환 덱에 직관적으로 배치되어야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var requiredPanels = new[] { "QualityPanel", "VideoQuickPanel", "AudioQuickPanel", "PdfQuickPanel" };
        foreach (var panelName in requiredPanels)
        {
            var panel = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == panelName);
            Assert.NotNull(panel);
        }

        // 각 패널에 연결된 컨트롤 및 슬라이더 확인
        var qualitySlider = doc.Descendants().FirstOrDefault(e => e.Attribute("AutomationProperties.AutomationId")?.Value == "QualitySlider");
        Assert.NotNull(qualitySlider);

        var videoSlider = doc.Descendants().FirstOrDefault(e => e.Attribute("AutomationProperties.AutomationId")?.Value == "VideoCrfSlider");
        Assert.NotNull(videoSlider);

        var audioCombo = doc.Descendants().FirstOrDefault(e => e.Attribute("AutomationProperties.AutomationId")?.Value == "AudioBitrateQuickCombo");
        Assert.NotNull(audioCombo);

        var pdfCombo = doc.Descendants().FirstOrDefault(e => e.Attribute("AutomationProperties.AutomationId")?.Value == "PdfCompressQuickCombo");
        Assert.NotNull(pdfCombo);
    }

    [Fact]
    public void PastResultsEmpty_MustUseDoubleBezelShell_And_PureKoreanTerminology()
    {
        // 변환 기록 빈 상태도 FsDoubleBezelShellStyle을 사용하여 DropZoneEmpty와 일관된 머신 룩을 제공해야 하며,
        // 버튼 텍스트는 '큐'가 아닌 '대기열' 표준 한국어 용어를 사용해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var emptyContainer = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PastResultsEmpty");
        Assert.NotNull(emptyContainer);

        var bezelShell = emptyContainer.Descendants().FirstOrDefault(e => e.Name.LocalName == "Border" && e.Attribute("Style")?.Value == "{StaticResource FsDoubleBezelShellStyle}");
        Assert.NotNull(bezelShell);

        var texts = emptyContainer.Descendants().Where(e => e.Name.LocalName == "TextBlock")
            .Select(t => t.Attribute("Text")?.Value)
            .Where(v => v != null)
            .ToList();

        Assert.DoesNotContain(texts, t => t!.Contains("큐로"));
        Assert.Contains(texts, t => t!.Contains("대기열로"));
    }

    [Fact]
    public void SettingsWindow_Cards_MustUseDoubleBezelShell_ForUniformStyle()
    {
        // SettingsWindow의 AI 카드 및 외부 도구 카드도 FsDoubleBezelShellStyle 및 FsDoubleBezelCoreStyle을 사용하여
        // 앱 전체와 동일한 스위스 미니멀 이중 베젤 아키텍처를 준수해야 한다.
        var settingsFile = Path.Combine(ViewsDir, "SettingsWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(settingsFile));

        var shells = doc.Descendants().Where(e => e.Name.LocalName == "Border" && e.Attribute("Style")?.Value == "{StaticResource FsDoubleBezelShellStyle}").ToList();
        Assert.True(shells.Count >= 2, $"SettingsWindow는 최소 2개 이상의 이중 베젤 쉘 카드를 포함해야 합니다. (발견된 수: {shells.Count})");
    }

    [Fact]
    public void Sidebar_AllCards_MustUseDoubleBezelShell_ForUnifiedAesthetic()
    {
        // high-end-visual-design Section 4 & stitch-design-taste:
        // 사이드바의 모든 덱(스마트 변환 덱, 저장 위치 및 충돌 해결 덱, AI 작업 덱, 통계 덱)은
        // 단일 보더 플랫 카드(FsCardStyle)가 아닌 이중 베젤(FsDoubleBezelShellStyle + FsDoubleBezelCoreStyle)로 통일되어야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var sidebarScroll = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ScrollViewer" && e.Ancestors().Any(a => a.Name.LocalName == "Border" && a.Attribute(XName.Get("Column", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "0" || a.Attribute("Grid.Column")?.Value == "0"));
        Assert.NotNull(sidebarScroll);

        var stackPanel = sidebarScroll.Descendants().FirstOrDefault(e => e.Name.LocalName == "StackPanel");
        Assert.NotNull(stackPanel);

        // stackPanel의 직계 자식 카드들
        var directChildCards = stackPanel.Elements().Where(e => e.Name.LocalName == "Border").ToList();
        Assert.True(directChildCards.Count >= 4, $"사이드바에는 최소 4개 이상의 카드가 있어야 합니다. (발견된 수: {directChildCards.Count})");

        foreach (var card in directChildCards)
        {
            var style = card.Attribute("Style")?.Value;
            Assert.Equal("{StaticResource FsDoubleBezelShellStyle}", style);
        }
    }

    [Fact]
    public void PreviewPane_MustUsePureKorean_And_NotContainEnglishLabels()
    {
        // AGENTS.md 규약 3 & stitch-design-taste:
        // 우측 미리보기 인스펙터 패널의 헤더, 빈 상태 안내문구, 메타데이터 레이블은
        // 날것의 영어(PREVIEW, No selection, Active Queue, Format, Size, Dimensions, Pages) 대신
        // 정갈하고 직관적인 한국어(미리보기, 선택된 항목 없음, 대기열, 형식, 크기, 해상도 등)를 사용해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var textBlocks = doc.Descendants().Where(e => e.Name.LocalName == "TextBlock").ToList();
        var allTexts = textBlocks.Select(t => t.Attribute("Text")?.Value).Where(v => v != null).ToList();

        // 영어 레이블 금지
        Assert.DoesNotContain(allTexts, t => t == "PREVIEW");
        Assert.DoesNotContain(allTexts, t => t == "No selection");
        Assert.DoesNotContain(allTexts, t => t != null && t.Contains("Active Queue 항목을 클릭하면"));
        Assert.DoesNotContain(allTexts, t => t == "Format");
        Assert.DoesNotContain(allTexts, t => t == "Size");
        Assert.DoesNotContain(allTexts, t => t == "Dimensions");
        Assert.DoesNotContain(allTexts, t => t == "Pages");

        // 한국어 레이블 필수
        Assert.Contains(allTexts, t => t == "선택된 항목 없음");
        Assert.Contains(allTexts, t => t == "형식");
        Assert.Contains(allTexts, t => t == "크기");
        Assert.Contains(allTexts, t => t != null && t.Contains("해상도"));
    }

    [Fact]
    public void DropZoneEmpty_MustUseDoubleBezelShell_ForLinearTierMachinedLook()
    {
        // high-end-visual-design Section 4 (Double-Bezel):
        // 중앙 메인 빈 상태 드롭존(DropZoneEmpty)도 단일 대시 사각형이 아닌
        // FsDoubleBezelShellStyle + FsDoubleBezelCoreStyle 이중 베젤 쉘 아키텍처로 구현되어
        // PastResultsEmpty 및 사이드바 덱들과 완벽한 하드웨어 머신 룩 통일감을 형성해야 한다.
        var mainFile = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(mainFile));

        var dropZone = doc.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "DropZoneEmpty");
        Assert.NotNull(dropZone);

        var bezelShell = dropZone.Descendants().FirstOrDefault(e => e.Name.LocalName == "Border" && e.Attribute("Style")?.Value == "{StaticResource FsDoubleBezelShellStyle}");
        Assert.NotNull(bezelShell);
    }

    [Fact]
    public void MainWindow_CodeBehind_DateLabels_MustUseKorean_And_NotContainEnglishTodayOrYesterday()
    {
        // AGENTS.md 규약 3 & stitch-design-taste:
        // 변환 기록 날짜 헤더(DateTitle)는 영문 'Today', 'Yesterday' 대신 정제된 한국어 '오늘', '어제'를 사용해야 한다.
        var codeFile = Path.Combine(ViewsDir, "MainWindow.xaml.cs");
        var code = File.ReadAllText(codeFile);

        Assert.DoesNotContain("\"Today\"", code);
        Assert.DoesNotContain("\"Yesterday\"", code);
        Assert.Contains("\"오늘", code);
        Assert.Contains("\"어제", code);
    }

    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("SettingsWindow.xaml")]
    [InlineData("DiagnoseWindow.xaml")]
    [InlineData("QuickOptionsWindow.xaml")]
    [InlineData("QuickProgressWindow.xaml")]
    public void AllWindows_MustInclude_UiControlsDictionary_InResources(string xamlName)
    {
        // Wpf.Ui Fluent 2 컨트롤(ComboBox, Slider, Button 등)의 다크 테마 룩앤필이
        // 독립 실행 및 In-Memory 렌더링 시에도 100% 보장되도록 각 윈도우의 MergedDictionaries는
        // <ui:ControlsDictionary/>를 필수로 포함해야 한다.
        var file = Path.Combine(ViewsDir, xamlName);
        var doc = XDocument.Parse(File.ReadAllText(file));

        var hasControlsDict = doc.Descendants().Any(e => e.Name.LocalName == "ControlsDictionary");
        Assert.True(hasControlsDict, $"{xamlName}에 <ui:ControlsDictionary/>가 선언되어 있지 않습니다.");
    }

    [Fact]
    public void MainWindow_CodeBehind_MustNotContain_RawEnglish_PastResults()
    {
        // AGENTS.md 규약 3 & stitch-design-taste:
        // 메시지 박스 및 다이얼로그 안내 문구에서도 'Past Results' 날것의 영어를 금지하고
        // '변환 기록' 표준 한국어로 안내해야 한다.
        var codeFile = Path.Combine(ViewsDir, "MainWindow.xaml.cs");
        var code = File.ReadAllText(codeFile);

        Assert.DoesNotContain("\"Past Results", code);
    }

    [Fact]
    public void PastResults_MetaLine_MustHave_TextTrimmingCharacterEllipsis_ToPreventOverflow()
    {
        // AGENTS.md Rule 4 (Overflow Safety):
        // 긴 파일 메타데이터(타임스탬프 + 파일수)를 표시하는 TextBlock은
        // 좁은 컬럼 폭에서도 오버플로/글리프 잘림 없이 안전하게 축약되어야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var metaTextBlocks = doc.Descendants()
            .Where(e => e.Name.LocalName == "TextBlock" &&
                        e.Attribute("Text")?.Value == "{Binding MetaLine}")
            .ToList();

        Assert.NotEmpty(metaTextBlocks);
        foreach (var tb in metaTextBlocks)
        {
            var trimming = tb.Attribute("TextTrimming")?.Value;
            Assert.True(trimming == "CharacterEllipsis",
                "MetaLine TextBlock은 TextTrimming=\"CharacterEllipsis\"를 지정하여 오버플로를 방지해야 합니다.");
        }
    }

    [Fact]
    public void MainWindow_SearchAndFilterToolbar_MustBeScopedToListColumn()
    {
        // 검색 및 카테고리 필터 바는 미리보기(Inspector) 위로 어색하게 500px 걸쳐있지 않고,
        // 대상이 되는 대기열/기록 리스트 컬럼(Grid.Column="0") 내부에 정렬되어야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var searchBox = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "TextBox" &&
                                                             e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "SearchBox");
        Assert.NotNull(searchBox);

        // SearchBox를 감싸는 툴바 Border 찾기
        var toolbarBorder = searchBox.Ancestors().FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                                                      e.Attribute("Grid.Row")?.Value == "0");
        Assert.NotNull(toolbarBorder);

        var colSpan = toolbarBorder.Attribute("Grid.ColumnSpan")?.Value;
        Assert.True(string.IsNullOrEmpty(colSpan) || colSpan == "1",
            "Search/Filter Toolbar는 Inspector 컬럼을 침범하는 ColumnSpan=\"2\"가 아니어야 하며, 리스트 컬럼(Column 0)에 정합되어야 합니다.");
    }

    [Fact]
    public void ActiveQueueList_Columns_MustProvideAdequateWidthForMetadata()
    {
        // 대기열 아이템 행 그리드에서 파일 크기 및 상태 컬럼이 과도하게 너비를 차지하여
        // 파일명과 메타데이터 문자열이 중간에 잘리지 않도록 컬럼 너비를 정밀 최적화해야 한다.
        // SizeText 컬럼 <= 80px, DisplayStateText 컬럼 <= 90px
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var queueList = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ItemsControl" &&
                                                              e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ActiveQueueList");
        Assert.NotNull(queueList);

        var colDefs = queueList.Descendants()
            .Where(e => e.Name.LocalName == "Grid.ColumnDefinitions" && e.Elements().Count() >= 6)
            .FirstOrDefault();
        Assert.NotNull(colDefs);

        var widths = colDefs.Elements().Select(c => c.Attribute("Width")?.Value).ToList();
        Assert.True(widths.Count >= 6, "ActiveQueueList 행 그리드는 최소 6개 컬럼(선택, 아이콘, 본문*, 크기, 상태, 삭제)이어야 합니다.");

        // Column 3: SizeText width
        var sizeWidth = widths[3];
        Assert.True(int.TryParse(sizeWidth, out var sw) && sw <= 80,
            $"SizeText 컬럼 폭은 80px 이하여야 메타데이터 컬럼이 오버플로되지 않습니다. 현재: {sizeWidth}");

        // Column 4: StateText width
        var stateWidth = widths[4];
        Assert.True(int.TryParse(stateWidth, out var stw) && stw <= 90,
            $"DisplayStateText 컬럼 폭은 90px 이하여야 메타데이터 컬럼이 오버플로되지 않습니다. 현재: {stateWidth}");
    }

    [Fact]
    public void Inspector_PreviewArea_MustUse_DoubleBezelArchitecture()
    {
        // 미리보기 뷰포트는 단순 평면 박스가 아니라 고품격 machined 하드웨어 느낌의
        // FsDoubleBezelShellStyle 및 FsDoubleBezelCoreStyle 이중 베젤(Doppelrand) 구조를 가져야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var previewArea = doc.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PreviewImageArea");
        Assert.NotNull(previewArea);

        var hasShell = previewArea.Descendants().Any(e =>
            e.Attribute("Style")?.Value.Contains("FsDoubleBezelShellStyle") == true);
        var hasCore = previewArea.Descendants().Any(e =>
            e.Attribute("Style")?.Value.Contains("FsDoubleBezelCoreStyle") == true);

        Assert.True(hasShell && hasCore,
            "PreviewImageArea는 고품격 Fluent 2 미학을 위해 FsDoubleBezelShellStyle 및 FsDoubleBezelCoreStyle 이중 베젤 구조를 갖추어야 합니다.");
    }

    [Fact]
    public void MainWindow_SearchBox_Width_MustLeaveAdequateRoomForFilterButtons()
    {
        // 1080p 및 컴팩트 뷰포트에서도 '전체' 및 카테고리 필터 버튼들이
        // 검색창에 가려지거나 겹치지 않도록 검색창 너비는 180px 이하여야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var searchBox = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "TextBox" &&
                                                             e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "SearchBox");
        Assert.NotNull(searchBox);

        var toolbarGrid = searchBox.Ancestors()
            .FirstOrDefault(e => e.Name.LocalName == "Grid" && e.Element(searchBox.Name.Namespace + "Grid.ColumnDefinitions") != null);
        Assert.NotNull(toolbarGrid);

        var searchBoxCol = toolbarGrid.Element(searchBox.Name.Namespace + "Grid.ColumnDefinitions")?
            .Elements().FirstOrDefault();
        Assert.NotNull(searchBoxCol);

        var widthVal = searchBoxCol.Attribute("Width")?.Value;
        Assert.True(int.TryParse(widthVal, out var w) && w <= 180,
            $"SearchBox 컬럼 폭은 180px 이하여야 필터 버튼들이 잘리지 않습니다. 현재: {widthVal}");
    }

    [Fact]
    public void BatchActionBar_MustInclude_LiveCockpitSummary()
    {
        // BatchActionBar는 단순 버튼 나열이 아니라 큐의 총 파일 개수 및 총 데이터 용량을 한눈에 보여주는
        // QueueSummaryCountText 및 QueueSummarySizeText 콕핏 텔레메트리 요소를 포함해야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var batchBar = doc.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "BatchActionBar");
        Assert.NotNull(batchBar);

        var summaryCount = batchBar.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "QueueSummaryCountText");
        var summarySize = batchBar.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "QueueSummarySizeText");

        Assert.NotNull(summaryCount);
        Assert.NotNull(summarySize);
    }

    [Fact]
    public void Inspector_MustInclude_ConversionPipelineCard()
    {
        // 미리보기 패널(Inspector)은 원본과 대상 형식 간 변환 경로 및 예상 절감 효과를 즉시 시각화하는
        // ConversionPipelineCard 컨테이너를 포함해야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var pipelineCard = doc.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ConversionPipelineCard");
        Assert.NotNull(pipelineCard);

        var sourceText = pipelineCard.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PipelineSourceText");
        var targetText = pipelineCard.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PipelineTargetText");

        Assert.NotNull(sourceText);
        Assert.NotNull(targetText);
    }

    [Fact]
    public void PastResults_MustInclude_TelemetryHeaderBar()
    {
        // 변환 기록 뷰(PastResultsContainer)는 단순 리스트 노출 전에 누적 변환 수량 및 총 절감량을 요약하고
        // 빠른 내보내기 액션을 제공하는 PastResultsHeaderBar 툴바를 포함해야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var container = doc.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PastResultsContainer");
        Assert.NotNull(container);

        var headerBar = container.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PastResultsHeaderBar");
        Assert.NotNull(headerBar);
    }

    [Fact]
    public void Sidebar_OutputFormatHint_MustWrapText()
    {
        // 320px 좁은 사이드바 폭에서도 설명 문구가 '자동 필...'처럼 잘리지 않도록
        // OutputFormatHint는 TextWrapping="Wrap"을 선언해야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var hint = doc.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "OutputFormatHint");
        Assert.NotNull(hint);

        var textWrapping = hint.Attribute("TextWrapping")?.Value;
        Assert.Equal("Wrap", textWrapping);
    }
    [Fact]
    public void SearchBox_MustInclude_WatermarkPlaceholder_And_ClearButton()
    {
        // 검색창은 플레이스홀더 워터마크(SearchPlaceholderText)와 입력 내용 원클릭 지우기 버튼(SearchClearButton)을 포함해야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var placeholder = doc.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "SearchPlaceholderText");
        var clearBtn = doc.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "SearchClearButton");

        Assert.NotNull(placeholder);
        Assert.NotNull(clearBtn);
    }

    [Fact]
    public void Inspector_ActionButtons_MustBind_IsEnabled_To_Selection()
    {
        // 파일이 선택되지 않았을 때 무의미한 클릭을 방지하기 위해
        // 미리보기 하단의 열기(PreviewOpenButton) 및 폴더보기(PreviewRevealButton) 버튼은 IsEnabled 바인딩 또는 제어를 가져야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var openBtn = doc.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PreviewOpenButton");
        var revealBtn = doc.Descendants().FirstOrDefault(e =>
            e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "PreviewRevealButton");

        Assert.NotNull(openBtn);
        Assert.NotNull(revealBtn);

        var openEnabled = openBtn.Attribute("IsEnabled")?.Value;
        var revealEnabled = revealBtn.Attribute("IsEnabled")?.Value;

        Assert.NotNull(openEnabled);
        Assert.NotNull(revealEnabled);
    }

    [Fact]
    public void Window_InputBindings_MustInclude_SelectAll_And_Delete()
    {
        // Raycast / Linear 급 생산성을 위해 전체 선택(Ctrl+A) 및 선택 항목 삭제(Delete) 단축키가 Window.InputBindings에 등록되어야 한다.
        var file = Path.Combine(ViewsDir, "MainWindow.xaml");
        var doc = XDocument.Parse(File.ReadAllText(file));

        var inputBindings = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Window.InputBindings");
        Assert.NotNull(inputBindings);

        var keyBindings = inputBindings.Elements().Where(e => e.Name.LocalName == "KeyBinding").ToList();
        bool hasCtrlA = keyBindings.Any(kb => kb.Attribute("Key")?.Value == "A" && kb.Attribute("Modifiers")?.Value == "Ctrl");
        bool hasDelete = keyBindings.Any(kb => kb.Attribute("Key")?.Value == "Delete");

        Assert.True(hasCtrlA, "Window.InputBindings에 Ctrl+A 단축키가 등록되어야 합니다.");
        Assert.True(hasDelete, "Window.InputBindings에 Delete 단축키가 등록되어야 합니다.");
    }
}









