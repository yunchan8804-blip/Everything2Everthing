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
using Everything2Everything.App.Shell;
using Everything2Everything.Core;

namespace Everything2Everything.App.Views;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ObservableCollection<QueueItem> _activeQueue = new();
    private readonly ObservableCollection<DateGroup> _pastResults = new();
    private CancellationTokenSource? _cts;
    private NameCollision _conflictRule = NameCollision.AppendNumber;

    public string? SelectedOutputExtension { get; set; } = ".jpg";

    public ICommand AddFilesCommand { get; }
    public ICommand ProcessQueueCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand RefreshCommand { get; }

    public MainWindow() : this(null) { }

    public MainWindow(IReadOnlyList<string>? initialFiles)
    {
        AddFilesCommand = new RelayCommand(_ => PickAndAddFiles());
        ProcessQueueCommand = new RelayCommand(_ => OnProcessQueueClick(this, new RoutedEventArgs()),
            _ => _activeQueue.Count > 0 && _cts is null);
        CloseCommand = new RelayCommand(_ => Close());
        RefreshCommand = new RelayCommand(_ => ApplyAppDataStats());

        InitializeComponent();

        ActiveQueueList.ItemsSource = _activeQueue;
        PastResultsList.ItemsSource = _pastResults;

        InitializeOutputFormats();
        LoadHistory();
        UpdateBadges();
        UpdateProcessQueueButton();

        if (initialFiles is { Count: > 0 })
        {
            AddToQueue(initialFiles);
            ShowTab("Active");
        }
        else
        {
            ShowTab("Past");
        }

        UpdateActiveQueueVisibility();
        ApplyAppDataStats();

        _ = RefreshCapabilityStatusAsync();
    }

    private async Task RefreshCapabilityStatusAsync()
    {
        var engine = ((App)Application.Current).Engine;
        var notReady = new List<string>();
        foreach (var p in engine.Providers.All)
        {
            if (p.Capability.Status == Everything2Everything.Core.Providers.ProviderStatus.RequiresExternal)
            {
                var availability = await p.CheckAvailabilityAsync();
                if (!availability.IsReady)
                    notReady.Add(p.Capability.DisplayName);
            }
        }

        if (notReady.Count == 0)
        {
            CapabilityStatusText.Visibility = Visibility.Collapsed;
            return;
        }

        CapabilityStatusText.Text = $"⚠ {notReady.Count}개 형식이 외부 도구를 기다립니다 (Diagnose 참조)";
        CapabilityStatusText.Visibility = Visibility.Visible;
    }

    private void PickAndAddFiles()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "변환할 파일 추가",
            Filter = "지원 파일|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff;*.webp;*.avif;*.heic;*.heif;*.psd;*.dng;*.nef;*.cr2;*.cr3;*.arw;*.raf;*.orf;*.rw2;*.srw;*.pef;*.pdf;*.docx;*.doc;*.html;*.htm;*.hwp;*.hwpx|모든 파일|*.*",
        };
        if (dlg.ShowDialog(this) == true)
        {
            AddToQueue(dlg.FileNames);
            ShowTab("Active");
        }
    }

    // ============== Tabs ==============

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && tb.Tag is string tag)
        {
            ShowTab(tag);
        }
    }

    private void ShowTab(string tag)
    {
        TabActiveBtn.IsChecked = tag == "Active";
        TabPastBtn.IsChecked = tag == "Past";

        ActiveQueueView.Visibility = tag == "Active" ? Visibility.Visible : Visibility.Collapsed;
        PastResultsView.Visibility = tag == "Past" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ============== Drag & Drop ==============

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        DropHintOverlay.Visibility = e.Effects == DragDropEffects.Copy
            ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DropHintOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnFilesDropped(object sender, DragEventArgs e)
    {
        DropHintOverlay.Visibility = Visibility.Collapsed;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        AddToQueue(ExpandPaths(paths));
        ShowTab("Active");
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

    private void AddToQueue(IEnumerable<string> paths)
    {
        var wasEmpty = _activeQueue.Count == 0;
        var existing = new HashSet<string>(_activeQueue.Select(q => q.SourcePath), StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!File.Exists(path) || existing.Contains(path)) continue;
            _activeQueue.Add(QueueItem.FromPath(path));
        }
        UpdateBadges();
        UpdateProcessQueueButton();
        UpdateActiveQueueVisibility();
        RefreshAvailableOutputFormats();

        if (wasEmpty && _activeQueue.Count > 0 && _selectedPreviewItem is null)
        {
            _selectedPreviewItem = _activeQueue[0];
            _ = LoadPreviewAsync(_selectedPreviewItem);
        }
    }

    private void OnRemoveQueueItem(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is QueueItem item)
        {
            _activeQueue.Remove(item);
            UpdateBadges();
            UpdateProcessQueueButton();
            UpdateActiveQueueVisibility();
            RefreshAvailableOutputFormats();
        }
    }

    private void UpdateActiveQueueVisibility()
    {
        var hasItems = _activeQueue.Count > 0;
        DropZoneEmpty.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        ActiveQueueScroll.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateBadges()
    {
        TabActiveBadge.Text = _activeQueue.Count.ToString(CultureInfo.InvariantCulture);
        var count = _pastResults.Sum(g => g.Entries.Count);
        TabPastBadge.Text = count.ToString(CultureInfo.InvariantCulture);
    }

    private void UpdateProcessQueueButton()
    {
        var count = _activeQueue.Count;
        if (_cts is not null)
        {
            ProcessQueueButton.Content = $"Processing… ({count} files)";
            ProcessQueueButton.IsEnabled = false;
        }
        else if (count == 0)
        {
            ProcessQueueButton.Content = "Idle — drop files to begin";
            ProcessQueueButton.IsEnabled = false;
        }
        else
        {
            ProcessQueueButton.Content = $"Process Queue ({count})";
            ProcessQueueButton.IsEnabled = true;
        }
    }

    // ============== Sidebar inputs ==============

    private void OnQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityValueText is null) return;
        QualityValueText.Text = $"{(int)e.NewValue}%";
    }

    private void OnConflictSegmentClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;
        ConflictSkipBtn.IsChecked = clicked == ConflictSkipBtn;
        ConflictRenameBtn.IsChecked = clicked == ConflictRenameBtn;
        ConflictReplaceBtn.IsChecked = clicked == ConflictReplaceBtn;
        _conflictRule = (clicked.Tag as string) switch
        {
            "Skip" => NameCollision.Skip,
            "Replace" => NameCollision.Overwrite,
            _ => NameCollision.AppendNumber,
        };
    }

    private void OnPickOutputFolderClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "출력 폴더 선택" };
        if (dlg.ShowDialog(this) == true)
            OutputPathTextBox.Text = dlg.FolderName;
    }

    private ConvertOptions BuildOptions()
    {
        var opts = new ConvertOptions
        {
            OnCollision = _conflictRule,
        };
        opts.Jpeg.Quality = (int)QualitySlider.Value;
        opts.Webp.Quality = (int)QualitySlider.Value;
        opts.Avif.Quality = Math.Clamp((int)QualitySlider.Value - 30, 1, 100);

        var custom = OutputPathTextBox.Text?.Trim();
        if (!string.IsNullOrEmpty(custom))
        {
            opts.OutputLocation = OutputLocation.Custom;
            opts.CustomOutputDirectory = custom;
        }
        else
        {
            opts.OutputLocation = OutputLocation.SubfolderBesideSource;
        }

        return opts;
    }

    // ============== Process queue ==============

    // ============== Preview ==============

    private QueueItem? _selectedPreviewItem;
    private string? _selectedPreviewPath;
    private CancellationTokenSource? _previewCts;

    private async void OnQueueRowClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src && IsInsideButton(src)) return;
        if (sender is not FrameworkElement fe || fe.Tag is not QueueItem item) return;

        _selectedPreviewItem = item;
        await LoadPreviewAsync(item);
        e.Handled = true;
    }

    private async void OnPastRowClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src && IsInsideButton(src)) return;
        if (sender is not FrameworkElement fe || fe.Tag is not HistoryRow row) return;

        if (row.SourcePath == "<demo>")
        {
            SetPreviewMeta(row.FileName, row.SourcePath, row.FormatLabel, row.SizeText);
            ShowPreviewReason("샘플 데모 항목입니다. 원본 파일이 없으므로 미리보기를 만들 수 없습니다.");
            _selectedPreviewPath = null;
        }
        else if (!File.Exists(row.SourcePath))
        {
            SetPreviewMeta(row.FileName, row.SourcePath, row.FormatLabel, row.SizeText);
            ShowPreviewReason("원본 파일을 찾을 수 없습니다. 파일이 이동·삭제되었을 수 있습니다.");
            _selectedPreviewPath = null;
        }
        else
        {
            _selectedPreviewItem = null;
            _selectedPreviewPath = row.SourcePath;
            await LoadPreviewByPathAsync(row.SourcePath, row.FileName, row.FormatLabel, row.SizeText);
        }
        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is Button) return true;
            node = System.Windows.Media.VisualTreeHelper.GetParent(node)
                   ?? (node is FrameworkElement fe ? fe.Parent : null);
        }
        return false;
    }

    private Task LoadPreviewAsync(QueueItem item)
    {
        _selectedPreviewPath = item.SourcePath;
        return LoadPreviewByPathAsync(item.SourcePath, item.FileName, item.FormatLabel, item.SizeText);
    }

    private async Task LoadPreviewByPathAsync(string sourcePath, string fileName, string formatLabel, string sizeText)
    {
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;

        SetPreviewMeta(fileName, sourcePath, formatLabel, sizeText);
        ShowPreviewLoading();

        try
        {
            var result = await PreviewService.CreateAsync(sourcePath, 720, token);
            if (token.IsCancellationRequested) return;

            PreviewLoading.Visibility = Visibility.Collapsed;

            if (result.Image is not null)
            {
                PreviewImage.Source = result.Image;
                PreviewImage.Visibility = Visibility.Visible;
            }
            else
            {
                PreviewReasonText.Text = result.Reason ?? "미리보기를 생성하지 못했습니다.";
                PreviewReason.Visibility = Visibility.Visible;
            }

            PreviewDimText.Text = result.Dimensions ?? "—";
            PreviewPageText.Text = result.PageCount?.ToString() ?? "—";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            PreviewLoading.Visibility = Visibility.Collapsed;
            ShowPreviewReason("미리보기 오류: " + ex.Message);
        }
    }

    private void SetPreviewMeta(string fileName, string filePath, string formatLabel, string sizeText)
    {
        PreviewFileName.Text = fileName;
        PreviewFilePath.Text = filePath;
        PreviewFormatText.Text = formatLabel;
        PreviewSizeText.Text = sizeText;
        PreviewDimText.Text = "—";
        PreviewPageText.Text = "—";
    }

    private void ShowPreviewLoading()
    {
        PreviewEmpty.Visibility = Visibility.Collapsed;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewReason.Visibility = Visibility.Collapsed;
        PreviewLoading.Visibility = Visibility.Visible;
    }

    private void ShowPreviewReason(string reason)
    {
        PreviewEmpty.Visibility = Visibility.Collapsed;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewLoading.Visibility = Visibility.Collapsed;
        PreviewReasonText.Text = reason;
        PreviewReason.Visibility = Visibility.Visible;
    }

    private void OnPreviewOpenFolder(object sender, RoutedEventArgs e)
    {
        var path = _selectedPreviewPath ?? _selectedPreviewItem?.SourcePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch { }
    }

    // ============== Export Log ==============

    private void OnExportLogClick(object sender, RoutedEventArgs e)
    {
        if (_pastResults.Count == 0)
        {
            MessageBox.Show(this, "저장할 이력이 없습니다.", "Everything2Everything",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Log",
            FileName = $"Everything2Everything-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExt = ".csv",
            Filter = "CSV (*.csv)|*.csv|JSON (*.json)|*.json",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            if (dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                ExportJson(dlg.FileName);
            else
                ExportCsv(dlg.FileName);

            MessageBox.Show(this, "저장되었습니다:\n" + dlg.FileName, "Everything2Everything",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "저장 중 오류: " + ex.Message, "Everything2Everything",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportCsv(string path)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date,Format,FileName,SourcePath,Size,Savings,Meta");
        foreach (var group in _pastResults)
            foreach (var row in group.Entries)
                sb.AppendLine(string.Join(",",
                    EscapeCsv(group.DateTitle),
                    EscapeCsv(row.FormatLabel),
                    EscapeCsv(row.FileName),
                    EscapeCsv(row.SourcePath),
                    EscapeCsv(row.SizeText),
                    EscapeCsv(row.SavingsText),
                    EscapeCsv(row.MetaLine)));
        File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
    }

    private void ExportJson(string path)
    {
        var data = _pastResults.Select(g => new
        {
            date = g.DateTitle,
            sessionSavings = HumanizeBytes(g.SessionSavingsBytes),
            entries = g.Entries.Select(r => new
            {
                format = r.FormatLabel,
                fileName = r.FileName,
                sourcePath = r.SourcePath,
                size = r.SizeText,
                savings = r.SavingsText,
                meta = r.MetaLine,
            }),
        });
        File.WriteAllText(path,
            System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            System.Text.Encoding.UTF8);
    }

    private static string EscapeCsv(string? s)
    {
        s ??= "";
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private async void OnProcessQueueClick(object sender, RoutedEventArgs e)
    {
        if (_activeQueue.Count == 0) return;

        var snapshot = _activeQueue.ToList();
        foreach (var item in snapshot) item.SetPending();

        _cts = new CancellationTokenSource();
        UpdateProcessQueueButton();
        ShowProcessingProgress(snapshot.Count);

        var engine = ((App)Application.Current).Engine;
        var options = BuildOptions();

        var reporter = new Progress<ConvertProgress>(p =>
        {
            for (var i = 0; i < snapshot.Count; i++)
            {
                if (i < p.Index) snapshot[i].SetState("done");
                else if (i == p.Index) snapshot[i].SetState($"{(int)(p.FileProgress * 100)}%");
                else snapshot[i].SetState("queued");
            }
            UpdateProcessingProgress(p);
        });

        try
        {
            var sources = snapshot.Select(s => s.SourcePath).ToList();
            var outputExt = SelectedOutputExtension ?? ".jpg";
            var batchMode = (CombineToSingleCheck?.IsChecked == true)
                ? BatchMode.CombineToSingle
                : BatchMode.Independent;
            var results = await engine.ConvertManyAsync(sources, outputExt, options, reporter, batchMode, _cts.Token);

            foreach (var (item, result) in snapshot.Zip(results))
            {
                long outputSize = 0;
                foreach (var p in result.OutputPaths)
                {
                    try { outputSize += new FileInfo(p).Length; } catch { }
                }

                AddToHistory(new HistoryEntry(
                    Timestamp: DateTime.Now,
                    SourcePath: item.SourcePath,
                    SourceFormat: item.FormatLabel,
                    SourceSizeBytes: item.SourceSizeBytes,
                    OutputSizeBytes: outputSize,
                    OutputCount: result.OutputPaths.Count,
                    MetaLine: item.MetaLine,
                    Status: result.Status,
                    Message: result.Message,
                    OutputPaths: result.OutputPaths.ToList()));

                _activeQueue.Remove(item);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show(this, "변환 중 오류: " + ex.Message, "Everything2Everything",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts = null;
            UpdateBadges();
            UpdateProcessQueueButton();
            UpdateActiveQueueVisibility();
            ApplyAppDataStats();
            HideProcessingProgress();
            if (_activeQueue.Count == 0) ShowTab("Past");
        }
    }

    private void ShowProcessingProgress(int totalCount)
    {
        if (ProcessingProgressPanel is null) return;
        ProcessingProgressPanel.Visibility = Visibility.Visible;
        ProcessingProgressBar.Value = 0;
        ProcessingPercentLabel.Text = "0%";
        ProcessingCountLabel.Text = $"0 / {totalCount}";
        ProcessingFileLabel.Text = "준비 중…";
    }

    private void UpdateProcessingProgress(ConvertProgress p)
    {
        if (ProcessingProgressPanel is null) return;
        var total = Math.Max(1, p.Total);
        var overall = ((p.Index + p.FileProgress) / total) * 100.0;
        overall = Math.Clamp(overall, 0, 100);
        ProcessingProgressBar.Value = overall;
        ProcessingPercentLabel.Text = $"{overall:0.#}%";
        ProcessingCountLabel.Text = $"{Math.Min(p.Index + 1, p.Total)} / {p.Total}";
        ProcessingFileLabel.Text = string.IsNullOrEmpty(p.CurrentPath)
            ? "처리 중…"
            : Path.GetFileName(p.CurrentPath);
    }

    private void HideProcessingProgress()
    {
        if (ProcessingProgressPanel is null) return;
        ProcessingProgressPanel.Visibility = Visibility.Collapsed;
        if (CancelProcessingButton is not null)
        {
            CancelProcessingButton.IsEnabled = true;
            CancelProcessingButton.Content = "취소";
        }
    }

    private void OnCancelProcessingClick(object sender, RoutedEventArgs e)
    {
        if (_cts is null) return;
        try { _cts.Cancel(); } catch { }
        if (CancelProcessingButton is not null)
        {
            CancelProcessingButton.IsEnabled = false;
            CancelProcessingButton.Content = "취소 중…";
        }
        if (ProcessingFileLabel is not null)
            ProcessingFileLabel.Text = "취소 중…";
    }

    // ============== History ==============

    private void AddToHistory(HistoryEntry entry)
    {
        AddToHistoryGroups(entry);
        HistoryStorage.Append(entry);
    }

    private void AddToHistoryGroups(HistoryEntry entry)
    {
        var label = FormatDateLabel(entry.Date);
        var group = _pastResults.FirstOrDefault(g => g.DateTitle == label);
        if (group is null)
        {
            group = new DateGroup(label);
            _pastResults.Insert(0, group);
        }
        group.Add(HistoryRow.From(entry));
    }

    private void LoadHistory()
    {
        var entries = HistoryStorage.Load();
        if (entries.Count == 0)
        {
            // 첫 실행: 데모 데이터로 시각적 가이드 제공
            SeedDemoHistory();
            return;
        }

        // 가장 오래된 것부터 추가 (Insert(0)이 누적)
        foreach (var e in entries.OrderBy(e => e.Timestamp))
            AddToHistoryGroups(e);
    }

    private void SeedDemoHistory()
    {
        var today = FormatDateLabel(DateOnly.FromDateTime(DateTime.Today));
        var todayGroup = new DateGroup(today);
        todayGroup.Add(new HistoryRow(
            FormatLabel: "PNG", FormatBrush: (Brush)FindResource("FsFmtPng"),
            FileName: "hero_background_final_v2.png",
            MetaLine: "08:42:12 • 3200x1800",
            SizeText: "14.2 MB",
            SavingsText: "↓ 1.1 MB",
            SourcePath: "<demo>"));
        todayGroup.Add(new HistoryRow(
            FormatLabel: "HEIC", FormatBrush: (Brush)FindResource("FsFmtHeic"),
            FileName: "portrait_session_04.heic",
            MetaLine: "08:35:45 • 4032x3024",
            SizeText: "6.8 MB",
            SavingsText: "↓ 2.4 MB",
            SourcePath: "<demo>"));
        todayGroup.SessionSavingsBytes = (long)(842.4 * 1024 * 1024);
        _pastResults.Add(todayGroup);

        var yesterday = FormatDateLabel(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
        var yGroup = new DateGroup(yesterday);
        yGroup.Add(new HistoryRow(
            FormatLabel: "PDF", FormatBrush: (Brush)FindResource("FsFmtPdf"),
            FileName: "Q3_Full_Marketing_Deck_v12.pdf",
            MetaLine: "17:22:10 • 124 Pages",
            SizeText: "245.4 MB",
            SavingsText: "↓ 12.8 MB",
            SourcePath: "<demo>"));
        yGroup.Add(new HistoryRow(
            FormatLabel: "PNG", FormatBrush: (Brush)FindResource("FsFmtPng"),
            FileName: "asset_bundle_archive_raw.png",
            MetaLine: "16:45:33 • 8000x8000",
            SizeText: "82.1 MB",
            SavingsText: "↓ 4.5 MB",
            SourcePath: "<demo>"));
        yGroup.SessionSavingsBytes = (long)(3.1 * 1024 * 1024 * 1024);
        _pastResults.Add(yGroup);

        UpdateBadges();
    }

    private static string FormatDateLabel(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var label = date == today ? "Today"
            : date == today.AddDays(-1) ? "Yesterday"
            : date.ToString("dddd", CultureInfo.GetCultureInfo("en-US"));
        return $"{label}, {date:MMM d}";
    }

    private void ApplyAppDataStats()
    {
        var todayLabel = FormatDateLabel(DateOnly.FromDateTime(DateTime.Today));
        var todayGroup = _pastResults.FirstOrDefault(g => g.DateTitle == todayLabel);

        var processedToday = todayGroup?.Entries.Count ?? 0;
        var allSavings = _pastResults.Sum(g => g.SessionSavingsBytes);

        ProcessedTodayText.Text = processedToday.ToString("N0", CultureInfo.InvariantCulture);
        SpaceSavedText.Text = HumanizeBytes(allSavings);
    }

    // ============== Top-bar actions ==============

    private void OnRegisterClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ContextMenuRegistrar.Register(((App)Application.Current).Engine);
            MessageBox.Show(this,
                "컨텍스트 메뉴를 등록했습니다.\n파일 우클릭 → \"추가 옵션 표시\" 또는 \"JPEG로 빠른 변환/변환…\".",
                "Everything2Everything", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "등록 중 오류: " + ex.Message, "Everything2Everything",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDiagnoseClick(object sender, RoutedEventArgs e)
    {
        var window = new DiagnoseWindow(((App)Application.Current).Engine) { Owner = this };
        window.ShowDialog();
    }

    private void OnClearAllClick(object sender, RoutedEventArgs e)
    {
        if (TabActiveBtn.IsChecked == true)
        {
            _activeQueue.Clear();
            UpdateBadges();
            UpdateProcessQueueButton();
            UpdateActiveQueueVisibility();
            RefreshAvailableOutputFormats();
        }
        else if (TabPastBtn.IsChecked == true)
        {
            var confirm = MessageBox.Show(this,
                "Past Results 전체를 삭제하시겠습니까?\n영구 저장된 이력도 함께 삭제됩니다.",
                "Everything2Everything",
                MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;

            _pastResults.Clear();
            HistoryStorage.Clear();
        }
        UpdateBadges();
        UpdateProcessQueueButton();
        UpdateActiveQueueVisibility();
        ApplyAppDataStats();
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string path && File.Exists(path))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true,
                });
            }
            catch { }
        }
    }

    public static string HumanizeBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.#} {units[unit]}";
    }

    // ========================================================================
    // Output format selection (피보팅: 양방향 매트릭스)
    // ========================================================================

    private static readonly OutputFormatInfo[] AllFormats =
    {
        new(".jpg",  "JPEG",     "JPG",  "FsFmtJpg"),
        new(".png",  "PNG",      "PNG",  "FsFmtPng"),
        new(".webp", "WebP",     "WEBP", "FsFmtWebp"),
        new(".avif", "AVIF",     "AVIF", "FsFmtAvif"),
        new(".bmp",  "BMP",      "BMP",  "FsFmtBmp"),
        new(".tif",  "TIFF",     "TIF",  "FsFmtTiff"),
        new(".gif",  "GIF",      "GIF",  "FsFmtGif"),
        new(".pdf",  "PDF",      "PDF",  "FsFmtPdf"),
        new(".docx", "Word",     "DOCX", "FsFmtDocx"),
        new(".html", "HTML",     "HTML", "FsFmtHtml"),
        new(".md",   "Markdown", "MD",   "FsFmtOther"),
        new(".txt",  "텍스트",     "TXT",  "FsFmtOther"),
        new(".csv",  "CSV",      "CSV",  "FsFmtCsv"),
        new(".json", "JSON",     "JSON", "FsFmtJson"),
        new(".xlsx", "Excel",    "XLSX", "FsFmtXlsx"),
        new(".svg",  "SVG",      "SVG",  "FsFmtSvg"),
        new(".mp4",  "MP4",      "MP4",  "FsFmtVideo"),
        new(".webm", "WebM",     "WEBM", "FsFmtVideo"),
        new(".mkv",  "MKV",      "MKV",  "FsFmtVideo"),
        new(".mov",  "MOV",      "MOV",  "FsFmtVideo"),
        new(".avi",  "AVI",      "AVI",  "FsFmtVideo"),
        new(".mp3",  "MP3",      "MP3",  "FsFmtAudio"),
        new(".aac",  "AAC",      "AAC",  "FsFmtAudio"),
        new(".m4a",  "M4A",      "M4A",  "FsFmtAudio"),
        new(".opus", "Opus",     "OPUS", "FsFmtAudio"),
        new(".ogg",  "OGG",      "OGG",  "FsFmtAudio"),
        new(".flac", "FLAC",     "FLAC", "FsFmtAudio"),
        new(".wav",  "WAV",      "WAV",  "FsFmtAudio"),
    };

    private bool _suppressFormatChanged;

    private void InitializeOutputFormats()
    {
        RefreshAvailableOutputFormats();
    }

    private void RefreshAvailableOutputFormats()
    {
        if (OutputFormatCombo is null) return;

        var engine = ((App)Application.Current).Engine;
        IReadOnlyCollection<string> available;

        if (_activeQueue.Count == 0)
        {
            available = engine.Providers.AllOutputExtensions;
        }
        else
        {
            HashSet<string>? intersection = null;
            foreach (var item in _activeQueue)
            {
                var outs = engine.Providers.OutputsForFile(item.SourcePath);
                var outSet = new HashSet<string>(outs, StringComparer.OrdinalIgnoreCase);
                if (intersection is null) intersection = outSet;
                else intersection.IntersectWith(outSet);
            }
            available = intersection ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var visible = AllFormats.Where(f => available.Contains(f.Extension)).ToList();
        if (visible.Count == 0)
            visible = AllFormats.Where(f => f.Extension == ".jpg").ToList();

        var keepExt = SelectedOutputExtension;
        if (keepExt is null || !visible.Any(v => string.Equals(v.Extension, keepExt, StringComparison.OrdinalIgnoreCase)))
            keepExt = visible[0].Extension;

        _suppressFormatChanged = true;
        try
        {
            OutputFormatCombo.Items.Clear();
            foreach (var f in visible)
            {
                OutputFormatCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{f.DisplayName} ({f.Extension})",
                    Tag = f.Extension,
                });
            }
            for (var i = 0; i < OutputFormatCombo.Items.Count; i++)
            {
                if (((ComboBoxItem)OutputFormatCombo.Items[i]!).Tag is string tag
                    && string.Equals(tag, keepExt, StringComparison.OrdinalIgnoreCase))
                {
                    OutputFormatCombo.SelectedIndex = i;
                    break;
                }
            }
        }
        finally
        {
            _suppressFormatChanged = false;
        }

        SelectedOutputExtension = keepExt;
        UpdateOutputFormatBadge(keepExt);
        UpdateQualityPanelForFormat(keepExt);
        UpdateOutputDestHint(keepExt);
        UpdateCombineState(keepExt);

        if (OutputFormatHint is not null)
        {
            OutputFormatHint.Text = _activeQueue.Count == 0
                ? "큐에 파일을 추가하면 변환 가능한 형식으로 자동 필터링됩니다"
                : $"큐의 모든 파일이 변환 가능한 형식 ({visible.Count}개)";
        }
    }

    private void UpdateCombineState(string? extension)
    {
        if (CombineToSingleCheck is null || CombineHint is null) return;

        var ext = extension ?? string.Empty;
        var combinableOutput = ConversionEngine.CanCombine(ext);
        var allInputsCombinable = _activeQueue.Count > 0
            && _activeQueue.All(q => ConversionEngine.CanCombineInput(q.SourcePath));
        var enabled = combinableOutput && allInputsCombinable && _activeQueue.Count >= 2;

        CombineToSingleCheck.IsEnabled = enabled;
        if (!enabled && CombineToSingleCheck.IsChecked == true)
            CombineToSingleCheck.IsChecked = false;

        CombineHint.Text = (combinableOutput, _activeQueue.Count) switch
        {
            (false, _) => "PDF/TIFF/GIF 출력일 때만 단일 파일 결합이 가능합니다",
            (true, 0) => "PDF/TIFF/GIF 출력에 한해 큐의 이미지들을 한 파일로 결합합니다",
            (true, 1) => "결합하려면 큐에 2개 이상의 이미지가 필요합니다",
            (true, _) when !allInputsCombinable => "결합은 이미지 입력만 지원합니다 (PDF/DOCX 등 제외)",
            _ => $"체크 시 큐의 {_activeQueue.Count}개 이미지를 단일 {ext.TrimStart('.').ToUpperInvariant()}로 결합",
        };
    }

    private void OnCombineToggleChanged(object sender, RoutedEventArgs e)
    {
        UpdateCombineState(SelectedOutputExtension);
    }

    private void OnOutputFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFormatChanged) return;
        if (OutputFormatCombo.SelectedItem is ComboBoxItem item && item.Tag is string ext)
        {
            SelectedOutputExtension = ext;
            UpdateOutputFormatBadge(ext);
            UpdateQualityPanelForFormat(ext);
            UpdateOutputDestHint(ext);
        }
    }

    private void UpdateOutputFormatBadge(string? extension)
    {
        if (OutputFormatBadge is null || OutputFormatBadgeText is null) return;
        var info = AllFormats.FirstOrDefault(f =>
            string.Equals(f.Extension, extension, StringComparison.OrdinalIgnoreCase));
        if (info is null) return;

        OutputFormatBadgeText.Text = info.BadgeText;
        var resource = TryFindResource(info.ColorResource);
        if (resource is System.Windows.Media.Brush brush)
            OutputFormatBadge.Background = brush;
    }

    private void UpdateQualityPanelForFormat(string? extension)
    {
        if (QualityPanel is null || QualityLabelText is null) return;
        var ext = extension?.ToLowerInvariant();
        var supportsQuality = ext is ".jpg" or ".jpeg" or ".webp" or ".avif";
        QualityPanel.Visibility = supportsQuality ? Visibility.Visible : Visibility.Collapsed;
        QualityLabelText.Text = ext switch
        {
            ".jpg" or ".jpeg" => "JPEG QUALITY",
            ".webp" => "WEBP QUALITY",
            ".avif" => "AVIF QUALITY",
            _ => "ENCODING QUALITY",
        };
    }

    private void UpdateOutputDestHint(string? extension)
    {
        if (OutputDestHint is null) return;
        var folder = (extension ?? ".jpg").TrimStart('.').ToLowerInvariant();
        OutputDestHint.Text = $"비워두면 원본 옆 _{folder} 폴더에 저장됩니다";
    }

    private sealed record OutputFormatInfo(string Extension, string DisplayName, string BadgeText, string ColorResource);
}

