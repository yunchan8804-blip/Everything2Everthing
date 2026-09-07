using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Everything2Everything.Core.Converters;

namespace Everything2Everything.App.ViewModels;

public enum ToolInstallState
{
    NotInstalled,
    Installed,
    Busy,
    Failed,
}

/// <summary>설치 마법사 목록의 한 행. 카탈로그 정의 + 선택/상태/진행 표현.</summary>
public sealed class ToolInstallItem : INotifyPropertyChanged
{
    public ExternalToolDefinition Definition { get; }
    public ToolInstallItem(ExternalToolDefinition definition)
    {
        Definition = definition;
        RefreshStatus();
    }

    public string Name => Definition.DisplayName;
    public string Description => Definition.Description;

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private ToolInstallState _state;
    public ToolInstallState State
    {
        get => _state;
        private set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
        }
    }

    public string StatusText => State switch
    {
        ToolInstallState.Installed => "설치됨",
        ToolInstallState.Busy => "설치 중…",
        ToolInstallState.Failed => "실패",
        _ => "미설치",
    };

    public bool IsBusy => State == ToolInstallState.Busy;

    public Brush StatusBrush => State switch
    {
        ToolInstallState.Installed => BrushOf("FsStatusSuccess"),
        ToolInstallState.Busy => BrushOf("FsStatusInfo"),
        ToolInstallState.Failed => BrushOf("FsStatusDanger"),
        _ => BrushOf("FsStatusWarn"),
    };

    private string? _detail;
    public string? Detail
    {
        get => _detail;
        private set { _detail = value; OnPropertyChanged(); }
    }

    public void RefreshStatus()
        => State = Definition.IsInstalled() ? ToolInstallState.Installed : ToolInstallState.NotInstalled;

    public void ApplyResult(bool success, string? detail)
    {
        Detail = detail;
        State = success ? ToolInstallState.Installed : ToolInstallState.Failed;
    }

    public void BeginInstall() => State = ToolInstallState.Busy;

    private static Brush BrushOf(string key)
        => (Application.Current?.TryFindResource(key) as Brush)
           ?? _fallbacks.GetValueOrDefault(key, _warn);

    private static readonly Brush _info = Fixed(0x1565C0);
    private static readonly Brush _success = Fixed(0x2E7D32);
    private static readonly Brush _warn = Fixed(0xC77700);
    private static readonly Brush _danger = Fixed(0xC62828);
    private static readonly Dictionary<string, Brush> _fallbacks = new()
    {
        ["FsStatusInfo"] = _info,
        ["FsStatusSuccess"] = _success,
        ["FsStatusWarn"] = _warn,
        ["FsStatusDanger"] = _danger,
    };

    private static Brush Fixed(uint rgb)
    {
        var c = (int)rgb;
        return new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>설치 마법사의 루트 뷰모델: 목록 + 전체 선택 + 선택분 순차 설치.</summary>
public sealed class ToolSetupViewModel : INotifyPropertyChanged
{
    private readonly ExternalToolInstaller _installer;
    private bool _isInstalling;
    private bool _selectAll = true;

    public ObservableCollection<ToolInstallItem> Tools { get; } = new();

    public ToolSetupViewModel(IEnumerable<ExternalToolDefinition>? definitions = null, ExternalToolInstaller? installer = null)
    {
        _installer = installer ?? new ExternalToolInstaller();
        foreach (var def in definitions ?? ExternalToolCatalog.All)
            Tools.Add(new ToolInstallItem(def));
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        private set { _isInstalling = value; OnPropertyChanged(); }
    }

    public bool SelectAll
    {
        get => _selectAll;
        set
        {
            _selectAll = value;
            foreach (var t in Tools) t.IsSelected = value;
            OnPropertyChanged();
        }
    }

    public void RefreshStatus()
    {
        foreach (var t in Tools) t.RefreshStatus();
    }

    public async Task InstallSelectedAsync(CancellationToken ct)
    {
        if (IsInstalling) return;
        IsInstalling = true;
        try
        {
            foreach (var item in Tools.Where(t => t.IsSelected).ToList())
                await InstallOneAsync(item, ct).ConfigureAwait(true);
        }
        finally
        {
            IsInstalling = false;
        }
    }

    public async Task InstallOneAsync(ToolInstallItem item, CancellationToken ct)
    {
        item.BeginInstall();
        var result = await _installer.InstallAsync(item.Definition, ct).ConfigureAwait(true);
        var detail = string.IsNullOrWhiteSpace(result.Output)
            ? result.Message
            : Tail(result.Output, 600);
        item.ApplyResult(result.Success, detail);
    }

    private static string Tail(string s, int max)
        => s.Length <= max ? s : "…" + s[^max..];

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
