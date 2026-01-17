using System.Runtime.InteropServices;

namespace SteamEngineSimController.Types;

[StructLayout(LayoutKind.Sequential)]
public readonly struct SteamEngineVisualizationPartial {
    // some leading fields are probably missing; most fields known thanks to ange

    public readonly IntPtr m_engineBridge;
    public readonly IntPtr m_simulation;
    public readonly IntPtr m_simulationBridge;

    public readonly IntPtr m_flame;
    public readonly IntPtr m_overflowBall, m_overflowArrow;
    public readonly IntPtr m_pressureNeedle, m_pressureChangeNeedle,
                           m_waterTemperatureNeedle, m_waterTemperatureChangeNeedle,
                           steamTemperatureNeedle, m_rpmNeedle,
                           m_maxPressureNeedle;

    public readonly IntPtr m_waterPump, m_waterFilter,
                           m_waterPipe, m_waterFilterSensor;

    public readonly IntPtr m_waterDisplay, m_sedimentDisplay;
    public readonly IntPtr m_heatElement;
    public readonly IntPtr m_burner;
    public readonly IntPtr m_throttleValve, m_blowoffValveAdjustment;
    public readonly IntPtr m_blowOffValve;
    public readonly IntPtr m_steamWhistlehandle;
    public readonly IntPtr m_blowOffValveSpringObject;

    public readonly IntPtr waterPumpVis;

    public readonly IntPtr steamParticleForceField, cylinderVis, tankObject, scythe, damageToggle;

    public readonly IntPtr heatSlider, heatOffsetSlider;
    public readonly IntPtr pressureMarker, waterTemperatureMarker, waterLevelMarker, waterFlowRatemarker, engineSpeedMarker;

    public readonly IntPtr throttleSlider, blowOffValveSlider, starterSlider, brakeSlider;
    public readonly IntPtr brakeStopSlider, whistleSliger, reverserSlider; // pattern to be searched

    public readonly IntPtr pressureReadout, waterTempReadout, waterLevelReadout;
}

// RadialSlider size: 1296=0x510 bytes (confirmed via cheatengine mem layout, reverser and prv and throttle appear stacked after eachother, like in an array of structs)