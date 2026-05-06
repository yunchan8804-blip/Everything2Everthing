using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EverythingToJpeg.Core;
using EverythingToJpeg.Core.Providers;
using Wpf.Ui.Controls;

namespace EverythingToJpeg.App.Views;

public partial class DiagnoseWindow : FluentWindow
{
    private readonly ConversionEngine _engine;

    public DiagnoseWindow(ConversionEngine engine)
    {
        _engine = engine;
        InitializeComponent();
        Loaded += async (_, _) => await PopulateAsync();
    }

    private async Task PopulateAsync()
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

    private static UIElement BuildSection(string title, IEnumerable<(string Key, string Value)> items)
    {
        var card = new CardControl { Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 12) };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        });
        foreach (var (k, v) in items)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var keyText = new TextBlock { Text = k, FontSize = 12, Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush") };
            var valueText = new TextBlock { Text = v, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(valueText, 1);
            row.Children.Add(keyText);
            row.Children.Add(valueText);
            stack.Children.Add(row);
        }
        card.Content = stack;
        return card;
    }

    private UIElement BuildProviderCard(IConverterProvider provider, ProviderAvailability availability)
    {
        var card = new CardControl { Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 12) };
        var stack = new StackPanel();
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock
        {
            Text = provider.Capability.DisplayName,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
        });
        var (badgeText, brushKey) = (provider.Capability.Status, availability.IsReady) switch
        {
            (ProviderStatus.ComingSoon, _) => ("개발 중", "BadgeMutedBrush"),
            (ProviderStatus.Disabled, _) => ("비활성", "BadgeMutedBrush"),
            (_, true) => ("준비됨", "BadgeReadyBrush"),
            _ => ("점검 필요", "BadgeWarnBrush"),
        };
        header.Children.Add(new Border
        {
            Background = (Brush)Application.Current.FindResource(brushKey),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock { Text = badgeText, FontSize = 11, Foreground = Brushes.White },
        });
        stack.Children.Add(header);

        stack.Children.Add(new TextBlock
        {
            Text = $"확장자: {string.Join(", ", provider.Capability.Extensions)}",
            FontSize = 11,
            Foreground = (Brush)Application.Current.FindResource("TextFillColorTertiaryBrush"),
            Margin = new Thickness(0, 4, 0, 0),
        });
        if (!availability.IsReady && !string.IsNullOrEmpty(availability.Reason))
        {
            stack.Children.Add(new TextBlock
            {
                Text = availability.Reason,
                FontSize = 11,
                Foreground = (Brush)Application.Current.FindResource("BadgeWarnBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }
        card.Content = stack;
        return card;
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await PopulateAsync();
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
