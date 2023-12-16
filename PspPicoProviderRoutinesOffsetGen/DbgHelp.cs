using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace PspPicoProviderRoutinesOffsetGen;

static unsafe class DbgHelp
{
    [StructLayout(LayoutKind.Sequential)]
    struct SYMBOL_INFO
    {
        public uint SizeOfStruct;
        public uint TypeIndex;
        public fixed ulong Reserved[2];
        public uint Index;
        public uint Size;
        public ulong ModBase;
        public uint Flags;
        public ulong Value;
        public ulong Address;
        public uint Register;
        public uint Scope;
        public uint Tag;
        public uint NameLen;
        public uint MaxNameLen;
        public fixed byte Name[1];
    }

    delegate bool _SymEnumerateSymbolsCallback(void* pSymInfo, uint SymbolSize, nint UserContext);

    [DllImport("Dbghelp.dll", EntryPoint = nameof(SymInitialize),
        CharSet = CharSet.Ansi, SetLastError = true)]
    static extern bool _SymInitialize(nint hProcess, string? UserSearchPath, bool fInvadeProcess);

    [DllImport("Dbghelp.dll", EntryPoint = nameof(SymLoadModuleEx),
        CharSet = CharSet.Ansi, SetLastError = true)]
    static extern ulong _SymLoadModuleEx(nint hProcess, nint hFile,
        string? ImageName, string? ModuleName, ulong BaseOfDll,
        uint DllSize, nint Data, uint Flags);

    [DllImport("Dbghelp.dll", EntryPoint = nameof(SymEnumSymbols),
        CharSet = CharSet.Ansi, SetLastError = true)]
    static extern bool _SymEnumSymbols(nint hProcess, ulong BaseOfDll,
        string? Mask, _SymEnumerateSymbolsCallback EnumSymbolsCallback, nint UserContext);

    [DllImport("Dbghelp.dll", EntryPoint = nameof(SymUnloadModule),
        CharSet = CharSet.Ansi, SetLastError = true)]
    static extern bool _SymUnloadModule(nint hProcess, ulong BaseOfDll);

    public struct SymbolInfo
    {
        public ulong ModBase;
        public ulong Address;
        public string Name;
    }

    public delegate bool SymEnumerateSymbolsCallback(SymbolInfo SymbolInfo, nint UserContext);

    public static void SymInitialize(nint hProcess, string? UserSearchPath, bool fInvadeProcess)
    {
        if (!_SymInitialize(hProcess, UserSearchPath, fInvadeProcess))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static ulong SymLoadModuleEx(nint hProcess, nint hFile,
        string? ImageName, string? ModuleName, ulong BaseOfDll,
        uint DllSize, nint Data, uint Flags)
    {
        ulong result = _SymLoadModuleEx(hProcess, hFile,
            ImageName, ModuleName, BaseOfDll, DllSize, Data, Flags);

        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return result;
    }

    public static void SymEnumSymbols(nint hProcess, ulong BaseOfDll,
        string? Mask, SymEnumerateSymbolsCallback EnumSymbolsCallback, nint UserContext)
    {
        if (!_SymEnumSymbols(hProcess, BaseOfDll, Mask, (p, size, ctx) =>
        {
            var pSymbolInfo = (SYMBOL_INFO*)p;
            var info = new SymbolInfo()
            {
                Address = pSymbolInfo->Address,
                ModBase = pSymbolInfo->ModBase,
                Name = Marshal.PtrToStringAnsi((nint)pSymbolInfo->Name, (int)pSymbolInfo->NameLen)
            };

            return EnumSymbolsCallback(info, UserContext);
        }, UserContext))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static void SymUnloadModule(nint hProcess, ulong BaseOfDll)
    {
        if (!_SymUnloadModule(hProcess, BaseOfDll))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
