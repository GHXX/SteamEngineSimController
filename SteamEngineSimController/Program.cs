using SteamEngineSimController.MemoryHelpers;
using SteamEngineSimController.Types;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace SteamEngineSimController;

internal class Program {
    private static MemoryLocation<float> memlocReverser = null!;
    private static MemoryLocation<float> memlocBrakeStop = null!;
    private static MemoryLocation<float> memlocEngineSpeedMarker = null!;
    private static MemoryLocation<float> memlocBoilerPressureMarker = null!;
    private static MemoryLocation<float> memlocActualHeat = null!;
    private static MemoryLocation<float> memlocDesiredHeat = null!;
    private static MemoryLocation<float> memlocWaterPump = null!;
    private static MemoryLocation<float> memlocWaterLevelMarker = null!;
    private static SteamEngineVisualizationPartial steamEngineVisualizationStruct = default;
    //private static ReadOnlyMemoryLocation<float> memlocBoilerPressure = null!;

    public float BoilerPressureMarker350 { get => memlocBoilerPressureMarker.GetValue() * 350; set => memlocBoilerPressureMarker.SetValue(value / 350); }

    /// <summary>
    /// Unit: CC (as ingame), The raw value ranges from 0 to 1
    /// </summary>
    public float WaterLevelMarker80 { get => memlocWaterLevelMarker.GetValue() * 80; set => memlocWaterLevelMarker.SetValue(value / 80); }


    // X: heater level in memory; Y:
    private static double[] desiredHeatConversionCoeffs = [5.68319704, -10.60034555]; //[5.62529347, -10.52847886];
    private static float DesiredHeat01 {
        get {
            var x = memlocDesiredHeat.GetValue();
            var y = Math.Exp(desiredHeatConversionCoeffs[0] * x + desiredHeatConversionCoeffs[1]);
            return (float)y;
        }

        set {
            var y = value;

            double x;
            if (y < 0.01) {
                x = 0;
            } else if (y > 1 - 0.01) {
                x = 1.875;
            } else {
                x = (double)((Math.Log(y) - desiredHeatConversionCoeffs[1]) / desiredHeatConversionCoeffs[0]);
            }

            memlocDesiredHeat.SetValue((float)x);
        }

    }

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

    private static float BoilerPressure {
        get {

            var stringAddress = steamEngineVisualizationStruct.pressureReadout + 0x260;
            var psiText = MemoryUtil.ReadString(gameHandle, stringAddress);

            float rv = 0;
            if (psiText.EndsWith(" PSI") && float.TryParse(string.Join("", psiText.SkipLast(" PSI".Length)), CultureInfo.InvariantCulture, out var res)) {
                rv = res;
            } else if (psiText.Contains("IN HG") && float.TryParse(psiText.Split("IN HG")[0], out var res2)) {
                rv = -res2;
            } else {
                throw new Exception("Failed to parse boiler pressure string");
            }
            return rv;

        }
    }

    private static float BoilerWaterLevel {
        get {
            var stringAddress = steamEngineVisualizationStruct.waterLevelReadout + 0x260;
            string ccText = MemoryUtil.ReadString(gameHandle, stringAddress);

            float rv = 0;
            if (ccText.EndsWith(" CC") && float.TryParse(string.Join("", ccText.SkipLast(" CC".Length)), CultureInfo.InvariantCulture, out var res)) {
                rv = res;
            } else {
                throw new Exception("Failed to parse water level string");
            }
            return rv;
        }
    }


    private static PID reverserPid = null!;
    private static PID heatPid = null!;
    private static PID waterLevelPid = null!;

    private static ConsoleDoubleBuffered console = new();

    private static void Main(string[] args) {
        console.DisableBuffering = true;
        Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        Console.Title = "Steam engine sim controller";
        while (true) {
            try {
                var proc = Process.GetProcessesByName("atg-steam-engine-demo").Single();
                console.DisableBuffering = true;
                Init(proc);
                console.DisableBuffering = false;
                while (!proc.HasExited) {
                    Update(proc);
                    console.ShowBuffer();
                    Thread.Sleep(500);
                }
            } catch (Exception ex) {
                console.DisableBuffering = true;
                console.WriteLine($"Caught fatal exception: {ex}");
            }
            Thread.Sleep(1000);
        }
    }

