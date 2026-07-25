using System.Numerics;
using Content.Shared._starcup.Overlays;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._starcup.Overlays;

public sealed partial class ScreenVisionOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private IEntitySystemManager _sysMan = default!;

    private readonly DamageableSystem _damageable = default!;
    private readonly MobStateSystem _mobState = default!;
    private readonly MobThresholdSystem _mobThres = default!;
    private static readonly ProtoId<ShaderPrototype> ScreenVision = "ScreenVision";

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _screenVisionShader;

    public ScreenVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _screenVisionShader = _prototypeManager.Index(ScreenVision).Instance().Duplicate();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { Valid: true } player
            || !_entityManager.HasComponent<ScreenVisionComponent>(player))
        {
            return false;
        }

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        _screenVisionShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        // Propagate component fields to shader

        var unverifiedEntity = _playerManager.LocalEntity;
        if (unverifiedEntity is { Valid: true } entity)
        {
            var damagableExists = _entityManager.TryGetComponent(entity, out DamageableComponent? damageable);
            var mobStateExists = _entityManager.TryGetComponent(entity, out MobStateComponent? mobState);
            var mobThresholdExists = _entityManager.TryGetComponent(entity, out MobThresholdsComponent? mobThreshold);
            var screenVisionExists = _entityManager.TryGetComponent(entity, out ScreenVisionComponent? screenVision);
            if (damagableExists && mobStateExists && screenVisionExists && mobThresholdExists)
            {
                var dmg = _damageable.GetPositiveDamage((entity, damageable!));
                var total = dmg.GetTotal().Float();
                var incapacitated = _mobState.IsIncapacitated(entity, mobState!);
                var incapacitatedModifier = incapacitated  ? screenVision!.IncapacitatedMultiplier : 1;
                var comp = screenVision!;
                var crit = _mobThres.TryGetThresholdForState(entity, MobState.Critical, out var critThres, mobThreshold);
                var hpIntoCrit = (incapacitated ? total - critThres : 100)!.Value.Float();
                _screenVisionShader.SetParameter("brightness", comp.ShaderBrightness);
                _screenVisionShader.SetParameter("contrast", comp.ShaderContrast);
                _screenVisionShader.SetParameter("saturation", comp.ShaderSaturation);
                _screenVisionShader.SetParameter("gamma", comp.ShaderGamma);

                // hp into crit / 100 else 0
                _screenVisionShader.SetParameter("curvature", incapacitated ? comp.ShaderCurvature + (hpIntoCrit / 100) : comp.ShaderCurvature);
                _screenVisionShader.SetParameter("corner_soften", comp.ShaderCornerSoften);
                // hp into crit * 2 else comp
                _screenVisionShader.SetParameter("vignette", incapacitated ? comp.ShaderVignette + hpIntoCrit * 2 : comp.ShaderVignette);
                _screenVisionShader.SetParameter("scanline_strength", comp.ShaderScanlineStrength);
                _screenVisionShader.SetParameter("scanline_density", comp.ShaderScanlineDensity);
                _screenVisionShader.SetParameter("interlace_strength", comp.ShaderInterlaceStrength);
                _screenVisionShader.SetParameter("mask_strength", comp.ShaderMaskStrength);
                _screenVisionShader.SetParameter("mask_scale", comp.ShaderMaskScale);
                _screenVisionShader.SetParameter("chroma_offset_px", comp.ShaderChromaOffsetPx);
                _screenVisionShader.SetParameter("luma_smear_px", comp.ShaderLumaSmearPx);
                // hp into crit / 2 else (lost alive hp / 100) * 3
                _screenVisionShader.SetParameter("jitter_px", incapacitated ? comp.ShaderJitterPx + (hpIntoCrit / 2) +(total/100)*3  : comp.ShaderJitterPx + (total / 100)*3);
                _screenVisionShader.SetParameter("wobble_px", comp.ShaderWobblePx);
                _screenVisionShader.SetParameter("tape_noise", comp.ShaderTapeNoise);
                // hp into crit * 1 else lost alive hp / 100) * 0.2
                _screenVisionShader.SetParameter("tape_lines", incapacitated ? comp.ShaderTapeLines + hpIntoCrit : comp.ShaderTapeLines + (total / 100.0f) * 0.2f);
                _screenVisionShader.SetParameter("roll_speed", comp.ShaderRollSpeed);
                _screenVisionShader.SetParameter("roll_strength", comp.ShaderRollStrength);
                _screenVisionShader.SetParameter("glow_strength", comp.ShaderGlowStrength);
                _screenVisionShader.SetParameter("glow_threshold", comp.ShaderGlowThreshold);

            }
        }


        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_screenVisionShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null); // important - as of writing, construction overlay breaks without this
    }
}
