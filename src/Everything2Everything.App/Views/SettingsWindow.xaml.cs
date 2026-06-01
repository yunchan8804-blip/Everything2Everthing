using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;

namespace Everything2Everything.App.Views;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ISettingsStore _settings;

    public SettingsWindow(ISettingsStore settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
        RefreshToolStatus();
    }

    private void LoadSettings()
    {
        var backend = (_settings.Get("ai.backend") ?? "auto").ToLowerInvariant();
        BackendCombo.SelectedIndex = backend switch
        {
            "openai" => 1,
            "anthropic" => 2,
            "codex" => 3,
            _ => 0,
        };
        ModelBox.Text = _settings.Get("ai.model") ?? string.Empty;

        SetKeyStatus(OpenAiDot, OpenAiStatus, _settings.Contains("openai.apikey"), HasEnv("OPENAI_API_KEY"));
        SetKeyStatus(AnthropicDot, AnthropicStatus, _settings.Contains("anthropic.apikey"), HasEnv("ANTHROPIC_API_KEY"));

        GpuToggle.IsChecked = _settings.Get("video.gpu") != "false"; // 기본 켜짐
    }

    private static bool HasEnv(string name) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name));

    private void SetKeyStatus(Ellipse dot, TextBlock text, bool stored, bool env)
    {
        if (stored) { dot.Fill = Res("FsStatusSuccess"); text.Text = "저장됨 (변경하려면 새 키 입력)"; }
        else if (env) { dot.Fill = Res("FsStatusInfo"); text.Text = "환경변수에서 감지됨"; }
        else { dot.Fill = Res("FsTextTertiary"); text.Text = "키 미설정"; }
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    // --- 2단계 Verify: 형식 검사로 버튼 활성화 → 클릭 시에만 실제 핑 ---

    private void OnOpenAiKeyChanged(object sender, RoutedEventArgs e)
        => OpenAiVerifyBtn.IsEnabled = OpenAiKeyBox.Password.StartsWith("sk-", StringComparison.Ordinal);

    private void OnAnthropicKeyChanged(object sender, RoutedEventArgs e)
        => AnthropicVerifyBtn.IsEnabled = AnthropicKeyBox.Password.StartsWith("sk-ant-", StringComparison.Ordinal);

    private async void OnVerifyOpenAi(object sender, RoutedEventArgs e)
        => await VerifyAsync(OpenAiDot, OpenAiStatus, OpenAiVerifyBtn,
            new OpenAiChatClient(OpenAiKeyBox.Password), ModelOr("gpt-4o-mini"));

    private async void OnVerifyAnthropic(object sender, RoutedEventArgs e)
        => await VerifyAsync(AnthropicDot, AnthropicStatus, AnthropicVerifyBtn,
            new AnthropicChatClient(AnthropicKeyBox.Password), ModelOr("claude-3-5-sonnet-latest"));

    private async void OnVerifyCodex(object sender, RoutedEventArgs e)
        => await VerifyAsync(CodexDot, CodexStatus, CodexVerifyBtn, new CodexChatClient(), ModelOr(string.Empty));

    private string ModelOr(string fallback)
        => string.IsNullOrWhiteSpace(ModelBox.Text) ? fallback : ModelBox.Text.Trim();

    private async Task VerifyAsync(Ellipse dot, TextBlock text, Button btn, IChatClient client, string model)
    {
        btn.IsEnabled = false;
        dot.Fill = Res("FsStatusInfo");
        text.Text = "확인 중…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var reply = await client.CompleteAsync("Reply with exactly: OK", "ping", model, 8, cts.Token);
            dot.Fill = Res("FsStatusSuccess");
            text.Text = string.IsNullOrWhiteSpace(reply) ? $"확인됨 ({client.Name})" : $"확인됨 — {client.Name}";
        }
        catch (Exception ex)
        {
            dot.Fill = Res("FsStatusDanger");
            text.Text = "실패: " + Trunc(ex.Message);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private static string Trunc(string s) => s.Length > 64 ? s[..64] + "…" : s;

    private void RefreshToolStatus()
    {
        var ffmpeg = ExternalToolDetector.TryFindFfmpeg(out _);
        FfmpegDot.Fill = Res(ffmpeg ? "FsStatusSuccess" : "FsStatusWarn");
        FfmpegStatus.Text = ffmpeg ? "준비됨" : "미설치";

        var libre = ExternalToolDetector.TryFindLibreOfficeSoffice(out _);
        LibreDot.Fill = Res(libre ? "FsStatusSuccess" : "FsStatusWarn");
        LibreStatus.Text = libre ? "준비됨" : "미설치";

        var codex = ExternalToolDetector.IsCodexAvailable();
        CodexDot.Fill = Res(codex ? "FsStatusSuccess" : "FsTextTertiary");
        CodexStatus.Text = codex ? "설치됨 — 키 없이 사용 가능" : "미설치 (npm i -g @openai/codex)";
        CodexVerifyBtn.IsEnabled = codex;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var backend = BackendCombo.SelectedIndex switch
        {
            1 => "openai",
            2 => "anthropic",
            3 => "codex",
            _ => "auto",
        };
        _settings.Set("ai.backend", backend);

        var model = ModelBox.Text?.Trim();
        if (string.IsNullOrEmpty(model)) _settings.Remove("ai.model");
        else _settings.Set("ai.model", model);

        _settings.Set("video.gpu", GpuToggle.IsChecked == true ? "true" : "false");
        if (OpenAiKeyBox.Password.Length > 0) _settings.Set("openai.apikey", OpenAiKeyBox.Password);
        if (AnthropicKeyBox.Password.Length > 0) _settings.Set("anthropic.apikey", AnthropicKeyBox.Password);

        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnDownloadFfmpeg(object sender, RoutedEventArgs e)
        => OpenUrl("https://github.com/BtbN/FFmpeg-Builds/releases");

    private void OnDownloadLibre(object sender, RoutedEventArgs e)
        => OpenUrl("https://www.libreoffice.org/download/");

    private void OnOpenFfmpegFolder(object sender, RoutedEventArgs e)
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Everything2Everything", "ffmpeg");
        Directory.CreateDirectory(dir);
        OpenUrl(dir);
    }

    private static void OpenUrl(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch { /* 브라우저/탐색기 실행 실패 무시 */ }
    }
}
