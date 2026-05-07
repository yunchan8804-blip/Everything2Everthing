using Everything2Everything.App.Shell;
using Everything2Everything.Core;

namespace Everything2Everything.App.Cli;

internal static class CliRouter
{
    public enum Mode
    {
        ShowMain,
        Quick,
        Dialog,
        Register,
        Unregister,
        Diagnose,
        Help,
    }

    public sealed record ParsedArgs(Mode Mode, IReadOnlyList<string> Files, string? OutputExtension = null);

    public static ParsedArgs Parse(string[] args)
    {
        if (args is null || args.Length == 0)
            return new ParsedArgs(Mode.ShowMain, Array.Empty<string>());

        var verb = args[0].Trim().ToLowerInvariant();
        var rest = args.Skip(1).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();

        if (verb == "to")
        {
            if (rest.Count < 2)
                return new ParsedArgs(Mode.Help, Array.Empty<string>());
            var outputExt = NormalizeExt(rest[0]);
            var files = ExpandFiles(rest.Skip(1));
            return new ParsedArgs(Mode.Quick, files, outputExt);
        }

        return verb switch
        {
            "quick" => new ParsedArgs(Mode.Quick, ExpandFiles(rest), ".jpg"),
            "dialog" => new ParsedArgs(Mode.Dialog, ExpandFiles(rest)),
            "register" => new ParsedArgs(Mode.Register, Array.Empty<string>()),
            "unregister" => new ParsedArgs(Mode.Unregister, Array.Empty<string>()),
            "diagnose" or "doctor" => new ParsedArgs(Mode.Diagnose, Array.Empty<string>()),
            "help" or "--help" or "-h" or "/?" => new ParsedArgs(Mode.Help, Array.Empty<string>()),
            _ when File.Exists(args[0]) => new ParsedArgs(Mode.Dialog, ExpandFiles(args)),
            _ => new ParsedArgs(Mode.ShowMain, Array.Empty<string>()),
        };
    }

    private static string NormalizeExt(string ext)
    {
        var trimmed = ext.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    private static IReadOnlyList<string> ExpandFiles(IEnumerable<string> raw)
    {
        var list = new List<string>();
        foreach (var arg in raw)
        {
            if (string.IsNullOrWhiteSpace(arg)) continue;
            try
            {
                if (File.Exists(arg)) { list.Add(Path.GetFullPath(arg)); continue; }
                if (Directory.Exists(arg))
                {
                    foreach (var f in Directory.EnumerateFiles(arg, "*", SearchOption.TopDirectoryOnly))
                        list.Add(Path.GetFullPath(f));
                }
            }
            catch { }
        }
        return list;
    }

    public static string HelpText()
    {
        var engine = Everything2EverythingBootstrap.CreateDefault();
        var inputs = string.Join(", ",
            engine.Providers.Implemented.SelectMany(p => p.Capability.InputExtensions).Distinct().OrderBy(e => e));
        var outputs = string.Join(", ",
            engine.Providers.Implemented.SelectMany(p => p.Capability.OutputExtensions).Distinct().OrderBy(e => e));
        var coming = string.Join(", ",
            engine.Providers.ComingSoon.SelectMany(p => p.Capability.InputExtensions).Distinct().OrderBy(e => e));

        return $"""
        Everything2Everything — 모든 것을 모든 것으로 (양방향 변환)

        사용:
          Everything2Everything.exe to <ext> <파일들...>   <ext>로 변환 (예: to png a.jpg b.heic)
          Everything2Everything.exe quick <파일들...>      빠른 변환 (기본 출력 .jpg)
          Everything2Everything.exe dialog <파일들...>     메인 창에서 출력 형식 선택
          Everything2Everything.exe register                컨텍스트 메뉴 등록 (현재 사용자)
          Everything2Everything.exe unregister              컨텍스트 메뉴 해제
          Everything2Everything.exe diagnose                지원 매트릭스·외부 도구 진단
          Everything2Everything.exe                         메인 창 표시

        입력 형식: {inputs}
        출력 형식: {outputs}
        지원 예정: {coming}
        """;
    }

    public static int RunRegister(bool register)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "Everything2Everything_register.log");
        var log = new System.Text.StringBuilder();
        log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] RunRegister start, register={register}");
        try
        {
            log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] Creating engine...");
            var engine = Everything2EverythingBootstrap.CreateDefault();
            log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] Engine OK. Implemented providers: {string.Join(",", engine.Providers.Implemented.Select(p => p.Capability.Id))}");

            if (register)
            {
                log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] Calling Register...");
                ContextMenuRegistrar.Register(engine);
            }
            else
            {
                ContextMenuRegistrar.Unregister(engine);
            }
            log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] OK");
            File.WriteAllText(logPath, log.ToString());
            Console.Out.WriteLine($"등록 완료. 로그: {logPath}");
            return 0;
        }
        catch (Exception ex)
        {
            log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] EXCEPTION {ex.GetType().Name}: {ex.Message}");
            log.AppendLine(ex.ToString());
            try { File.WriteAllText(logPath, log.ToString()); } catch { }
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
