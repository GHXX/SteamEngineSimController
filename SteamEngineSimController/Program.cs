using SteamEngineSimController.MemoryHelpers;
using System.Diagnostics;
using System.Globalization;

namespace SteamEngineSimController;

internal class Program {
    private static MemoryLocation<float> memlocReverser = null!;
    //private static MemoryLocation<float> memlocGeneratorSpeed = null!;

    private static nint gameHandle = -1;
    private static IntPtr generatorSpeedPtr = -1;
    private static float GeneratorSpeed {
        get {
            var rpmText = string.Join("", KernelMethods.ReadMemory(gameHandle, generatorSpeedPtr, 16).TakeWhile(x => x != '\0').Select(x => (char)x));
            var rpmValue = rpmText.EndsWith(" RPM") && float.TryParse(string.Join("", rpmText.SkipLast(" RPM".Length)), CultureInfo.InvariantCulture, out var res) ? res : -1;
            if (rpmValue < 0) {
                throw new Exception(); // maybe clean this up loll
            }
            return rpmValue;
        }
    }


    private static PID reverserPid = null!;

    private static void Main(string[] args) {
        Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        Console.Title = "Steam engine sim controller";
        while (true) {
            try {
                var proc = Process.GetProcessesByName("atg-steam-engine-demo").Single();

                Init(proc);
                while (!proc.HasExited) {
                    Update(proc);
                    Thread.Sleep(500);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Caught fatal exception: {ex}");
            }
            Thread.Sleep(1000);
        }
    }

    private static int timeSecondsNow = 0;
    private const double desiredGeneratorSpeed = 1150;
    private static void Update(Process proc) {
        timeSecondsNow++;
        var newReverserSetting = Math.Clamp(reverserPid.Step(timeSecondsNow, desiredGeneratorSpeed, GeneratorSpeed), 0, 1);
        memlocReverser.SetValue((float)newReverserSetting);
        Console.Clear();
        Console.WriteLine($"""
            New values:
            New reverser speed: {newReverserSetting:N3}; {reverserPid.StateString}
            """);
    }

    private static void Init(Process proc) {
        nint handle = proc.Handle;
        gameHandle = handle;
        var ep = proc.MainModule!.BaseAddress; // AKA ENTRY aka 14::
        var procMemLen = proc.MainModule!.ModuleMemorySize;

        var pages = MemoryUtil.GetMemoryPages(handle).Where(x => x.Protect == KernelMethods.FlPageProtect.PAGE_READWRITE).ToArray();

        nint[] FindMultipleWidgetValueAddresses(string pattern) {
            var matches = MemoryUtil.FindMemoryWithWildcardsAcrossALLPages(handle,
            pattern.Split(' ')
            .Select(x => (byte?)byte.Parse(x, NumberStyles.HexNumber)).ToArray(), pages);
            return matches.Select(x => x.Key).ToArray();
        }

        nint FindWidgetValueAddress(string pattern, int offset = -8) {
            var matches = MemoryUtil.FindMemoryWithWildcardsAcrossALLPages(handle,
            pattern.Split(' ')
            .Select(x => (byte?)byte.Parse(x, NumberStyles.HexNumber)).ToArray(), pages);
            return matches.Single().Key + offset; // offset because the value we are looking for is before the inlined 25...-string
        }
        var reverserAddress = FindWidgetValueAddress("25 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 0F 00 00 00 00 00 00 00 52 45 56 45 52 53 45 52 00 00 00 00");
        var brakeStopAddress = FindWidgetValueAddress("25 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 0F 00 00 00 00 00 00 00 42 52 41 4B 45 20 53 54 4F 50 00 00 00 00 00 00");
        var whistleAddress = FindWidgetValueAddress("25 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 0F 00 00 00 00 00 00 00 57 48 49 53 54 4C 45 00 00 00 00 00 00 00 00 00");
        var genRpmTextAddressCandidates = FindMultipleWidgetValueAddresses("47 45 4E 45 52 41 54 4F 52 20 53 50 45 45 44 00").Select(x => x + 38).ToArray();
        var generatorRpmTextAddresses = genRpmTextAddressCandidates.Where(c => {
            var stringRep = string.Join("", KernelMethods.ReadMemory(handle, c - 7, 64).Select(x => (char)x));
            return stringRep.Replace(" RPM", "").Length == stringRep.Length - 2 * " RPM".Length;
        }).ToArray();
        var generatorRpmTextAddress = generatorRpmTextAddresses.Single() - 6;
        generatorSpeedPtr = generatorRpmTextAddress;
        //var rpmText = string.Join("", KernelMethods.ReadMemory(handle, generatorRpmTextAddress, 16).TakeWhile(x => x != '\0').Select(x=>(char)x));
        //var rpmValue = rpmText.EndsWith(" RPM") && float.TryParse(string.Join("", rpmText.SkipLast(" RPM".Length)), CultureInfo.InvariantCulture, out var res) ? res : -1;

        //    var steamEngineVisualizationPrivFieldPtr = MemoryUtil.FindMemoryWithWildcardsAcrossALLPages(handle,
        //new[] { brakeStopAddress, whistleAddress, reverserAddress }.Select(x => BitConverter.GetBytes(x - i).ToArray())
        //                                                           .Aggregate((a, b) => a.Concat(b).ToArray())
        //                                                           .Select(x => (byte?)x)
        //                                                           .ToArray(), pages);

        //for (int i = 544; i <= 4096; i += 4) { // Main struct offset discovery

        //    var steamEngineVisualizationPrivFieldPtr = MemoryUtil.FindMemoryWithWildcardsAcrossALLPages(handle,
        //        new[] { brakeStopAddress, whistleAddress, reverserAddress }.Select(x => BitConverter.GetBytes(x - i).ToArray())
        //                                                                   .Aggregate((a, b) => a.Concat(b).ToArray())
        //                                                                   .Select(x => (byte?)x)
        //                                                                   .ToArray(), pages);
        //    if (steamEngineVisualizationPrivFieldPtr.Count != 0) {
        //        Console.WriteLine($"found at -{i}");
        //    }
        //}

        //var windowEventHandlerHolder = MemoryUtil.FindMemoryWithWildcards(handle, ep, (uint)procMemLen, "00,00,7F,F6,16,D7,30,40".Split(",").Select(x => (byte?)Convert.ToByte(x, 16)).ToArray());
        //var windowEventHandlerHolder2 = MemoryUtil.FindMemoryWithWildcards(handle, (nint)0x00007FF616D71000, (uint)0x0000000000050000+0x23000, "00,00,7F,F6,16,D7,30,40".Split(",").Select(x => (byte?)Convert.ToByte(x, 16)).ToArray());
        //long reverserOffset = 0x227AC773750; //0x1DAEBEE0450 - 0x00007ff616d66ba0 + ep;
        //long generatorSpeedOffset = reverserOffset + 0x12E51800;
        memlocReverser = new MemoryLocation<float>(handle, (nint)reverserAddress);
        //memlocGeneratorSpeed = new MemoryLocation<float>(handle, (nint)generatorSpeedOffset);

        reverserPid = new PID(0.01, 1e-5, 0, memlocReverser.GetValue(), false, (0, 1));
    }
}
