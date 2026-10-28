using System.Runtime.CompilerServices;
using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Tags;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
[HarmonyPatch]
public sealed class UnfinishedDecree : ModCardTemplate
{
    private const string SelfHpLossKey = "SelfHpLoss";
    private const string SelfMinHpKey = "SelfMinHp";
    private const string EnemyMinHpKey = "EnemyMinHp";

    // 记录本次打出《未竟律令》在奇迹扣血前的状态，用于判断是否在奇迹阶段触发了名刀/免死
    private static readonly ConditionalWeakTable<CardPlay, PreMiracleState> _preMiracleStates = new();

    // 当前正在受《未竟律令》50血保底保护的敌人目标及其最低生命线
    private static Creature? _protectedEnemyTarget = null;
    private static int _protectedEnemyFloorHp = 50;

    private class PreMiracleState
    {
        public int HpBeforeMiracle;
        public bool TriggeredDeathPreventionInMiracle;
    }

    public UnfinishedDecree() : base(7, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 已移除 CardKeyword.Retain（保留），让卡面更精简
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
        ALPHANETKeywords.Miracle
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(150m, ValueProp.Move),
        new DynamicVar(SelfHpLossKey, 50m),
        new DynamicVar(SelfMinHpKey, 5m),
        new DynamicVar(EnemyMinHpKey, 50m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var player = Owner!;
        var creature = player.Creature;
        var target = cardPlay.Target;

        int selfHpLoss = DynamicVars[SelfHpLossKey].IntValue; // 50
        int selfMinHp = DynamicVars[SelfMinHpKey].IntValue;   // 5
        int enemyMinHp = DynamicVars[EnemyMinHpKey].IntValue; // 50

        // =================================================================
        // 1. 结算玩家自身失去 50 点生命（含 5 血保底逻辑与名刀免二次致死保护）
        // =================================================================
        int currentHp = creature.CurrentHp;
        bool survivedLethalMiracle = false;

        if (_preMiracleStates.TryGetValue(cardPlay, out var state))
        {
            survivedLethalMiracle = state.TriggeredDeathPreventionInMiracle;
            _preMiracleStates.Remove(cardPlay);
        }

        if (!survivedLethalMiracle && currentHp > 0)
        {
            decimal actualSelfDamage;

            if (currentHp >= selfMinHp)
            {
                // 若此前（奇迹结算后）生命不低于 5，则至少保留 5 点生命
                actualSelfDamage = Math.Min(selfHpLoss, currentHp - selfMinHp);
            }
            else
            {
                // 若此前生命已低于 5（且未在奇迹阶段触发名刀），全额承受 50 点生命流失
                actualSelfDamage = selfHpLoss;
            }

            if (actualSelfDamage > 0)
            {
                await CreatureCmd.Damage(
                    choiceContext,
                    creature,
                    actualSelfDamage,
                    ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
                    this,
                    cardPlay
                );
            }
        }

        if (creature.IsDead)
            return;

        // =================================================================
        // 2. 对一名敌人造成 150 点伤害，敌人至少保留 50 点生命（不秒人）
        // =================================================================
        // 如果敌人当前生命高于 50，则最低扣到 50；如果本来就不足 50，则最低保持当前生命（只破盾不掉血）
        _protectedEnemyTarget = target;
        _protectedEnemyFloorHp = Math.Min(target.CurrentHp, enemyMinHp);

        try
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }
        finally
        {
            _protectedEnemyTarget = null;
        }

        // =================================================================
        // 3. 结束你的回合
        // =================================================================
        PlayerCmd.EndTurn(player, canBackOut: false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-2); // 7 -> 5
    }

    // =========================================================================
    // Harmony 拦截 1：记录奇迹扣血前状态
    // =========================================================================
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
    [HarmonyPrefix]
    public static void RecordPreMiracleHp(CardPlay cardPlay)
    {
        if (cardPlay?.Card is UnfinishedDecree && cardPlay.Card.Owner?.Creature != null)
        {
            _preMiracleStates.AddOrUpdate(cardPlay, new PreMiracleState
            {
                HpBeforeMiracle = cardPlay.Card.Owner.Creature.CurrentHp,
                TriggeredDeathPreventionInMiracle = false
            });
        }
    }

    // =========================================================================
    // Harmony 拦截 2：阻止受保护的敌人在这次攻击中死亡，同时检测玩家的免死触发
    // =========================================================================
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldDie))]
    [HarmonyPostfix]
    public static void HandleShouldDie(Creature creature, ref bool __result)
    {
        if (creature == null) return;

        // A. 如果是正在被《未竟律令》攻击的敌人，强制禁止死亡！
        if (creature == _protectedEnemyTarget)
        {
            __result = false;
            return;
        }

        // B. 如果玩家在奇迹扣血阶段被名刀阻止了死亡 (__result == false)，记录免死已触发
        if (creature.Player != null && !__result)
        {
            foreach (var kvp in _preMiracleStates)
            {
                if (kvp.Key.Card?.Owner?.Creature == creature)
                {
                    kvp.Value.TriggeredDeathPreventionInMiracle = true;
                }
            }
        }
    }

    // =========================================================================
    // Harmony 拦截 3：拦截底层 Creature.LoseHpInternal (所有扣减生命值行为的最终入口)
    // 无论是普通攻击穿透格挡后的扣血，还是奇迹自损扣血，都会经过这里！
    // =========================================================================
    [HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
    [HarmonyPrefix]
    public static void CapHpLossOnCreature(Creature __instance, ref decimal amount)
    {
        // 1. 敌人锁血保护：当受保护的敌人即将失去生命时，严格截断其失去的生命值，使其血量绝不低于 _protectedEnemyFloorHp (50)！
        if (__instance == _protectedEnemyTarget && amount > 0m)
        {
            decimal maxAllowedLoss = Math.Max(0m, __instance.CurrentHp - _protectedEnemyFloorHp);
            if (amount > maxAllowedLoss)
            {
                amount = maxAllowedLoss;
            }
            return;
        }

        // 2. 玩家名刀兜底检测：如果玩家在 BeforeCardPlayed 奇迹扣血阶段承受了 >= 当前血量的致命伤害
        if (__instance?.Player != null && amount >= __instance.CurrentHp)
        {
            foreach (var kvp in _preMiracleStates)
            {
                if (kvp.Key.Card?.Owner?.Creature == __instance)
                {
                    kvp.Value.TriggeredDeathPreventionInMiracle = true;
                }
            }
        }
    }
}