using SteamEngineSimController.MemoryHelpers;
using System.Runtime.InteropServices;
using System.Text;

namespace SteamEngineSimController.Types;

// std::string implementation in Microsoft STL
[StructLayout(LayoutKind.Sequential)]
public readonly struct StandardString {

    private const int bufferSize = 16;
    private const int smallStringCapacity = bufferSize - 1;        // extra one space for null terminator

    private readonly StringValue value;
    private readonly nuint size;
    private readonly nuint capacity;

    [StructLayout(LayoutKind.Explicit, Size = bufferSize)]
    private unsafe struct StringValue {
        [FieldOffset(0)]
        public fixed byte buffer[bufferSize];  // Short string optimization if Capacity <= SmallStringCapacity
        [FieldOffset(0)]
        public readonly IntPtr externalBuffer;    // if Capacity > SmallStringCapacity

    }


    public unsafe readonly string ToString(nint gameHandle) {
        if (capacity <= smallStringCapacity) {
            fixed (byte* buffer = value.buffer) {
                return Encoding.UTF8.GetString(buffer, (int)size);
            }
        }
        else {
            return Encoding.UTF8.GetString(KernelMethods.ReadMemory(gameHandle, value.externalBuffer, (uint)size));
        }
    }
}
