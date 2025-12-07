using System.Runtime.InteropServices;

namespace SteamEngineSimController.MemoryHelpers;
public class KernelMethods {
    [DllImport("Kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, ref uint lpNumberOfBytesRead);

    public static byte[] ReadMemory(IntPtr handle, IntPtr address, uint size) {
        byte[] buffer = new byte[size];
        uint bytesRead = 0;
        ReadProcessMemory(handle, address, buffer, size, ref bytesRead);

        // validate read action
        if (bytesRead != size)
            throw new Exception($"Read operation from address 0x{address:X8} was partial! Expected number of bytes read: {size}; Actual amount: {bytesRead}");

        return buffer;
    }


    [DllImport("kernel32.dll")]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, ref uint lpNumberOfBytesWritten);

    public static void WriteMemory(IntPtr handle, IntPtr address, byte[] newData) {
        uint bytesWrittenCount = 0;
        WriteProcessMemory(handle, address, newData, newData.Length, ref bytesWrittenCount);

        // validate write action
        if (bytesWrittenCount != newData.Length)
            throw new Exception($"Write operation to address 0x{address:X8} failed! Expected number of bytes written: {newData.Length}; Actual amount: {bytesWrittenCount}");
    }


    [DllImport("kernel32.dll")]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpBaseAddress, int dwSize, int flAllocationType, int flProtect);
    // lpBaseAddress = NULL -> automatically determine location

    public static IntPtr AllocProcessMemory(IntPtr handle, int size, bool executable) {
        return VirtualAllocEx(handle, IntPtr.Zero, size, 0x00001000 | 0x00002000 /* MEM_COMMIT | MEM_RESERVE */, executable ? 0x40 /* PAGE_EXEC_READWRITE */ : 0x04 /* PAGE_READWRITE */);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr VirtualFreeEx(IntPtr hProcess, IntPtr lpBaseAddress, int dwSize, int deFreeType);

    public static IntPtr FreeProcessMemory(IntPtr handle, IntPtr baseAddress, int size) {
        return VirtualFreeEx(handle, baseAddress, size, 0x00008000 /* MEM_RELEASE */);
    }


    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    internal static extern IntPtr GetProcAddress(IntPtr hModule, string functionName);

    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, int dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, int dwCreationFlags, IntPtr lpThreadId);
    // lpThreadAttribs == 0 -> use default value
    // dwStackSize == 0 -> use default value
    // dwCreationFlags: 0 -> runs immediately; 4-> starts suspended
    // lpThreadId == NULL: then no thread identifier is returned

    public static IntPtr CreateRemoteThread(IntPtr handle, IntPtr startAddress, int stackSize = 0, bool startSuspended = false) {
        return CreateRemoteThread(handle, IntPtr.Zero, stackSize, startAddress, IntPtr.Zero, startSuspended ? 4 : 0, IntPtr.Zero);
    }

    /// <summary>
    /// DO NOT USE DIRECTLY, TO LOCK THE GAME PROCESS. USE <see cref="Util.SuspendGameProcessLock.Lock(Action)"/> INSTEAD!
    /// </summary>
    [DllImport("ntdll.dll", PreserveSig = false)]
    internal static extern void NtSuspendProcess(IntPtr processHandle);

    /// <summary>
    /// DO NOT USE DIRECTLY, TO LOCK THE GAME PROCESS. USE <see cref="Util.SuspendGameProcessLock.Lock(Action)"/> INSTEAD!
    /// </summary>

    [DllImport("ntdll.dll", PreserveSig = false, SetLastError = true)]
    internal static extern void NtResumeProcess(IntPtr processHandle);

    [DllImport("kernel32.dll", PreserveSig = false, SetLastError = true)]
    private static extern int VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, int flNewProtect, ref int lpFlOldProtect);

    /// <summary>
    /// Changes the page protection to the specified value and returns the old one
    /// </summary>
    /// <param name="handle">the game handle to use</param>
    /// <param name="baseAddress">the base address of the range to change the proection of</param>
    /// <param name="size">the size of the range to change the proection of</param>
    /// <param name="newProtectionValue">a value indicating the new proection status</param>
    /// <returns>the old protection status</returns>
    public static FlPageProtect ChangePageProtection(IntPtr handle, IntPtr baseAddress, int size, FlPageProtect newProtectionValue) {
        int oldProtect = 0;
        VirtualProtectEx(handle, baseAddress, size, (int)newProtectionValue, ref oldProtect); // return value is always zero???
        var error_code = Marshal.GetLastWin32Error();
        if (error_code != 0)
            throw new InvalidOperationException($"ChangePageProtection failed with result code: {error_code}");

        return (FlPageProtect)oldProtect;
    }

    [Flags]
    public enum FlPageProtect : int {
        PAGE_NOACCESS = 0x1,
        PAGE_READONLY = 0x2,
        PAGE_READWRITE = 0x4,
        PAGE_WRITECOPY = 0x8,
        PAGE_EXECUTE = 0x10,
        PAGE_EXECUTE_READ = 0x20,
        PAGE_EXECUTE_READWRITE = 0x40,
        PAGE_EXECUTE_WRITECOPY = 0x80,
        PAGE_GUARD = 0x100,
        PAGE_NOCACHE = 0x200,
        PAGE_WRITECOMBINE = 0x400,

    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct PMEMORY_BASIC_INFORMATION(
        IntPtr BaseAddress,
        IntPtr AllocationBase,
        FlPageProtect AllocationProtect,
        short PartitionId,
        ulong RegionSize, // size_t
        MemoryState State,
        FlPageProtect Protect,
        int Type
    );

    [DllImport("kernel32.dll", PreserveSig = false, SetLastError = true)]
    private static extern int GetLastError();

    private readonly struct PrivilegeSet {
        private readonly int PrivCount;
        private readonly int Control;
        private readonly LUID_AND_ATTRIBUTES[] arr;

        public PrivilegeSet(int privCount, int control, LUID_AND_ATTRIBUTES[] arr) {
            this.PrivCount = privCount;
            this.Control = control;
            this.arr = arr ?? throw new ArgumentNullException(nameof(arr));
        }
    }

    private readonly struct LUID_AND_ATTRIBUTES {
        private readonly int LUID_LowPart;
        private readonly long LUID_HighPart;
        private readonly int Attributes;
    }

    public enum MemoryState : int {
        MEM_COMMIT = 0x1_000,
        MEM_RESERVE = 0x2_000,
        MEM_FREE = 0x10_000,
    }

    [DllImport("kernel32.dll", PreserveSig = false, SetLastError = true)]
    private static extern int PrivilegeCheck(IntPtr processHandle, PrivilegeSet requiredPermissions, ref bool result);


    [DllImport("kernel32.dll", PreserveSig = true, SetLastError = true)]
    private static extern ulong VirtualQueryEx(IntPtr processHandle, IntPtr lpAddress, ref PMEMORY_BASIC_INFORMATION info, ulong info_sz /* size_t */);

    internal static PMEMORY_BASIC_INFORMATION? VirtualQueryEx(IntPtr processHandle, nint lpAddress) {
        PMEMORY_BASIC_INFORMATION outVar = new();
        var readBytesOrZeroIfFail = VirtualQueryEx(processHandle, lpAddress, ref outVar, (ulong)Marshal.SizeOf<PMEMORY_BASIC_INFORMATION>());
        if (readBytesOrZeroIfFail == 0) {
            //var lastError = GetLastError();
            //throw new Exception($"VirtualQueryEx failed: 0x{lastError:X}");
            return null;
        }
        return outVar;
    }
}
