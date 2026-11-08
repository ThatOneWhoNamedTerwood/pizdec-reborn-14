using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Jittering;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.PizReb.Cyberware;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.PizReb.Cyberware;

public sealed partial class CyberpsychosisSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private SharedJitteringSystem _jittering = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private NpcFactionSystem _faction = default!;

    private static readonly string[] LaughSounds =
    {
        "/Audio/Voice/Human/laugh_male_1.ogg",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberwareComponent, CyberwareChangedEvent>(OnCyberwareChanged);
        SubscribeLocalEvent<CyberpsychosisComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<CyberpsychosisComponent> ent, ref ComponentStartup args)
    {
        ScheduleFirst(ent.Comp);
    }

    private void ScheduleFirst(CyberpsychosisComponent psycho)
    {
        if (psycho.Stage != CyberpsychosisStage.None)
            return;
        var delay = _random.NextDouble() * (psycho.TremorDelayMax - psycho.TremorDelayMin).TotalSeconds +
                    psycho.TremorDelayMin.TotalSeconds;
        psycho.NextStage = _timing.CurTime + TimeSpan.FromSeconds(delay);
    }

    private void OnCyberwareChanged(EntityUid uid, CyberwareComponent cyber, ref CyberwareChangedEvent args)
    {
        var overloaded = cyber.BrainLoad > cyber.Capacity;
        if (!TryComp<CyberpsychosisComponent>(uid, out var psycho))
        {
            if (!overloaded)
                return;
            psycho = EnsureComp<CyberpsychosisComponent>(uid);
            ScheduleFirst(psycho);
            return;
        }
        if (!overloaded && psycho.Stage == CyberpsychosisStage.None && !psycho.Permanent)
        {
            RemComp<CyberpsychosisComponent>(uid);
            RemComp<JitteringComponent>(uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CyberpsychosisComponent>();
        while (query.MoveNext(out var uid, out var psycho))
        {
            if (now < psycho.NextStage)
                continue;
            Advance(uid, psycho);
        }
    }

    private void Advance(EntityUid uid, CyberpsychosisComponent psycho)
    {
        switch (psycho.Stage)
        {
            case CyberpsychosisStage.None:
                psycho.Stage = CyberpsychosisStage.Tremor;
                SetJitter(uid, 24f, 8f);
                SendMessage(uid, "cyberware-psychosis-tremor");
                psycho.NextStage = _timing.CurTime + psycho.EyesDelay;
                break;
            case CyberpsychosisStage.Tremor:
                psycho.Stage = CyberpsychosisStage.Eyes;
                SetJitter(uid, 45f, 10f);
                SetEyeGlow(uid);
                SendMessage(uid, "cyberware-psychosis-eyes");
                psycho.NextStage = _timing.CurTime + psycho.AggressionDelay;
                break;
            case CyberpsychosisStage.Eyes:
                psycho.Stage = CyberpsychosisStage.Aggression;
                _combat.SetInCombatMode(uid, true);
                SendMessage(uid, "cyberware-psychosis-aggression");
                psycho.NextStage = _timing.CurTime + psycho.LaughDelay;
                break;
            case CyberpsychosisStage.Aggression:
                psycho.Stage = CyberpsychosisStage.Laugh;
                _audio.PlayPvs(LaughSounds[0], uid);
                SendMessage(uid, "cyberware-psychosis-laugh");
                psycho.NextStage = _timing.CurTime + psycho.AntagonistDelay;
                break;
            case CyberpsychosisStage.Laugh:
                psycho.Stage = CyberpsychosisStage.Antagonist;
                GiveAntagonist(uid);
                SendMessage(uid, "cyberware-psychosis-antagonist");
                psycho.NextStage = _timing.CurTime + psycho.MonsterDelay;
                break;
            case CyberpsychosisStage.Antagonist:
                psycho.Stage = CyberpsychosisStage.Monster;
                BecomeMonster(uid);
                break;
        }
        Dirty(uid, psycho);
    }

    private void SetJitter(EntityUid uid, float amplitude, float frequency)
    {
        _jittering.DoJitter(uid, TimeSpan.FromMinutes(30), true, amplitude, frequency, forceValueChange: true);
    }

    private void SetEyeGlow(EntityUid uid)
    {
        var light = EnsureComp<PointLightComponent>(uid);
        _light.SetColor(uid, Color.Red, light);
        _light.SetEnergy(uid, 1.5f, light);
        _light.SetRadius(uid, 1.5f, light);
        _light.SetEnabled(uid, true, light);
    }

    private void GiveAntagonist(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;
        _mind.TryAddObjective(mindId, mind, "CyberpsychoKillAllObjective");
        RaiseLocalEvent(uid, new CyberpsychoAntagonistEvent());
    }

    private void BecomeMonster(EntityUid uid)
    {
        _faction.ClearFactions(uid);
        _faction.AddFaction(uid, "Cyberpsycho");
        var htn = EnsureComp<HTNComponent>(uid);
        htn.RootTask = new HTNCompoundTask { Task = "SimpleHostileCompound" };
        _npc.WakeNPC(uid);
        if (_mind.TryGetMind(uid, out var mindId, out _))
            _ghost.OnGhostAttempt(mindId, false, forced: true);
        RemComp<CyberpsychosisComponent>(uid);
    }

    private void SendMessage(EntityUid uid, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), uid, uid, PopupType.LargeCaution);
    }
}

public sealed class CyberpsychoAntagonistEvent : EntityEventArgs;



