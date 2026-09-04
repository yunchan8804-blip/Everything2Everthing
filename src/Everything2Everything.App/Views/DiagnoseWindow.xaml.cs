using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Everything2Everything.Core;
using Everything2Everything.Core.Providers;
using Wpf.Ui.Controls;

namespace Everything2Everything.App.Views;

public partial class DiagnoseWindow : FluentWindow
{
    private readonly ConversionEngine _engine;

    public DiagnoseWindow(ConversionEngine engine)
    {
        _engine = engine;
        InitializeComponent();
        Loaded += async (_, _) => await PopulateAsync();
    }

    public async Task PopulateAsync()
    {
        ItemsPanel.Children.Clear();

        ItemsPanel.Children.Add(BuildSection("환경", new[]
        {
            ("OS", Environment.OSVersion.VersionString),
            (".NET", Environment.Version.ToString()),
            ("실행 경로", Environment.ProcessPath ?? AppContext.BaseDirectory),
        }));

        foreach (var provider in _engine.Providers.All)
        {
            var availability = await provider.CheckAvailabilityAsync();
            ItemsPanel.Children.Add(BuildProviderCard(provider, availability));
        }
    }

    private Brush SafeBrush(string key, Brush fallback)
    {
        return (TryFindResource(key) as Brush)
            ?? (Application.Current?.TryFindResource(key) as Brush)
            ?? fallback;
    }

    private UIElement BuildSection(string title, IEnumerable<(string Key, string Value)> items)
    {
        var card = new Border
        {
            Background = SafeBrush("FsBgSurface", new SolidColorBrush(Color.FromRgb(0x1B, 0x1D, 0x22))),
            BorderBrush = SafeBrush("FsBorderHairline", new SolidColorBrush(Color.FromRgb(0x33, 0x38, 0x42))),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 12),
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = (FontFamily?)TryFindResource("FsFontSans"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = SafeBrush("FsTextPrimary", Brushes.White),
            Margin = new Thickness(0, 0, 0, 10),
        });

        foreach (var (k, v) in items)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyText = new TextBlock
            {
                Text = k,
                FontFamily = (FontFamily?)TryFindResource("FsFontSans"),
                FontSize = 12,
                Foreground = SafeBrush("FsTextSecondary", new SolidColorBrush(Color.FromRgb(0x9B, 0xA1, 0xAB))),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var valueText = new TextBlock
            {
                Text = v,
                FontFamily = (FontFamily?)TryFindResource("FsFontMono"),
                FontSize = 12,
                Foreground = SafeBrush("FsTextPrimary", Brushes.White),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            Grid.SetColumn(valueText, 1);
            row.Children.Add(keyText);
            row.Children.Add(valueText);
            stack.Children.Add(row);
        }

        card.Child = stack;
        return card;
    }

    private UIElement BuildProviderCard(IConverterProvider provider, ProviderAvailability availability)
    {
        var card = new Border
        {
            Background = SafeBrush("FsBgSurface", new SolidColorBrush(Color.FromRgb(0x1B, 0x1D, 0x22))),
            BorderBrush = SafeBrush("FsBorderHairline", new SolidColorBrush(Color.FromRgb(0x33, 0x38, 0x42))),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 12),
        };

        var stack = new StackPanel();

        // 1. 헤더 (공급자 명칭 + 상태 뱃지 우측 정렬)
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = provider.Capability.DisplayName,
            FontFamily = (FontFamily?)TryFindResource("FsFontSans"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = SafeBrush("FsTextPrimary", Brushes.White),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var (badgeText, badgeBgKey) = (provider.Capability.Status, availability.IsReady) switch
        {
            (ProviderStatus.ComingSoon, _) => ("개발 중", "FsTextTertiary"),
            (ProviderStatus.Disabled, _) => ("비활성", "FsTextTertiary"),
            (_, true) => ("준비됨", "FsAccentGreen"),
            _ => ("점검 필요", "FsAccentAmber"),
        };

        var badge = new Border
        {
            Background = SafeBrush(badgeBgKey, new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
        };

        Grid.SetColumn(badge, 1);
        headerGrid.Children.Add(titleText);
        headerGrid.Children.Add(badge);
        stack.Children.Add(headerGrid);

        // 2. 미세 구분선
        stack.Children.Add(new Border
        {
            Height = 1,
            Background = SafeBrush("FsBorderSubtle", new SolidColorBrush(Color.FromRgb(0x24, 0x27, 0x30))),
            Margin = new Thickness(0, 0, 0, 8),
        });

        // 3. 정형화된 입력/출력 격자 (Grid 컬럼 분리 정렬)
        var extGrid = new Grid();
        extGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        extGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        extGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        extGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 입력 형식 행
        var inputLabel = new TextBlock
        {
            Text = "입력 형식",
            FontFamily = (FontFamily?)TryFindResource("FsFontSans"),
            FontSize = 11,
            Foreground = SafeBrush("FsTextSecondary", new SolidColorBrush(Color.FromRgb(0x9B, 0xA1, 0xAB))),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 4),
        };
        var inputValue = new TextBlock
        {
            Text = string.Join(", ", provider.Capability.InputExtensions),
            FontFamily = (FontFamily?)TryFindResource("FsFontMono"),
            FontSize = 11,
            Foreground = SafeBrush("FsTextPrimary", Brushes.White),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 4),
        };
        Grid.SetRow(inputLabel, 0); Grid.SetColumn(inputLabel, 0);
        Grid.SetRow(inputValue, 0); Grid.SetColumn(inputValue, 1);
        extGrid.Children.Add(inputLabel);
        extGrid.Children.Add(inputValue);

        // 출력 형식 행
        var outputLabel = new TextBlock
        {
            Text = "출력 형식",
            FontFamily = (FontFamily?)TryFindResource("FsFontSans"),
            FontSize = 11,
            Foreground = SafeBrush("FsTextSecondary", new SolidColorBrush(Color.FromRgb(0x9B, 0xA1, 0xAB))),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 4),
        };
        var outputValue = new TextBlock
        {
            Text = string.Join(", ", provider.Capability.OutputExtensions),
            FontFamily = (FontFamily?)TryFindResource("FsFontMono"),
            FontSize = 11,
            Foreground = SafeBrush("FsAccentBlue", new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6))),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 4),
        };
        Grid.SetRow(outputLabel, 1); Grid.SetColumn(outputLabel, 0);
        Grid.SetRow(outputValue, 1); Grid.SetColumn(outputValue, 1);
        extGrid.Children.Add(outputLabel);
        extGrid.Children.Add(outputValue);

        stack.Children.Add(extGrid);

        // 4. 상태 안내 사유 (미비 시 경고 박스)
        if (!availability.IsReady && !string.IsNullOrEmpty(availability.Reason))
        {
            var warnBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xF5, 0x9E, 0x0B)),
                BorderBrush = SafeBrush("FsAccentAmber", new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B))),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new TextBlock
                {
                    Text = availability.Reason,
                    FontFamily = (FontFamily?)TryFindResource("FsFontSans"),
                    FontSize = 11,
                    Foreground = SafeBrush("FsAccentAmber", new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B))),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            stack.Children.Add(warnBox);
        }

        card.Child = stack;
        return card;
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await PopulateAsync();
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}

