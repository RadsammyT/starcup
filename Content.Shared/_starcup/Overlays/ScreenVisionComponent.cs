using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._starcup.Overlays;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class ScreenVisionComponent : Component
{
    /// <summary>
    ///     Should the shader have damage effects?
    /// </summary>
    [DataField] public bool DamageEffects = false;
    /// <summary>
    ///     If the entity is incapacitated (Crit or dead), intensify effects by this amount.
    /// </summary>
    [DataField] public float IncapacitatedMultiplier = 3;

    //Below are parameters for the shader.


    [DataField] public float ShaderBrightness = 1;
    [DataField] public float ShaderContrast = 1;
    [DataField] public float ShaderSaturation = 1;
    [DataField] public float ShaderGamma = 1;
    [DataField] public float ShaderCurvature = 0;
    [DataField] public float ShaderCornerSoften = 0;
    [DataField] public float ShaderVignette = 0;
    [DataField] public float ShaderScanlineStrength = 0.95f;
    [DataField] public float ShaderScanlineDensity = 1.15f;
    [DataField] public float ShaderInterlaceStrength = 0.2f;
    [DataField] public float ShaderMaskStrength = 0.22f;
    [DataField] public float ShaderMaskScale = 1;
    [DataField] public float ShaderChromaOffsetPx = 1.6f;
    [DataField] public float ShaderLumaSmearPx = 2.2f;
    [DataField] public float ShaderJitterPx = 0;
    [DataField] public float ShaderWobblePx = 0;
    [DataField] public float ShaderTapeNoise = 0;
    [DataField] public float ShaderTapeLines = 0;
    [DataField] public float ShaderRollSpeed = 0.1f;
    [DataField] public float ShaderRollStrength = 0.01f;
    [DataField] public float ShaderGlowStrength = 0.25f;
    [DataField] public float ShaderGlowThreshold = 0.65f;
    private ISerializationGenerated<ScreenVisionComponent> _serializationGeneratedImplementation;
}
