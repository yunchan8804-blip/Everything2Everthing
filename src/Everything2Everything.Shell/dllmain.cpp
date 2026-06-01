// Everything2Everything shell extension — IExplorerCommand cascade
//
//   Root verb (cascade)         → "Everything2Everything으로 변환"
//   ├─ ToJpg / ToPng / ...      → "exe to <ext> <paths>"
//   └─ Dialog                   → "exe dialog <paths>"
//
//   Legacy CLSIDs (Quick, Dialog) are kept for backward compat with any
//   external registrations that may still reference them.

#include "pch.h"

#pragma warning(disable : 4324)

using Microsoft::WRL::ClassicCom;
using Microsoft::WRL::ComPtr;
using Microsoft::WRL::InhibitRoOriginateError;
using Microsoft::WRL::Make;
using Microsoft::WRL::MakeAndInitialize;
using Microsoft::WRL::Module;
using Microsoft::WRL::ModuleType;
using Microsoft::WRL::RuntimeClass;
using Microsoft::WRL::RuntimeClassFlags;

namespace {

constexpr const wchar_t* kExeFileName = L"Everything2Everything.exe";

std::wstring QuoteForCommandLineArg(const std::wstring& arg) {
    const std::wstring quotable_chars(L" \\\"");
    if (arg.find_first_of(quotable_chars) == std::wstring::npos) {
        return arg;
    }

    std::wstring out;
    out.push_back(L'"');
    for (size_t i = 0; i < arg.size(); ++i) {
        if (arg[i] == L'\\') {
            const size_t start = i;
            size_t end = start + 1;
            for (; end < arg.size() && arg[end] == L'\\'; ++end) {}
            size_t backslash_count = end - start;
            if (end == arg.size() || arg[end] == L'"') {
                backslash_count *= 2;
            }
            for (size_t j = 0; j < backslash_count; ++j)
                out.push_back(L'\\');
            i = end - 1;
        }
        else if (arg[i] == L'"') {
            out.push_back(L'\\');
            out.push_back(L'"');
        }
        else {
            out.push_back(arg[i]);
        }
    }
    out.push_back(L'"');
    return out;
}

std::filesystem::path ResolveExePath() {
    std::filesystem::path module_path{
        wil::GetModuleFileNameW<std::wstring>(wil::GetModuleInstanceHandle()) };
    module_path = module_path.remove_filename();
    module_path /= kExeFileName;
    return module_path;
}

HRESULT LaunchAppWithItems(const wchar_t* verb, IShellItemArray* items) {
    if (!items) return S_OK;

    DWORD count = 0;
    RETURN_IF_FAILED(items->GetCount(&count));
    if (count == 0) return S_OK;

    auto exe_path = ResolveExePath();

    auto command = wil::str_printf<std::wstring>(LR"-("%s" %s)-",
        exe_path.c_str(), verb);

    for (DWORD i = 0; i < count; ++i) {
        ComPtr<IShellItem> item;
        if (FAILED(items->GetItemAt(i, &item))) continue;

        wil::unique_cotaskmem_string path;
        if (FAILED(item->GetDisplayName(SIGDN_FILESYSPATH, &path))) continue;

        command = wil::str_printf<std::wstring>(LR"-(%s %s)-",
            command.c_str(),
            QuoteForCommandLineArg(path.get()).c_str());
    }

    wil::unique_process_information process_info;
    STARTUPINFOW startup_info = { sizeof(startup_info) };
    RETURN_IF_WIN32_BOOL_FALSE(CreateProcessW(
        nullptr,
        command.data(),
        nullptr,
        nullptr,
        FALSE,
        CREATE_NO_WINDOW,
        nullptr,
        nullptr,
        &startup_info,
        &process_info));

    return S_OK;
}

// ---------------------------------------------------------------------
// 입력 형식별 추천 출력 — 영상 우클릭엔 영상/오디오 출력만, 데이터엔 데이터 출력만 보이도록
// ---------------------------------------------------------------------
enum class Cat { Image, Video, Audio, Data, Vector, Doc, Markup, Text };

inline Cat CatOf(const std::wstring& e) {
    if (e == L".mp4" || e == L".webm" || e == L".mkv" || e == L".mov" || e == L".avi") return Cat::Video;
    if (e == L".mp3" || e == L".aac" || e == L".m4a" || e == L".opus" || e == L".ogg" || e == L".flac" || e == L".wav") return Cat::Audio;
    if (e == L".csv" || e == L".json" || e == L".xlsx") return Cat::Data;
    if (e == L".svg") return Cat::Vector;
    if (e == L".md" || e == L".markdown" || e == L".html" || e == L".htm") return Cat::Markup;
    if (e == L".txt") return Cat::Text;
    if (e == L".pdf" || e == L".docx" || e == L".doc" || e == L".hwp" || e == L".hwpx") return Cat::Doc;
    return Cat::Image;  // 이미지/RAW/HEIC/PSD 등 나머지는 이미지로 취급
}

inline bool Recommend(const std::wstring& inExt, const std::wstring& outExt) {
    const Cat in = CatOf(inExt), out = CatOf(outExt);
    if (in == Cat::Video) return out == Cat::Video || out == Cat::Audio;
    if (in == Cat::Audio) return out == Cat::Audio;
    if (in == Cat::Data)  return out == Cat::Data;
    if (in == Cat::Image || in == Cat::Vector)
        return out == Cat::Image || outExt == L".pdf" || outExt == L".txt" || outExt == L".docx";
    if (in == Cat::Doc)
        return out == Cat::Image || outExt == L".pdf" || outExt == L".txt" || outExt == L".docx" || outExt == L".html" || outExt == L".md";
    if (in == Cat::Markup || in == Cat::Text)
        return outExt == L".html" || outExt == L".md" || outExt == L".txt" || outExt == L".docx" || outExt == L".pdf";
    return true;
}

inline std::wstring FirstItemExt(IShellItemArray* items) {
    if (!items) return L"";
    ComPtr<IShellItem> item;
    if (FAILED(items->GetItemAt(0, &item))) return L"";
    wil::unique_cotaskmem_string path;
    if (FAILED(item->GetDisplayName(SIGDN_FILESYSPATH, &path))) return L"";
    std::wstring p(path.get());
    const auto dot = p.find_last_of(L'.');
    if (dot == std::wstring::npos) return L"";
    std::wstring ext = p.substr(dot);
    for (auto& c : ext) c = static_cast<wchar_t>(towlower(c));
    return ext;
}

// ---------------------------------------------------------------------
// Sub-command — leaf in the cascade (e.g., "JPEG (.jpg)" → "to jpg")
// ---------------------------------------------------------------------
class SubVerbCommand : public RuntimeClass<
    RuntimeClassFlags<ClassicCom | InhibitRoOriginateError>,
    IExplorerCommand>
{
public:
    HRESULT RuntimeClassInitialize() { return S_OK; }

    void Configure(std::wstring title, std::wstring verb, std::wstring outExt = L"") {
        title_ = std::move(title);
        verb_ = std::move(verb);
        outExt_ = std::move(outExt);
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* name) override {
        return SHStrDupW(title_.c_str(), name);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override {
        auto exe = ResolveExePath();
        return SHStrDupW(exe.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* infoTip) override {
        *infoTip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* guidCommandName) override {
        *guidCommandName = GUID_NULL;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* cmdState) override {
        if (outExt_.empty()) { *cmdState = ECS_ENABLED; return S_OK; }  // "변환…" 항목은 항상 표시
        const std::wstring inExt = FirstItemExt(items);
        *cmdState = (inExt.empty() || Recommend(inExt, outExt_)) ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override {
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enumCommands) override {
        *enumCommands = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override {
        return LaunchAppWithItems(verb_.c_str(), items);
    }

private:
    std::wstring title_;
    std::wstring verb_;
    std::wstring outExt_;
};

// ---------------------------------------------------------------------
// IEnumExplorerCommand — feeds children to Explorer
// ---------------------------------------------------------------------
class SubCommandEnumerator : public RuntimeClass<
    RuntimeClassFlags<ClassicCom | InhibitRoOriginateError>,
    IEnumExplorerCommand>
{
public:
    HRESULT RuntimeClassInitialize() { return S_OK; }

    void Configure(std::vector<ComPtr<IExplorerCommand>> commands) {
        commands_ = std::move(commands);
        index_ = 0;
    }

    IFACEMETHODIMP Next(ULONG celt, IExplorerCommand** apUICommand, ULONG* pceltFetched) override {
        if (!apUICommand) return E_POINTER;
        ULONG fetched = 0;
        for (; fetched < celt && index_ < commands_.size(); ++fetched, ++index_) {
            apUICommand[fetched] = commands_[index_].Get();
            apUICommand[fetched]->AddRef();
        }
        if (pceltFetched) *pceltFetched = fetched;
        return (fetched == celt) ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP Skip(ULONG celt) override {
        size_t remaining = commands_.size() - index_;
        index_ += static_cast<size_t>(std::min<ULONG>(celt, static_cast<ULONG>(remaining)));
        return S_OK;
    }

    IFACEMETHODIMP Reset() override {
        index_ = 0;
        return S_OK;
    }

    IFACEMETHODIMP Clone(IEnumExplorerCommand** ppenum) override {
        if (!ppenum) return E_POINTER;
        ComPtr<SubCommandEnumerator> clone;
        RETURN_IF_FAILED(MakeAndInitialize<SubCommandEnumerator>(&clone));
        clone->Configure(commands_);
        clone->index_ = index_;
        return clone.CopyTo(ppenum);
    }

private:
    std::vector<ComPtr<IExplorerCommand>> commands_;
    size_t index_ = 0;
};

// ---------------------------------------------------------------------
// Root cascade — "Everything2Everything으로 변환" with 11 sub-items
// ---------------------------------------------------------------------
struct SubItem {
    const wchar_t* Title;
    const wchar_t* Verb;
};

inline ComPtr<SubVerbCommand> MakeSub(const wchar_t* title, const wchar_t* verb, const wchar_t* outExt = L"") {
    ComPtr<SubVerbCommand> cmd;
    MakeAndInitialize<SubVerbCommand>(&cmd);
    cmd->Configure(title, verb, outExt);
    return cmd;
}

}  // namespace

class __declspec(uuid("F1A2B3C4-D5E6-4789-9A01-2B3C4D5E6F70"))
    RootCascadeCommand final
    : public RuntimeClass<
        RuntimeClassFlags<ClassicCom | InhibitRoOriginateError>,
        IExplorerCommand>
{
public:
    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* name) override {
        return SHStrDupW(L"Everything2Everything으로 변환", name);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override {
        auto exe = ResolveExePath();
        return SHStrDupW(exe.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* infoTip) override {
        *infoTip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* guidCommandName) override {
        *guidCommandName = __uuidof(RootCascadeCommand);
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* cmdState) override {
        *cmdState = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override {
        *flags = ECF_HASSUBCOMMANDS;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enumCommands) override {
        if (!enumCommands) return E_POINTER;
        *enumCommands = nullptr;

        std::vector<ComPtr<IExplorerCommand>> cmds;
        cmds.reserve(26);
        // 이미지 (입력이 이미지/벡터/문서일 때 표시)
        cmds.push_back(MakeSub(L"JPEG (.jpg)",          L"to jpg",  L".jpg"));
        cmds.push_back(MakeSub(L"PNG (.png)",           L"to png",  L".png"));
        cmds.push_back(MakeSub(L"WebP (.webp)",         L"to webp", L".webp"));
        cmds.push_back(MakeSub(L"AVIF (.avif)",         L"to avif", L".avif"));
        cmds.push_back(MakeSub(L"GIF (.gif)",           L"to gif",  L".gif"));
        cmds.push_back(MakeSub(L"TIFF (.tif)",          L"to tif",  L".tif"));
        cmds.push_back(MakeSub(L"BMP (.bmp)",           L"to bmp",  L".bmp"));
        cmds.push_back(MakeSub(L"PDF (.pdf)",           L"to pdf",  L".pdf"));
        cmds.push_back(MakeSub(L"텍스트 (.txt)",        L"to txt",  L".txt"));
        cmds.push_back(MakeSub(L"Word (.docx)",         L"to docx", L".docx"));
        cmds.push_back(MakeSub(L"HTML (.html)",         L"to html", L".html"));
        cmds.push_back(MakeSub(L"Markdown (.md)",       L"to md",   L".md"));
        // 영상 (입력이 영상일 때 표시)
        cmds.push_back(MakeSub(L"MP4 (.mp4)",           L"to mp4",  L".mp4"));
        cmds.push_back(MakeSub(L"WebM (.webm)",         L"to webm", L".webm"));
        cmds.push_back(MakeSub(L"MKV (.mkv)",           L"to mkv",  L".mkv"));
        cmds.push_back(MakeSub(L"MOV (.mov)",           L"to mov",  L".mov"));
        // 오디오 (입력이 영상/오디오일 때 표시)
        cmds.push_back(MakeSub(L"MP3 (.mp3)",           L"to mp3",  L".mp3"));
        cmds.push_back(MakeSub(L"M4A (.m4a)",           L"to m4a",  L".m4a"));
        cmds.push_back(MakeSub(L"FLAC (.flac)",         L"to flac", L".flac"));
        cmds.push_back(MakeSub(L"WAV (.wav)",           L"to wav",  L".wav"));
        // 데이터 (입력이 데이터일 때 표시)
        cmds.push_back(MakeSub(L"JSON (.json)",         L"to json", L".json"));
        cmds.push_back(MakeSub(L"CSV (.csv)",           L"to csv",  L".csv"));
        cmds.push_back(MakeSub(L"Excel (.xlsx)",        L"to xlsx", L".xlsx"));
        // 항상 표시
        cmds.push_back(MakeSub(L"변환…  (옵션 선택)",   L"dialog"));

        ComPtr<SubCommandEnumerator> enumerator;
        RETURN_IF_FAILED(MakeAndInitialize<SubCommandEnumerator>(&enumerator));
        enumerator->Configure(std::move(cmds));
        return enumerator.CopyTo(enumCommands);
    }

    IFACEMETHODIMP Invoke(IShellItemArray*, IBindCtx*) override {
        // Root verb itself does nothing — children carry the action.
        return S_OK;
    }
};

// ---------------------------------------------------------------------
// Legacy non-cascade verbs — kept so any older registrations still work.
// ---------------------------------------------------------------------
template <typename Derived>
class CommandHandlerBase : public RuntimeClass<
    RuntimeClassFlags<ClassicCom | InhibitRoOriginateError>,
    IExplorerCommand>
{
public:
    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* name) override {
        return SHStrDupW(Derived::Title(), name);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override {
        auto exe = ResolveExePath();
        return SHStrDupW(exe.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* infoTip) override {
        *infoTip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* guidCommandName) override {
        *guidCommandName = GUID_NULL;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* cmdState) override {
        *cmdState = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override {
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enumCommands) override {
        *enumCommands = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override {
        return LaunchAppWithItems(Derived::Verb(), items);
    }
};

class __declspec(uuid("801B2DD3-632C-4731-9510-AEAE09345264"))
    QuickCommandHandler final
    : public CommandHandlerBase<QuickCommandHandler>
{
public:
    static constexpr const wchar_t* Title() { return L"Everything2Everything: 빠른 변환 (JPEG)"; }
    static constexpr const wchar_t* Verb() { return L"quick"; }
};

class __declspec(uuid("CEBA1DB7-9175-4DF6-A362-490DEA49B598"))
    DialogCommandHandler final
    : public CommandHandlerBase<DialogCommandHandler>
{
public:
    static constexpr const wchar_t* Title() { return L"Everything2Everything: 변환…"; }
    static constexpr const wchar_t* Verb() { return L"dialog"; }
};

CoCreatableClass(RootCascadeCommand)
CoCreatableClass(QuickCommandHandler)
CoCreatableClass(DialogCommandHandler)
CoCreatableClassWrlCreatorMapInclude(RootCascadeCommand)
CoCreatableClassWrlCreatorMapInclude(QuickCommandHandler)
CoCreatableClassWrlCreatorMapInclude(DialogCommandHandler)

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID) {
    return TRUE;
}

_Check_return_
STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv) {
    if (ppv == nullptr) return E_POINTER;
    *ppv = nullptr;
    return Module<ModuleType::InProc>::GetModule().GetClassObject(rclsid, riid, ppv);
}

__control_entrypoint(DllExport)
STDAPI DllCanUnloadNow(void) {
    return Module<ModuleType::InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}
