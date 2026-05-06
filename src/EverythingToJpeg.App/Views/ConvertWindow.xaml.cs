using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EverythingToJpeg.Core;
using Wpf.Ui.Controls;

namespace EverythingToJpeg.App.Views;

public partial class ConvertWindow : FluentWindow
{
    private static readonly string DialogLogPath =
        Path.Combine(Path.GetTempPath(), "EverythingToJpeg_dialog.log");

    private readonly ConversionEngine _engine;
    private readonly ObservableCollection<ConvertFileEntry> _entries = new();
    private CancellationTokenSource? _cts;
    private OutputLocation _outputMode = OutputLocation.SubfolderBesideSource;
    private NameCollision _conflictRule = NameCollision.AppendNumber;

    public ConvertWindow(ConversionEngine engine, IReadOnlyList<string> initialFiles)
    {
        _engine = engine;
        InitializeComponent();
        FilesList.ItemsSource = _entries;
        AddFiles(initialFiles);
    }

    private static void DiagLog(string line)
    {
        try { File.AppendAllText(DialogLogPath,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {line}{Environment.NewLine}"); }
        catch { }
    }

    // ============== Files ==============

    private void AddFiles(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(_entries.Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            if (!File.Exists(p)) continue;
            if (existing.Contains(p)) continue;

            var entry = ConvertFileEntry.From(p, _engine);
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
            Filter = "지원 파일|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff;*.webp;*.avif;*.heic;*.heif;*.psd;*.dng;*.nef;*.cr2;*.cr3;*.arw;*.raf;*.orf;*.rw2;*.srw;*.pef;*.pdf;*.docx;*.doc;*.html;*.htm;*.hwp;*.hwpx|모든 파일|*.*",
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
        if (sender is FrameworkElement fe && fe.DataContext is ConvertFileEntry entry)
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
                foreach (var f in Directory.EnumerateFiles(p, "*", SearchOption.TopDirectoryOnly))
                    yield return f;
        }
    }

    // ============== Segments ==============

    private void OnQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityValueText is null) return;
        QualityValueText.Text = ((int)e.NewValue).ToString(CultureInfo.InvariantCulture);
    }

    private void OnOutputSegmentClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;
        OutputSubBtn.IsChecked = clicked == OutputSubBtn;
        OutputSameBtn.IsChecked = clicked == OutputSameBtn;
        OutputCustomBtn.IsChecked = clicked == OutputCustomBtn;

        _outputMode = (clicked.Tag as string) switch
        {
            "Same" => OutputLocation.SameFolderAsSource,
            "Custom" => OutputLocation.Custom,
            _ => OutputLocation.SubfolderBesideSource,
        };
        CustomFolderRow.Visibility = _outputMode == OutputLocation.Custom
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnConflictSegmentClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;
        ConflictRenameBtn.IsChecked = clicked == ConflictRenameBtn;
        ConflictReplaceBtn.IsChecked = clicked == ConflictReplaceBtn;
        ConflictSkipBtn.IsChecked = clicked == ConflictSkipBtn;
        _conflictRule = (clicked.Tag as string) switch
        {
            "Skip" => NameCollision.Skip,
            "Replace" => NameCollision.Overwrite,
            _ => NameCollision.AppendNumber,
        };
    }

    private ConvertOptions BuildOptions()
    {
        var opts = new ConvertOptions
        {
            Quality = (int)QualitySlider.Value,
            PdfDpi = (int)DpiSlider.Value,
            FlattenTransparency = FlattenCheckBox.IsChecked == true,
            OutputLocation = _outputMode,
            OnCollision = _conflictRule,
        };
        if (_outputMode == OutputLocation.Custom)
            opts.CustomOutputDirectory = CustomFolderTextBox.Text;
        if (int.TryParse(MaxLongEdgeTextBox.Text, out var maxEdge) && maxEdge > 0)
            opts.MaxLongEdgePixels = maxEdge;
        return opts;
    }

    // ============== Convert ==============

    private async void OnConvertClick(object sender, RoutedEventArgs e)
    {
        DiagLog($"OnConvertClick: entries={_entries.Count}");
        if (_entries.Count == 0)
        {
            ShowInfo("변환할 파일이 없습니다.");
            return;
        }

        ConvertButton.IsEnabled = false;
        CancelButton.Content = "취소";
        ProgressStatusText.Text = "준비 중…";
        _cts = new CancellationTokenSource();

        ConvertOptions options;
        try
        {
            options = BuildOptions();
            DiagLog($"  options: Quality={options.Quality} OutputLocation={options.OutputLocation} Custom={options.CustomOutputDirectory} Collision={options.OnCollision}");
        }
        catch (Exception ex)
        {
            DiagLog("  BuildOptions threw: " + ex);
            ProgressStatusText.Text = "옵션 처리 오류: " + ex.Message;
            ConvertButton.IsEnabled = true;
            CancelButton.Content = "닫기";
            _cts = null;
            return;
        }

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
            DiagLog($"  starting ConvertManyAsync, {sources.Count} files");
            var results = await _engine.ConvertManyAsync(sources, options, reporter, _cts.Token);
            DiagLog($"  finished, {results.Count} results");
            ApplyResults(results);
            ProgressStatusText.Text = SummarizeResults(results);
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "변환이 취소되었습니다.";
        }
        catch (Exception ex)
        {
            DiagLog("  EXCEPTION: " + ex);
            ProgressStatusText.Text = "오류: " + ex.Message;
            MessageBox.Show(this, "변환 중 오류:\n\n" + ex.Message + "\n\n로그: " + DialogLogPath,
                "EverythingToJpeg", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void ShowInfo(string message)
        => MessageBox.Show(this, message, "EverythingToJpeg",
            MessageBoxButton.OK, MessageBoxImage.Information);
}

public sealed class ConvertFileEntry : INotifyPropertyChanged
{
    private string _state = "대기";
    private bool _isFailed;
    private ImageSource? _thumbnail;

    public required string Path { get; init; }
    public required string FileName { get; init; }
    public required string SubText { get; init; }
    public required string FormatLabel { get; init; }
    public required Brush FormatBrush { get; init; }

    public string State
    {
        get => _state;
        set { _state = value; Raise(nameof(State)); }
    }

    public bool IsFailed
    {
        get => _isFailed;
        set { _isFailed = value; Raise(nameof(IsFailed)); }
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; Raise(nameof(Thumbnail)); Raise(nameof(ShowFormatLabel)); }
    }

    public Visibility ShowFormatLabel => _thumbnail is null ? Visibility.Visible : Visibility.Collapsed;

    public static ConvertFileEntry From(string path, ConversionEngine engine)
    {
        var ext = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        var (label, brushKey) = FormatPalette.For(ext);

        string handler;
        if (engine.Providers.TryGetForFile(path, out var provider) && provider is not null)
            handler = provider.Capability.DisplayName;
        else
            handler = "지원되지 않음";

        long size = 0;
        try { size = new FileInfo(path).Length; } catch { }
        var sub = $".{ext}  ·  {handler}  ·  {MainWindow.HumanizeBytes(size)}";

        return new ConvertFileEntry
        {
            Path = path,
            FileName = System.IO.Path.GetFileName(path),
            SubText = sub,
            FormatLabel = label,
            FormatBrush = (Brush)Application.Current.FindResource(brushKey),
        };
    }

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
                Application.Current.Dispatcher.Invoke(() => Thumbnail = bmp);
            }
        }
        catch { }
    });

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
