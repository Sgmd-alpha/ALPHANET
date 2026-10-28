using ALPHANET.Characters;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
[HarmonyPatch]
public sealed class RiteOfRemembrance : ModCardTemplate
{
    // 防止被消耗触发 AutoPlay 后，卡牌本身因具有 Exhaust 词条再次进入消耗堆时引发死循环
    private bool _isTriggeringExhaustAutoPlay = false;

    public RiteOfRemembrance() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<TokenStasis>(upgrade: IsUpgraded),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 所有敌人受到 10 (14) 点伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute(choiceContext);

        // 2. 将一张“坟墓”（升级后为“坟墓+”）加入手牌
        CardModel grave = CombatState!.CreateCard<TokenStasis>(player);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(grave);
        }

        await CardPileCmd.AddGeneratedCardToCombat(grave, PileType.Hand, player);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m); // 10 -> 14
    }

    // =========================================================================
    // 核心机制：当本牌“未经过打出（oldPile != PileType.Play）”而直接进入消耗堆时，自动打出！
    // =========================================================================
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
    [HarmonyPostfix]
    public static void OnAfterCardChangedPilesPostfix(
        IRunState runState,
        ICombatState? combatState,
        CardModel? card,
        PileType oldPile,
        AbstractModel? clonedBy,
        ref Task __result)
    {
        if (combatState == null || card is not RiteOfRemembrance riteCard)
            return;

        // 必须是进入了消耗堆 (PileType.Exhaust)
        if (riteCard.Pile == null || riteCard.Pile.Type != PileType.Exhaust)
            return;

        // 如果是从 Play 牌堆进来的（说明是手动打出、或被《破灭》等 AutoPlay 打出后自然消耗的），绝不补打！
        if (oldPile == PileType.Play || riteCard._isTriggeringExhaustAutoPlay)
            return;

        Task originalTask = __result;
        __result = AutoPlayOnExhaustAsync(originalTask, riteCard);
    }

    private static async Task AutoPlayOnExhaustAsync(Task originalTask, RiteOfRemembrance riteCard)
    {
        if (originalTask != null)
        {
            await originalTask;
        }

        if (riteCard.Owner?.Creature == null || riteCard.Owner.Creature.IsDead)
            return;

        riteCard._isTriggeringExhaustAutoPlay = true;
        try
        {
            var choiceContext = new ThrowingPlayerChoiceContext();
            // 使用 AutoPlay 自动打出（IsAutoPlay 为 true，完美兼容《佩尔之眼》额外回合）
            await CardCmd.AutoPlay(choiceContext, riteCard, null);
        }
        finally
        {
            riteCard._isTriggeringExhaustAutoPlay = false;
        }
    }
}