// ============================================================
// View models
// ============================================================

public sealed class QueueItem : INotifyPropertyChanged
{
    private string _state = "queued";
    private double _progressValue;
    private Visibility _progressVisibility = Visibility.Collapsed;

    public required string SourcePath { get; init; }
    public required string FileName { get; init; }
    public required string FormatLabel { get; init; }
    public required Brush FormatBrush { get; init; }
    public required string SizeText { get; init; }
    public required string MetaLine { get; init; }
    public required long SourceSizeBytes { get; init; }

    public string StateText
    {
        get => _state;
        set { _state = value; Raise(nameof(StateText)); }
    }

    public Brush StateBrush => _state switch
    {
        "queued" => (Brush)Application.Current.FindResource("FsTextTertiary"),
        "done" => (Brush)Application.Current.FindResource("FsAccentGreen"),
        _ => (Brush)Application.Current.FindResource("FsAccentBlue"),
    };

    public double ProgressValue
    {
        get => _progressValue;
        private set { _progressValue = value; Raise(nameof(ProgressValue)); }
    }

    public Visibility ProgressVisibility
    {
        get => _progressVisibility;
        private set { _progressVisibility = value; Raise(nameof(ProgressVisibility)); }
    }

    public void SetPending()
    {
        StateText = "queued";
        ProgressValue = 0;
        ProgressVisibility = Visibility.Collapsed;
        Raise(nameof(StateBrush));
    }

