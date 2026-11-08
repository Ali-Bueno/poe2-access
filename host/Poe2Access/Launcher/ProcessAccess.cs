using System.Runtime.InteropServices;

namespace Poe2Access;

/// <summary>
///     What a launcher may learn of another process without reading its memory, which Process.MainModule needs and the
///     Korean client denies.
/// </summary>
internal static partial class ProcessAccess
{
    private const uint ProcessTerminate = 0x0001;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ErrorAccessDenied = 5;
    private const int ProcessBasicInformation = 0; // PROCESSINFOCLASS (winternl.h)

    /// <summary>The process's executable, or null when it cannot be read (the last P/Invoke error says why).</summary>
    public static unsafe string? ImagePath(int processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            const int capacity = 1024;
            var buffer = stackalloc char[capacity];
            var length = capacity;
            return QueryFullProcessImageName(process, 0, buffer, ref length) ? new string(buffer, 0, length) : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>The id of the process that started this one, or null when it cannot be read.</summary>
    public static int? ParentId(int processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var info = default(BasicInformation);
            return NtQueryInformationProcess(process, ProcessBasicInformation, ref info, Marshal.SizeOf<BasicInformation>(), out _) == 0
                ? (int)info.InheritedFromUniqueProcessId
                : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>Whether Windows denies this launcher ending the process: it runs as administrator and the launcher does not.</summary>
    public static bool TerminateDenied(int processId)
    {
        var process = OpenProcess(ProcessTerminate, false, processId);
        if (process == IntPtr.Zero)
        {
            return Marshal.GetLastPInvokeError() == ErrorAccessDenied;
        }

        CloseHandle(process);
        return false;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageName(IntPtr process, uint flags, char* name, ref int size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryInformationProcess(IntPtr process, int infoClass, ref BasicInformation info, int size, out int returned);

    /// <summary>PROCESS_BASIC_INFORMATION (winternl.h).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicInformation
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }
}
