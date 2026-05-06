using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EverythingToJpeg.Core;
using EverythingToJpeg.Core.Providers;
using Wpf.Ui.Controls;

namespace EverythingToJpeg.App.Views;

public partial class ConvertWindow : FluentWindow
{
    private readonly ConversionEngine _engine;
    private readonly ObservableCollection<FileEntry> _entries = new();
    private CancellationTokenSource? _cts;

    public ConvertWindow(ConversionEngine engine, IReadOnlyList<string> initialFiles)
    {
        _engine = engine;
        InitializeComponent();

        FilesList.ItemsSource = _entries;
        FilesList.ItemTemplate = (DataTemplate)CreateFileEntryTemplate();

        AddFiles(initialFiles);

        OutputModeCombo.SelectionChanged += (_, _) =>
            CustomFolderRow.Visibility = OutputModeCombo.SelectedIndex == 2
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(_entries.Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            if (!File.Exists(p)) continue;
            if (existing.Contains(p)) continue;

            var entry = new FileEntry(p, _engine);
            _entries.Add(entry);
            _ = entry.LoadThumbnailAsync();
        }
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        FilesSummaryText.Text = _entries.Count == 0
            ? "비어 있음 — 파일을 끌어다 놓거나 추가하세요"
            : $"{_entries.Count}개 파일";
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFilesDropped(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        AddFiles(ExpandPaths(paths));
    }

    private void OnAddFilesClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "추가할 파일 선택",
            Filter = "지원 파일|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff;*.webp;*.avif;*.heic;*.heif;*.psd;*.dng;*.nef;*.cr2;*.cr3;*.arw;*.raf;*.orf;*.rw2;*.srw;*.pef;*.pdf;*.docx;*.doc|모든 파일|*.*",
        };
        if (dlg.ShowDialog(this) == true) AddFiles(dlg.FileNames);
    }

    private void OnClearFilesClick(object sender, RoutedEventArgs e)
    {
        _entries.Clear();
        UpdateSummary();
    }

    private void OnRemoveEntry(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FileEntry entry)
        {
            _entries.Remove(entry);
            UpdateSummary();
        }
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "출력 폴더 선택" };
        if (dlg.ShowDialog(this) == true)
            CustomFolderTextBox.Text = dlg.FolderName;
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            if (File.Exists(p)) yield return p;
            else if (Directory.Exists(p))
            {
                foreach (var f in Directory.EnumerateFiles(p, "*", SearchOption.TopDirectoryOnly))
                    yield return f;
            }
        }
    }

    private async void OnConvertClick(object sender, RoutedEventArgs e)
    {
        if (_entries.Count == 0)
        {
            ShowInfo("변환할 파일이 없습니다.");
            return;
        }

        ConvertButton.IsEnabled = false;
        CancelButton.Content = "취소";
        _cts = new CancellationTokenSource();

        var options = BuildOptions();
        var reporter = new Progress<ConvertProgress>(p =>
        {
            var overall = p.Total == 0 ? 0 : (p.Index + p.FileProgress) / p.Total;
            OverallProgress.Value = Math.Clamp(overall, 0, 1);
            ProgressStatusText.Text = $"{Math.Min(p.Index + 1, p.Total)} / {p.Total} — {Path.GetFileName(p.CurrentPath)}";
            UpdateEntryProgress(p);
        });

        try
        {
            var sources = _entries.Select(en => en.Path).ToList();
            var results = await _engine.ConvertManyAsync(sources, options, reporter, _cts.Token);
            ApplyResults(results);
            ProgressStatusText.Text = SummarizeResults(results);
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "변환이 취소되었습니다.";
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = "오류: " + ex.Message;
        }
        finally
        {
            ConvertButton.IsEnabled = true;
            CancelButton.Content = "닫기";
            _cts = null;
        }
    }

    private void UpdateEntryProgress(ConvertProgress p)
    {
        if (p.Index >= _entries.Count) return;
        for (var i = 0; i < _entries.Count; i++)
        {
            if (i < p.Index) _entries[i].State = "완료";
            else if (i == p.Index) _entries[i].State = "변환 중…";
            else _entries[i].State = "대기";
        }
    }

    private void ApplyResults(IReadOnlyList<ConvertResult> results)
    {
        foreach (var result in results)
        {
            var entry = _entries.FirstOrDefault(e => string.Equals(e.Path, result.SourcePath, StringComparison.OrdinalIgnoreCase));
            if (entry is null) continue;
            entry.State = result.Status switch
            {
                ConvertStatus.Success => $"성공 ({result.OutputPaths.Count}개)",
                ConvertStatus.Skipped => "건너뜀",
                ConvertStatus.Failed => "실패: " + result.Message,
                _ => entry.State,
            };
            entry.IsFailed = result.Status == ConvertStatus.Failed;
        }
    }

    private static string SummarizeResults(IReadOnlyList<ConvertResult> results)
    {
        var success = results.Count(r => r.Status == ConvertStatus.Success);
        var skipped = results.Count(r => r.Status == ConvertStatus.Skipped);
        var failed = results.Count(r => r.Status == ConvertStatus.Failed);
        var outputs = results.Sum(r => r.OutputPaths.Count);
        return $"성공 {success}개 (출력 {outputs}), 건너뜀 {skipped}, 실패 {failed}";
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_cts is { } cts) { cts.Cancel(); return; }
        Close();
    }

    private ConvertOptions BuildOptions()
    {
        var opts = new ConvertOptions
        {
            Quality = (int)QualitySlider.Value,
            PdfDpi = (int)DpiSlider.Value,
            FlattenTransparency = FlattenCheckBox.IsChecked == true,
        };
        opts.OutputLocation = OutputModeCombo.SelectedIndex switch
        {
            1 => OutputLocation.SameFolderAsSource,
            2 => OutputLocation.Custom,
            _ => OutputLocation.SubfolderBesideSource,
        };
        if (opts.OutputLocation == OutputLocation.Custom)
            opts.CustomOutputDirectory = CustomFolderTextBox.Text;

        opts.OnCollision = CollisionCombo.SelectedIndex switch
        {
            1 => NameCollision.Overwrite,
            2 => NameCollision.Skip,
            _ => NameCollision.AppendNumber,
        };

        if (int.TryParse(MaxLongEdgeTextBox.Text, out var maxEdge) && maxEdge > 0)
            opts.MaxLongEdgePixels = maxEdge;

        return opts;
    }

    private void ShowInfo(string message)
        => MessageBox.Show(this, message, "EverythingToJpeg",
            MessageBoxButton.OK, MessageBoxImage.Information);

    private object CreateFileEntryTemplate()
    {
        const string xaml = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
          <Border Margin="0,0,0,6" Padding="12,8" CornerRadius="6"
                  Background="{DynamicResource SubtleFillColorTransparentBrush}"
                  BorderBrush="{DynamicResource ControlStrokeColorDefaultBrush}"
                  BorderThickness="1">
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="44"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
              </Grid.ColumnDefinitions>
              <Border Width="40" Height="40" CornerRadius="4"
                      Background="{DynamicResource ControlFillColorDefaultBrush}">
                <Image Source="{Binding Thumbnail}" Stretch="UniformToFill"/>
              </Border>
              <StackPanel Grid.Column="1" Margin="12,0,8,0" VerticalAlignment="Center">
                <TextBlock Text="{Binding FileName}" FontWeight="SemiBold"
                           TextTrimming="CharacterEllipsis"/>
                <TextBlock Text="{Binding SubText}" FontSize="11"
                           Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                           TextTrimming="CharacterEllipsis"/>
                <TextBlock Text="{Binding State}" FontSize="11" Margin="0,2,0,0">
                  <TextBlock.Style>
                    <Style TargetType="TextBlock">
                      <Setter Property="Foreground" Value="{DynamicResource TextFillColorTertiaryBrush}"/>
                      <Style.Triggers>
                        <DataTrigger Binding="{Binding IsFailed}" Value="True">
                          <Setter Property="Foreground" Value="#F87171"/>
                        </DataTrigger>
                      </Style.Triggers>
                    </Style>
                  </TextBlock.Style>
                </TextBlock>
              </StackPanel>
              <ui:Button Grid.Column="2" Icon="{ui:SymbolIcon Dismiss20}"
                         Appearance="Transparent" Click="OnRemoveEntry"
                         ToolTip="목록에서 제거"/>
            </Grid>
          </Border>
        </DataTemplate>
        """;
        return System.Windows.Markup.XamlReader.Parse(xaml);
    }
}

public sealed class FileEntry : System.ComponentModel.INotifyPropertyChanged
{
    private readonly ConversionEngine _engine;
    private string _state = "대기";
    private bool _isFailed;
    private ImageSource? _thumbnail;

    public FileEntry(string path, ConversionEngine engine)
    {
        Path = path;
        _engine = engine;
    }

    public string Path { get; }
    public string FileName => System.IO.Path.GetFileName(Path);

    public string SubText
    {
        get
        {
            var ext = System.IO.Path.GetExtension(Path).ToLowerInvariant();
            string handler;
            if (_engine.Providers.TryGetForFile(Path, out var provider) && provider is not null)
                handler = provider.Capability.DisplayName;
            else
                handler = "지원되지 않음";
            try
            {
                var size = new FileInfo(Path).Length;
                return $"{ext}  ·  {handler}  ·  {FormatBytes(size)}";
            }
            catch
            {
                return $"{ext}  ·  {handler}";
            }
        }
    }

    public string State { get => _state; set { _state = value; Raise(nameof(State)); } }
    public bool IsFailed { get => _isFailed; set { _isFailed = value; Raise(nameof(IsFailed)); } }
    public ImageSource? Thumbnail { get => _thumbnail; set { _thumbnail = value; Raise(nameof(Thumbnail)); } }

    public Task LoadThumbnailAsync() => Task.Run(() =>
    {
        try
        {
            var ext = System.IO.Path.GetExtension(Path).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(Path);
                bmp.DecodePixelWidth = 80;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                System.Windows.Application.Current.Dispatcher.Invoke(() => Thumbnail = bmp);
            }
        }
        catch { }
    });

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.#} {units[unit]}";
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string n) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
}
