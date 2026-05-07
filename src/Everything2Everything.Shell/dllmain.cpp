// Everything2Everything shell extension — IExplorerCommand handlers
// Two verbs:
//   - QuickCommandHandler  → "Everything2Everything.exe quick "<paths>""
//   - DialogCommandHandler → "Everything2Everything.exe dialog "<paths>""

#include "pch.h"

#pragma warning(disable : 4324)

using Microsoft::WRL::ClassicCom;
using Microsoft::WRL::ComPtr;
using Microsoft::WRL::InhibitRoOriginateError;
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

}  // namespace

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

CoCreatableClass(QuickCommandHandler)
CoCreatableClass(DialogCommandHandler)
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
