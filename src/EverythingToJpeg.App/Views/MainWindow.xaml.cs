using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using EverythingToJpeg.App.Shell;
using EverythingToJpeg.Core.Providers;
using Wpf.Ui.Controls;

namespace EverythingToJpeg.App.Views;

public partial class MainWindow : FluentWindow, INotifyPropertyChanged
{
    private bool _isDraggingOver;
    public bool IsDraggingOver
    {
        get => _isDraggingOver;
        set { _isDraggingOver = value; OnPropertyChanged(); }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await PopulateAsync();
    }

    private async Task PopulateAsync()
    {
        var engine = ((App)Application.Current).Engine;
        ProvidersList.Items.Clear();
        foreach (var provider in engine.Providers.All)
        {
            var availability = await provider.CheckAvailabilityAsync();
            ProvidersList.Items.Add(BuildProviderRow(provider.Capability, availability));
        }
    }

    private static UIElement BuildProviderRow(ProviderCapability cap, ProviderAvailability availability)
    {
        var (badge, badgeKey) = cap.Status switch
        {
            ProviderStatus.Available => availability.IsReady
                ? ("준비됨", "BadgeReadyBrush")
                : ("점검 필요", "BadgeWarnBrush"),
            ProviderStatus.Preview => ("프리뷰", "BadgeInfoBrush"),
            ProviderStatus.RequiresExternal => availability.IsReady
                ? ("외부 도구 감지됨", "BadgeReadyBrush")
                : ("외부 도구 필요", "BadgeWarnBrush"),
            ProviderStatus.ComingSoon => ("개발 중", "BadgeMutedBrush"),
            _ => ("비활성", "BadgeMutedBrush"),
        };

        var card = new CardControl
        {
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 8),
        };

        var stack = new StackPanel();

        var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
        headerStack.Children.Add(new TextBlock
        {
            Text = cap.DisplayName,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        headerStack.Children.Add(new Border
        {
            Background = (Brush)Application.Current.FindResource(badgeKey),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock { Text = badge, FontSize = 11, Foreground = Brushes.White },
        });
        stack.Children.Add(headerStack);

        stack.Children.Add(new TextBlock
        {
            Text = cap.Summary,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12,
            Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });

        stack.Children.Add(new TextBlock
        {
            Text = "확장자: " + string.Join(", ", cap.Extensions),
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

        if (cap.ExternalDependencies.Count > 0)
        {
            foreach (var dep in cap.ExternalDependencies)
            {
                var line = new TextBlock
                {
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                };
                line.Inlines.Add(new Run($"• {dep.Name} — {dep.Description} "));
                if (!string.IsNullOrEmpty(dep.DownloadUrl))
                {
                    var hl = new Hyperlink(new Run(dep.DownloadUrl)) { NavigateUri = new Uri(dep.DownloadUrl) };
                    hl.RequestNavigate += (_, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = e.Uri.AbsoluteUri,
                                UseShellExecute = true
                            });
                        }
                        catch { }
                        e.Handled = true;
                    };
                    line.Inlines.Add(hl);
                }
                stack.Children.Add(line);
            }
        }

        if (!string.IsNullOrEmpty(cap.RoadmapNote))
        {
            stack.Children.Add(new TextBlock
            {
                Text = "로드맵: " + cap.RoadmapNote,
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                Foreground = (Brush)Application.Current.FindResource("TextFillColorTertiaryBrush"),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        card.Content = stack;
        return card;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            IsDraggingOver = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        IsDraggingOver = false;
    }

    private void OnFilesDropped(object sender, DragEventArgs e)
    {
        IsDraggingOver = false;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        OpenConvertWindow(paths);
    }

    private void OnPickFilesClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "변환할 파일 선택",
            Multiselect = true,
            Filter = "지원 파일|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff;*.webp;*.avif;*.heic;*.heif;*.psd;*.dng;*.nef;*.cr2;*.cr3;*.arw;*.raf;*.orf;*.rw2;*.srw;*.pef;*.pdf;*.docx;*.doc|모든 파일|*.*",
        };
        if (dlg.ShowDialog(this) == true)
        {
            OpenConvertWindow(dlg.FileNames);
        }
    }

    private void OpenConvertWindow(string[] paths)
    {
        var files = ExpandPaths(paths);
        if (files.Count == 0) return;
        var window = new ConvertWindow(((App)Application.Current).Engine, files) { Owner = this };
        window.ShowDialog();
    }

    private static List<string> ExpandPaths(IEnumerable<string> paths)
    {
        var list = new List<string>();
        foreach (var p in paths)
        {
            try
            {
                if (File.Exists(p)) list.Add(p);
                else if (Directory.Exists(p))
                    list.AddRange(Directory.EnumerateFiles(p, "*", SearchOption.TopDirectoryOnly));
            }
            catch { }
        }
        return list;
    }

    private async void OnRegisterClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ContextMenuRegistrar.Register(((App)Application.Current).Engine);
            ShowToast("컨텍스트 메뉴를 등록했습니다.\n파일 위에서 우클릭 → \"추가 옵션 표시\"에서 보입니다.");
            await PopulateAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("등록 중 오류: " + ex.Message, "EverythingToJpeg",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnUnregisterClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ContextMenuRegistrar.Unregister(((App)Application.Current).Engine);
            ShowToast("컨텍스트 메뉴를 해제했습니다.");
            await PopulateAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("해제 중 오류: " + ex.Message, "EverythingToJpeg",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDiagnoseClick(object sender, RoutedEventArgs e)
    {
        var window = new DiagnoseWindow(((App)Application.Current).Engine) { Owner = this };
        window.ShowDialog();
    }

    private void ShowToast(string message)
    {
        MessageBox.Show(this, message, "EverythingToJpeg",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