    private static int timeSecondsNow = 0;
    //private const double desiredGeneratorSpeed = 1150;
    private static float lastGenSpeed = 0;
    private static void Update(Process proc) {
        timeSecondsNow++;
        var genSpeedNow = GeneratorSpeed;
        var engineSpeedMarkerPos = memlocEngineSpeedMarker.GetValue();
        var desiredGenSpeed = engineSpeedMarkerPos * 1200;
        var newReverserSetting = Math.Clamp(reverserPid.Step(timeSecondsNow, desiredGenSpeed, genSpeedNow), 0, 1);
        memlocReverser.SetValue((float)Math.Clamp(newReverserSetting, 0.5, 1));
        memlocBrakeStop.SetValue((float)(Math.Clamp((-newReverserSetting * 2 + 1), 0, 1)));

        var desiredPressure = memlocBoilerPressureMarker.GetValue();
        var currBoilerPressure = BoilerPressure;
        var newHeatSetting = Math.Clamp(heatPid.Step(timeSecondsNow, desiredPressure * 350, currBoilerPressure), 0, 1);
        DesiredHeat01 = (float)newHeatSetting;

        var desiredWaterLevel = memlocWaterLevelMarker.GetValue() * 80; // 0-80CC
        var currWaterLevel = BoilerWaterLevel; // 0-80CC
        var newWaterInletSetting = Math.Clamp(waterLevelPid.Step(timeSecondsNow, desiredWaterLevel, currWaterLevel), 0, 1);
        memlocWaterPump.SetValue((float)newWaterInletSetting);
        //var currBoilerWaterLevel = 

        //Console.Clear();
        console.WriteLine($"""
            Marker pos: {engineSpeedMarkerPos * 700:N2} --> Desired generator speed: {desiredGenSpeed}
            Generator speed delta: {genSpeedNow - lastGenSpeed}
            Boiler pressure: {currBoilerPressure} PSI
            Water level: {BoilerWaterLevel} CC

            New values:
            New reverser speed: {newReverserSetting:N3}; {reverserPid.StateString}
            New heat setting: {newHeatSetting:N3}; {heatPid.StateString}
            New inlet setting: {newWaterInletSetting:N3}
            """);
        lastGenSpeed = genSpeedNow;
    }

