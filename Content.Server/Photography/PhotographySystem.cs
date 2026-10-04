using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Photography;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Photography;

public sealed class PhotographySystem : EntitySystem
{


    [Dependency] private MetaDataSystem _metaDataSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;

    private static readonly ProtoId<TagPrototype> WriteIgnoreStampsTag = "WriteIgnoreStamps";
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";


    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CameraPhotoCapturedEvent>(OnPhotoCaptured);
        SubscribeLocalEvent<PhotographComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<CameraComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<PhotographComponent, InteractUsingEvent>(OnInteractUsing);
    }
    private void OnExamine(EntityUid uid, CameraComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        if (comp.CurrentPhotos == 0)
        {
            args.PushMarkup(Loc.GetString(("limited-charges-no-charges")));
            return;
        }
        args.PushMarkup(Loc.GetString("limited-charges-charges-remaining", ("charges", comp.CurrentPhotos)));
    }
    private void OnPhotoCaptured(CameraPhotoCapturedEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Handled)
            return;
        ev.Handled = true;

        var player = args.SenderSession.AttachedEntity;
        if (player == null)
            return;

        var cameraUid = GetEntity(ev.CameraNetUid);

        if (!TryComp<CameraComponent>(cameraUid, out var camera))
            return;

        var currentTime = _timing.CurTime;

        if (currentTime < camera.NextPhotoTime)
        {
            return;
        }

        if (camera.CurrentPhotos <= 0)
        {
            return;
        }

        const int maxSizeBytes = 100 * 1024;
        if (ev.PhotoBytes.Length > maxSizeBytes)
        {
            Logger.Warning($"Player {args.SenderSession.Name} sent too many bytes: {ev.PhotoBytes.Length}");
            _adminLogger.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(player.Value):player} attempted to capture a photo that was too large ({ev.PhotoBytes.Length} bytes)");
            return;
        }

        camera.CurrentPhotos--;
        camera.NextPhotoTime = currentTime + camera.Cooldown;
        Dirty(cameraUid, camera);

        var coords = Transform(player.Value).Coordinates;
        var photoEntity = Spawn("PalaroidPaper", coords);

        _metaDataSystem.SetEntityName(photoEntity, _loc.GetString("photography-picture-name"));
        _metaDataSystem.SetEntityDescription(photoEntity, _loc.GetString("photography-picture-description"));

        if (TryComp<PhotographComponent>(photoEntity, out var photoComp))
        {
            photoComp.RawData = ev.PhotoBytes;
            Dirty(photoEntity, photoComp);
        }
        _hands.PickupOrDrop(player.Value, photoEntity, dropNear:true);
        _audio.PlayPvs(camera.PrintSound, cameraUid);
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(player.Value):player} created a new photograph {ToPrettyString(photoEntity):photo} using {ToPrettyString(cameraUid):camera}");
    }

    private void OnUIOpened(Entity<PhotographComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not PolaroidUiKey.Key)
            return;

        UpdateUserInterface(ent.Owner, ent.Comp);
    }

    private void UpdateUserInterface(EntityUid uid, PhotographComponent photo)
    {
        if (!TryComp<PaperComponent>(uid, out var paper))
            return;

        var state = new PolaroidBoundUserInterfaceState(
            photo.RawData,
            paper.Content,
            paper.Mode,
            paper.StampedBy
        );

        _uiSystem.SetUiState(uid, PolaroidUiKey.Key, state);
    }
    private void OnInteractUsing(Entity<PhotographComponent> entity, ref InteractUsingEvent args)
    {
        if (!TryComp<PaperComponent>(entity.Owner, out var paper))
            return;
        // only allow editing if there are no stamps or when using a cyberpen
        var editable = paper.StampedBy.Count == 0 || _tagSystem.HasTag(args.Used, WriteIgnoreStampsTag);
        if (_tagSystem.HasTag(args.Used, WriteTag))
        {
            if (editable)
            {
                if (paper.EditingDisabled)
                {
                    var paperEditingDisabledMessage = Loc.GetString("paper-tamper-proof-modified-message");
                    _popupSystem.PopupEntity(paperEditingDisabledMessage, entity, args.User);

                    args.Handled = true;
                    return;
                }

                var ev = new PaperWriteAttemptEvent(entity.Owner);
                RaiseLocalEvent(args.User, ref ev);
                if (ev.Cancelled)
                {
                    if (ev.FailReason is not null)
                    {
                        var fileWriteMessage = Loc.GetString(ev.FailReason);
                        _popupSystem.PopupEntity(fileWriteMessage, entity.Owner, args.User);
                    }

                    args.Handled = true;
                    return;
                }

                var writeEvent = new PaperWriteEvent(args.User, entity);
                RaiseLocalEvent(args.Used, ref writeEvent);

                paper.Mode = PaperComponent.PaperAction.Write;
                _uiSystem.OpenUi(entity.Owner, PolaroidUiKey.Key, args.User);
                UpdateUserInterface(entity.Owner, entity.Comp);
            }
            args.Handled = true;
        }
    }
}

