using System.Numerics;
using Content.Shared._starcup.Overlays;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._starcup.Overlays;

public sealed partial class ScreenVisionOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private IEntitySystemManager _sysMan = default!;

    private readonly DamageableSystem _damageable = default!;
    private readonly MobThresholdSystem _mobThres = default!;
    private readonly StatusEffectsSystem _status = default!;


    private static readonly ProtoId<ShaderPrototype> ScreenVision = "ScreenVision";

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _screenVisionShader;

    public float CritPercent = 0f;
    private float _oldCritPercent = 0f;

    public float DeathPercent = 0f;
    private float _oldDeathPercent = 0f;

    public float OilPercent = 0f;
    private float _oldOilPercent = 0f;

    public static readonly EntProtoId Bloodloss = "StatusEffectBloodloss";

    public ScreenVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _damageable = _sysMan.GetEntitySystem<DamageableSystem>();
        _mobThres = _sysMan.GetEntitySystem<MobThresholdSystem>();
        _status = _sysMan.GetEntitySystem<StatusEffectsSystem>();
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
        if (unverifiedEntity == null)
            goto early;
        var entity = unverifiedEntity.Value;

        var damagableExists = _entityManager.TryGetComponent(entity, out DamageableComponent? damageable);
        var mobStateExists = _entityManager.TryGetComponent(entity, out MobStateComponent? mobState);
        var mobThresholdExists = _entityManager.TryGetComponent(entity, out MobThresholdsComponent? mobThreshold);
        var screenVisionExists = _entityManager.TryGetComponent(entity, out ScreenVisionComponent? screenVision);

        var comp = screenVision!;
        if (!(damagableExists && mobStateExists &&  mobThresholdExists) || !screenVision!.DamageEffects)
        {
            _oldCritPercent = 0.0f;
            _oldDeathPercent = 0.0f;
            CritPercent = 0f;
            DeathPercent = 0f;
            goto render;
        }

        var time = (float) _timing.RealTime.TotalSeconds;
        var lastFrameTime = (float) _timing.FrameTime.TotalSeconds;

        if (!MathHelper.CloseTo(_oldDeathPercent, DeathPercent, 0.001f))
        {
            var diff = DeathPercent - _oldDeathPercent;
            _oldDeathPercent += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldDeathPercent = DeathPercent;
        }

        if (!MathHelper.CloseTo(_oldCritPercent, CritPercent, 0.001f))
        {
            var diff = CritPercent - _oldCritPercent;
            _oldCritPercent += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldCritPercent = CritPercent;
        }

        if (!MathHelper.CloseTo(_oldOilPercent, OilPercent, 0.001f))
        {
            var diff = OilPercent - _oldOilPercent;
            _oldOilPercent += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldOilPercent = OilPercent;
        }

        var dmg = _damageable.GetPositiveDamage((entity, damageable!));
        if (mobState!.CurrentState == MobState.Alive)
        {
            DeathPercent = 0;
            if (_mobThres.TryGetIncapPercentage(entity, dmg.GetTotal(), out var percentage, mobThreshold!))
                CritPercent = percentage!.Value.Float();
        }

        if (mobState!.CurrentState == MobState.Critical)
        {
            CritPercent = 1.0f;
            if (_mobThres.TryGetDeadPercentage(entity, dmg.GetTotal(), out var percentage, mobThreshold!))
                DeathPercent = percentage!.Value.Float();
        }

        if (mobState!.CurrentState == MobState.Dead)
        {
            CritPercent = 0.0f;
            DeathPercent = 0.0f;
        }

        if (_status.HasStatusEffect(entity, Bloodloss))
            OilPercent = 1f;
        else
            OilPercent = 0f;
render:
        _screenVisionShader.SetParameter("brightness", comp.ShaderBrightness);
        _screenVisionShader.SetParameter("contrast", comp.ShaderContrast);
        _screenVisionShader.SetParameter("saturation", comp.ShaderSaturation);
        _screenVisionShader.SetParameter("gamma", comp.ShaderGamma);

        // hp into crit / 100 else 0
        _screenVisionShader.SetParameter("curvature", comp.ShaderCurvature + (_oldDeathPercent));
        _screenVisionShader.SetParameter("corner_soften", comp.ShaderCornerSoften);
        // hp into crit * 2 else comp
        _screenVisionShader.SetParameter("vignette", comp.ShaderVignette + (_oldDeathPercent * 200));
        _screenVisionShader.SetParameter("scanline_strength", comp.ShaderScanlineStrength);
        _screenVisionShader.SetParameter("scanline_density", comp.ShaderScanlineDensity);
        _screenVisionShader.SetParameter("interlace_strength", comp.ShaderInterlaceStrength);
        _screenVisionShader.SetParameter("mask_strength", comp.ShaderMaskStrength);
        _screenVisionShader.SetParameter("mask_scale", comp.ShaderMaskScale);
        _screenVisionShader.SetParameter("chroma_offset_px", comp.ShaderChromaOffsetPx + (_oldOilPercent * 10));
        _screenVisionShader.SetParameter("luma_smear_px", comp.ShaderLumaSmearPx);
        // hp into crit / 2 else (lost alive hp / 100) * 3
        _screenVisionShader.SetParameter("jitter_px", comp.ShaderJitterPx + (_oldCritPercent * 3) + (_oldDeathPercent * 10));
        _screenVisionShader.SetParameter("wobble_px", comp.ShaderWobblePx);
        _screenVisionShader.SetParameter("tape_noise", comp.ShaderTapeNoise);
        // hp into crit * 1 else lost alive hp / 100) * 0.2
        _screenVisionShader.SetParameter("tape_lines", comp.ShaderTapeLines + (_oldCritPercent * 0.2f) + (_oldDeathPercent * 100));
        _screenVisionShader.SetParameter("roll_speed", comp.ShaderRollSpeed);
        _screenVisionShader.SetParameter("roll_strength", comp.ShaderRollStrength);
        _screenVisionShader.SetParameter("glow_strength", comp.ShaderGlowStrength);
        _screenVisionShader.SetParameter("glow_threshold", comp.ShaderGlowThreshold);

early:
        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_screenVisionShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null); // important - as of writing, construction overlay breaks without this
    }

    private float GetDiff(float value, float lastFrameTime)
    {
        var adjustment = value * 5f * lastFrameTime;

        if (value < 0f)
            adjustment = Math.Clamp(adjustment, value, -value);
        else
            adjustment = Math.Clamp(adjustment, -value, value);

        return adjustment;
    }

}