    private static void Init(Process proc) {
        nint handle = proc.Handle;
        gameHandle = handle;
        var ep = proc.MainModule!.BaseAddress; // AKA ENTRY aka 14::
        var procMemLen = proc.MainModule!.ModuleMemorySize;


        console.WriteLine("Getting ram pages...");
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
        console.WriteLine("Searching widgets...");
        var reverserAddress = FindWidgetValueAddress("25 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 0F 00 00 00 00 00 00 00 52 45 56 45 52 53 45 52 00 00 00 00");
        var brakeStopAddress = FindWidgetValueAddress("25 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 0F 00 00 00 00 00 00 00 42 52 41 4B 45 20 53 54 4F 50 00 00 00 00 00 00");
        var whistleAddress = FindWidgetValueAddress("25 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 0F 00 00 00 00 00 00 00 57 48 49 53 54 4C 45 00 00 00 00 00 00 00 00 00");
        var heatAddress = FindWidgetValueAddress("25 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 0F 00 00 00 00 00 00 00 48 45 41 54 00 00 00");
        var genRpmTextAddressCandidates = FindMultipleWidgetValueAddresses("47 45 4E 45 52 41 54 4F 52 20 53 50 45 45 44 00").Select(x => x + 38).ToArray();
        var generatorRpmTextAddresses = genRpmTextAddressCandidates.Where(c => {
            byte[] res;
            try {
                res = KernelMethods.ReadMemory(handle, c - 7, 64);
            } catch (Exception) {
                return false;
            }
            var stringRep = string.Join("", res.Select(x => (char)x));
            return stringRep.Replace(" RPM", "").Length == stringRep.Length - 2 * " RPM".Length;
        }).ToArray();
        var generatorRpmTextAddress = generatorRpmTextAddresses.Single() - 6;
        generatorSpeedPtr = generatorRpmTextAddress;
        //var rpmText = string.Join("", KernelMethods.ReadMemory(handle, generatorRpmTextAddress, 16).TakeWhile(x => x != '\0').Select(x=>(char)x));
        //var rpmValue = rpmText.EndsWith(" RPM") && float.TryParse(string.Join("", rpmText.SkipLast(" RPM".Length)), CultureInfo.InvariantCulture, out var res) ? res : -1;

        console.WriteLine("Searching for main game object...");
        var steamEngineVisualizationKnownFieldOffset = MemoryUtil.FindMemoryWithWildcardsAcrossALLPages(handle,
            new[] { brakeStopAddress, whistleAddress, reverserAddress }
            .Select(x => BitConverter.GetBytes(x - 544).ToArray())
            .Aggregate((a, b) => a.Concat(b).ToArray())
            .Select(x => (byte?)x)
            .ToArray(), pages).Single().Key;


        var brakeStopStructOffset = Marshal.OffsetOf<SteamEngineVisualizationPartial>(nameof(SteamEngineVisualizationPartial.brakeStopSlider));
        var steamEngineVizOffset = steamEngineVisualizationKnownFieldOffset - brakeStopStructOffset;
        console.WriteLine($"Main game object found at roughly 0x{steamEngineVizOffset:X8}");
        var steamEngineVizStruct = MemoryUtil.ReadStruct<SteamEngineVisualizationPartial>(gameHandle, steamEngineVizOffset);

        #region MainStructRediscoveryCode
        //for (int i = 544; i <= 4096; i += 4) { // Main struct offset discovery

        //    var steamEngineVisualizationPrivFieldPtr = MemoryUtil.FindMemoryWithWildcardsAcrossALLPages(handle,
        //        new[] { brakeStopAddress, whistleAddress, reverserAddress }.Select(x => BitConverter.GetBytes(x - i).ToArray())
        //                                                                   .Aggregate((a, b) => a.Concat(b).ToArray())
        //                                                                   .Select(x => (byte?)x)
        //                                                                   .ToArray(), pages);
        //    if (steamEngineVisualizationPrivFieldPtr.Count != 0) {
        //        console.WriteLine($"found at -{i}");
        //    }
        //}

        //var windowEventHandlerHolder = MemoryUtil.FindMemoryWithWildcards(handle, ep, (uint)procMemLen, "00,00,7F,F6,16,D7,30,40".Split(",").Select(x => (byte?)Convert.ToByte(x, 16)).ToArray());
        //var windowEventHandlerHolder2 = MemoryUtil.FindMemoryWithWildcards(handle, (nint)0x00007FF616D71000, (uint)0x0000000000050000+0x23000, "00,00,7F,F6,16,D7,30,40".Split(",").Select(x => (byte?)Convert.ToByte(x, 16)).ToArray());
        //long reverserOffset = 0x227AC773750; //0x1DAEBEE0450 - 0x00007ff616d66ba0 + ep;
        //long generatorSpeedOffset = reverserOffset + 0x12E51800;
        #endregion


        var mainVizMempage = pages.Single(x => x.BaseAddress <= steamEngineVisualizationKnownFieldOffset && steamEngineVisualizationKnownFieldOffset <= x.BaseAddress + (int)x.Length);
        //var objectToLookFor = 0x166F45642E0; // search this offset in the main viz struct;
        //for (int i = 0; i <= 4096; i += 4) { // Main struct offset discovery

        //    var fieldPtrCandidate = MemoryUtil.FindMemoryWithWildcardsAcrossALLPages(handle,
        //        new[] { objectToLookFor }.Select(x => BitConverter.GetBytes(x - i).ToArray())
        //                                 .Aggregate((a, b) => a.Concat(b).ToArray())
        //                                 .Select(x => (byte?)x)
        //                                 .ToArray(), [mainVizMempage]);

        //    if (fieldPtrCandidate.Count != 0) {
        //        console.WriteLine($"found at field at -{i}: at {(string.Join(", ", fieldPtrCandidate.Keys.Select(x=>$"0x{x:X}")))}");
        //    }
        //}

        steamEngineVisualizationStruct = steamEngineVizStruct;
        var sliderValueOffset = 544;
        Assert(steamEngineVizStruct.reverserSlider == reverserAddress - sliderValueOffset, "reverser address");
        Assert(steamEngineVizStruct.brakeStopSlider == brakeStopAddress - sliderValueOffset, "brake stop address");
        memlocEngineSpeedMarker = new MemoryLocation<float>(gameHandle, MemoryUtil.ReadValue<IntPtr>(gameHandle, steamEngineVizStruct.engineSpeedMarker + 0x270) + 0x220);

        memlocReverser = new MemoryLocation<float>(handle, reverserAddress);
        memlocBrakeStop = new MemoryLocation<float>(handle, brakeStopAddress);
        memlocActualHeat = new MemoryLocation<float>(handle, steamEngineVizStruct.heatSlider + sliderValueOffset + 0xe8);
        memlocDesiredHeat = new MemoryLocation<float>(handle, steamEngineVizStruct.heatSlider + sliderValueOffset + 0xa4); // seems to range from 0 to 1.875 and be scaled exponentially?

        T deref<T>(IntPtr x) => MemoryUtil.ReadValue<T>(gameHandle, x);

        int waterPumpVis_waterValveSliderOffset = 0x2b0; // offset to the RadialSlider m_waterValveSlider field in WaterPumpVisualization
        var waterPumpRadialSliderAddress = deref<IntPtr>(steamEngineVisualizationStruct.waterPumpVis + waterPumpVis_waterValveSliderOffset);
        memlocWaterPump = new MemoryLocation<float>(handle, waterPumpRadialSliderAddress + sliderValueOffset);

        //var boilerPressurePtr_lVar1 = MemoryUtil.ReadValue<IntPtr>(handle, steamEngineVizStruct.pressureReadout + 0x440);
        //var boilerPressurePtr_lVar1_2 = MemoryUtil.ReadValue<IntPtr>(handle, steamEngineVizStruct.pressureReadout + 0x438);
        memlocBoilerPressureMarker = new ReadOnlyMemoryLocation<float>(handle, MemoryUtil.ReadValue<IntPtr>(gameHandle, steamEngineVizStruct.pressureMarker + 0x270) + 0x220);
        memlocWaterLevelMarker = new ReadOnlyMemoryLocation<float>(handle, MemoryUtil.ReadValue<IntPtr>(gameHandle, steamEngineVizStruct.waterLevelMarker + 0x270) + 0x220);

        //memlocGeneratorSpeed = new MemoryLocation<float>(handle, (nint)generatorSpeedOffset);

        reverserPid = new PID(0.01, 5e-5, 0, memlocReverser.GetValue(), false, (0, 1));
        heatPid = new PID(0.01, 1e-4, 0, DesiredHeat01, false, (0, 1));
        waterLevelPid = new PID(0.1, 1e-3, 0, memlocWaterPump.GetValue(), false, (0, 1));
    }
}