    public void SetState(string s)
    {
        StateText = s;
        Raise(nameof(StateBrush));

        if (s == "queued") { ProgressValue = 0; ProgressVisibility = Visibility.Collapsed; }
        else if (s == "done") { ProgressValue = 100; ProgressVisibility = Visibility.Visible; }
        else if (s.EndsWith('%') && double.TryParse(s.TrimEnd('%'), out var pct))
        {
            ProgressValue = pct;
            ProgressVisibility = Visibility.Visible;
        }
    }

    public static QueueItem FromPath(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        var (label, brushKey) = FormatPalette.For(ext);
        long size = 0;
        try { size = new FileInfo(path).Length; } catch { }

        return new QueueItem
        {
            SourcePath = path,
            FileName = Path.GetFileName(path),
            FormatLabel = label,
            FormatBrush = (Brush)Application.Current.FindResource(brushKey),
            SizeText = MainWindow.HumanizeBytes(size),
            MetaLine = $"{ext.ToUpperInvariant()} • {MainWindow.HumanizeBytes(size)}",
            SourceSizeBytes = size,
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class DateGroup : INotifyPropertyChanged
{
    public string DateTitle { get; }
    public ObservableCollection<HistoryRow> Entries { get; } = new();
    public long SessionSavingsBytes { get; set; }
    public string SessionSavingsText => $"Session Savings: {MainWindow.HumanizeBytes(SessionSavingsBytes)}";

    public DateGroup(string dateTitle) { DateTitle = dateTitle; }

    public void Add(HistoryRow row)
    {
        Entries.Insert(0, row);
        SessionSavingsBytes += row.SavingsBytes;
        Raise(nameof(SessionSavingsText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed record HistoryRow(
    string FormatLabel,
    Brush FormatBrush,
    string FileName,
    string MetaLine,
    string SizeText,
    string SavingsText,
    string SourcePath,
    string? OutputPath = null,
    long SavingsBytes = 0)
{
    public string RevealPath => OutputPath ?? SourcePath;

    public static HistoryRow From(HistoryEntry e)
    {
        var ext = Path.GetExtension(e.SourcePath).TrimStart('.').ToLowerInvariant();
        var (label, brushKey) = FormatPalette.For(ext);
        var saved = e.SavingsBytes;
        var arrow = saved >= 0 ? "↓" : "↑";
        return new HistoryRow(
            FormatLabel: label,
            FormatBrush: (Brush)Application.Current.FindResource(brushKey),
            FileName: Path.GetFileName(e.SourcePath),
            MetaLine: $"{e.Timestamp:HH:mm:ss} • {e.OutputCount} output(s)",
            SizeText: MainWindow.HumanizeBytes(e.SourceSizeBytes),
            SavingsText: $"{arrow} {MainWindow.HumanizeBytes(Math.Abs(saved))}",
            SourcePath: e.SourcePath,
            OutputPath: e.PrimaryOutputPath,
            SavingsBytes: saved);
    }
}

internal static class FormatPalette
{
    public static (string Label, string BrushKey) For(string ext) => ext switch
    {
        "pdf" => ("PDF", "FsFmtPdf"),
        "png" => ("PNG", "FsFmtPng"),
        "heic" or "heif" => ("HEIC", "FsFmtHeic"),
        "jpg" or "jpeg" or "jpe" => ("JPG", "FsFmtJpg"),
        "doc" or "docx" => ("DOCX", "FsFmtDocx"),
        "html" or "htm" => ("HTML", "FsFmtHtml"),
        "hwp" or "hwpx" => ("HWP", "FsFmtHwp"),
        "gif" => ("GIF", "FsFmtGif"),
        "tif" or "tiff" => ("TIFF", "FsFmtTiff"),
        "webp" => ("WEBP", "FsFmtWebp"),
        "bmp" => ("BMP", "FsFmtBmp"),
        "raw" or "dng" or "nef" or "cr2" or "cr3" or "arw" or "raf" or "orf" or "rw2" or "srw" or "pef"
            => ("RAW", "FsFmtRaw"),
        // 신규 카테고리 (output AllFormats 매핑과 동일 hue 유지)
        "csv" => ("CSV", "FsFmtCsv"),
        "json" => ("JSON", "FsFmtJson"),
        "xlsx" or "xls" => ("XLSX", "FsFmtXlsx"),
        "svg" => ("SVG", "FsFmtSvg"),
        "mp4" or "webm" or "mkv" or "mov" or "avi" or "m4v"
            => (ext.ToUpperInvariant(), "FsFmtVideo"),
        "mp3" or "aac" or "m4a" or "opus" or "ogg" or "oga" or "flac" or "wav"
            => (ext.ToUpperInvariant(), "FsFmtAudio"),
        _ => (ext.ToUpperInvariant(), "FsFmtOther"),
    };
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}
