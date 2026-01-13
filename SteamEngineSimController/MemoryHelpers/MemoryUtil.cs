using System.Runtime.InteropServices;
using static SteamEngineSimController.MemoryHelpers.KernelMethods;

namespace SteamEngineSimController.MemoryHelpers;
public static class MemoryUtil {

    private static Dictionary<(nint, nint, uint), byte[]> FindMemoryWithWildcards_cache = new Dictionary<(nint, nint, uint), byte[]>();
    public static Dictionary<nint, byte[]> FindMemoryWithWildcards(nint gameHandle, nint start, uint length, byte?[] bytesToFind) {
        var foundElements = new Dictionary<nint, byte[]>();

        byte[] bytes;
        if (!FindMemoryWithWildcards_cache.TryGetValue((gameHandle, start, length), out bytes!)) {
            bytes = KernelMethods.ReadMemory(gameHandle, start, length);
            FindMemoryWithWildcards_cache.Add((gameHandle, start, length), bytes);
        }

        if (bytesToFind.Contains(null)) {

            for (int basePos = 0; basePos < bytes.Length - bytesToFind.Length; basePos += 1) {
                bool found = true;
                for (int i = 0; i < bytesToFind.Length; i++) {
                    var currByte = bytes[basePos + i];
                    if (bytesToFind[i].HasValue && bytesToFind[i]!.Value != currByte) {
                        found = false;
                        break;
                    }

                }

                if (found) {
                    foundElements.Add(nint.Add(start, basePos), bytes.Skip(basePos).Take(bytesToFind.Length).ToArray());
                }
            }

        } else { // much faster variant if we dont use any special filtering. Could technically extend this also to filtering if the first value is not a wildcard.
            var nonNullFilter = bytesToFind.Select(x => x!.Value).ToArray();

            var currStartPos = 0;

            do {
                var foundCandidate = Array.IndexOf(bytes, nonNullFilter[0], currStartPos);
                if (foundCandidate == -1) break;

                bool found = true;
                for (int i = 1; i < nonNullFilter.Length; i++) {
                    var idx = foundCandidate + i;
                    if (idx >= bytes.Length)
                        break;

                    var currByte = bytes[idx];
                    if (nonNullFilter[i] != currByte) {
                        found = false;
                        break;
                    }
                }

                if (found) {
                    foundElements.Add(nint.Add(start, foundCandidate), bytes.Skip(foundCandidate).Take(nonNullFilter.Length).ToArray());
                }


                currStartPos = foundCandidate+1;
            }
            while (currStartPos + nonNullFilter.Length <= bytes.Length);
        }

        return foundElements;
    }


    public static T ReadValue<T>(nint gameHandle, nint location) {
        var bytes = KernelMethods.ReadMemory(gameHandle, location, (uint)Marshal.SizeOf<T>());
        var t = typeof(T);

        switch (Type.GetTypeCode(t)) {
            case TypeCode.Byte:
                return (dynamic)bytes[0];

            case TypeCode.Int16:
                return (dynamic)BitConverter.ToInt16(bytes, 0);
            case TypeCode.Int32:
                return (dynamic)BitConverter.ToInt32(bytes, 0);
            case TypeCode.Int64:
                return (dynamic)BitConverter.ToInt64(bytes, 0);
            case TypeCode.UInt16:
                return (dynamic)BitConverter.ToUInt16(bytes, 0);
            case TypeCode.UInt32:
                return (dynamic)BitConverter.ToUInt32(bytes, 0);
            case TypeCode.UInt64:
                return (dynamic)BitConverter.ToUInt64(bytes, 0);
            case TypeCode.Single:
                return (dynamic)BitConverter.ToSingle(bytes, 0);
            case TypeCode.Double:
                return (dynamic)BitConverter.ToDouble(bytes, 0);

            case TypeCode.Object:
                switch (t.FullName) {
                    case "System.IntPtr":
                        return (dynamic)new nint(BitConverter.ToInt64(bytes, 0));
                    default:
                        throw new NotImplementedException("Invalid obj type");
                }

            default:
                throw new NotImplementedException("Invalid type");
        }
    }

    public static void WriteValue<T>(nint handle, nint location, T newValue) {
        var t = typeof(T);
        byte[] bytesToWrite;
        switch (Type.GetTypeCode(t)) {
            case TypeCode.Byte:
                bytesToWrite = [(byte)(object)newValue]; ;
                break;
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
                bytesToWrite = BitConverter.GetBytes((dynamic)newValue);
                break;
            case TypeCode.Object:
                switch (newValue) {
                    case nint v:
                        bytesToWrite = BitConverter.GetBytes(v.ToInt64());
                        break;
                    default:
                        throw new NotImplementedException("Invalid obj type");
                }
                break;
            default:
                throw new NotImplementedException("Invalid type");
        }
        KernelMethods.WriteMemory(handle, location, bytesToWrite);
    }

    public readonly record struct MemoryPage() {
        public readonly nint BaseAddress;
        public readonly ulong Length;
        public readonly FlPageProtect Protect;

        public MemoryPage(PMEMORY_BASIC_INFORMATION x) : this() {
            this.BaseAddress = x.BaseAddress;
            this.Length = x.RegionSize;
            this.Protect = x.Protect;
        }
    }

    public static MemoryPage[] GetMemoryPages(nint gameHandle) {
        var rv = new List<PMEMORY_BASIC_INFORMATION>();
        ulong currPageStartAddress = 0;
        while (true) {
            var pageOrNull = VirtualQueryEx(gameHandle, (nint)currPageStartAddress);
            if (pageOrNull == null) break;
            var page = pageOrNull.Value;
            if (page.State == MemoryState.MEM_COMMIT) {
                rv.Add(page);
                //Console.WriteLine($"{(page.State == MemoryState.MEM_COMMIT ? "GOOD" : "BAD")} " + page.ToString());
            }
            currPageStartAddress = (ulong)page.BaseAddress + page.RegionSize;
        }
        return rv.Select(x => new MemoryPage(x)).ToArray();
    }

    public static Dictionary<nint, byte[]> FindMemoryWithWildcardsAcrossALLPages(nint gameHandle, byte?[] bytesToFind) {
        var pages = GetMemoryPages(gameHandle);
        return FindMemoryWithWildcardsAcrossALLPages(gameHandle, bytesToFind, pages);
    }

    public static Dictionary<nint, byte[]> FindMemoryWithWildcardsAcrossALLPages(nint gameHandle, byte?[] bytesToFind, MemoryPage[] pages) {
        var rv = new Dictionary<nint, byte[]>();
        for (int i = 0; i < pages.Length; i++) {
            MemoryPage p = pages[i];
            if (p.Length > uint.MaxValue) throw new NotImplementedException();

            var matches = FindMemoryWithWildcards(gameHandle, p.BaseAddress, (uint)p.Length, bytesToFind);
            if (matches.Count > 0)
                foreach (var match in matches) {
                    rv.Add(match.Key, match.Value);
                }
        }
        return rv;
    }
}
