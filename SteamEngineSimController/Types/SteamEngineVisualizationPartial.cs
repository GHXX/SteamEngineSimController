using System.Runtime.InteropServices;

namespace SteamEngineSimController.Types;

[StructLayout(LayoutKind.Sequential)]
public readonly struct SteamEngineVisualizationPartial {
    // some leading fields are omitted entirely.

    public readonly IntPtr waterPumpVis;

    public readonly IntPtr steamParticleForceField, cylinderVis, tankObject, scythe, damageToggle;

    public readonly IntPtr heatSlider, heatOffsetSlider;
    public readonly IntPtr pressureMarker, waterTemperatureMarker, waterLevelMarker, waterFlowRatemarker, engineSpeedMarker;

    public readonly IntPtr throttleSlider, blowOffValveSlider, starterSlider, brakeSlider;
    public readonly IntPtr brakeStopSlider, whistleSliger, reverserSlider; // pattern to be searched

    public readonly IntPtr pressureReadout;
}
