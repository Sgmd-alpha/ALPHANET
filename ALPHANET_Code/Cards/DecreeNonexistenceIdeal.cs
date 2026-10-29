using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Powers;
using ALPHANET.States;
using ALPHANET.Tags;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
[HarmonyPatch]
public sealed class DecreeNonexistenceIdeal : ModCardTemplate
{
    public override bool CanBeGeneratedInCombat => false;
    public DecreeNonexistenceIdeal() : base(99, CardType.Skill, CardRarity.Ancient, TargetType.AnyEnemy, false)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
        ALPHANETKeywords.Miracle
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
        HoverTipFactory.FromKeyword(ALPHANETKeywords.Miracle),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var target = cardPlay.Target;
        if (target == null || !target.IsAlive)
            return;
        await CreatureCmd.Kill(target, force: true);
        for (int i = 0; i < 3; i++)
        {
            if (!target.IsAlive) break;

            await CreatureCmd.Damage(
                choiceContext,
                target,
                999999m,
                ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
                Owner?.Creature,
                this,
                cardPlay
            );
        }
    }

    protected override void OnUpgrade()
    {
        // 升级获得【保留】词条
        AddKeyword(CardKeyword.Retain);
    }

    // =========================================================================
    // 彩蛋核心检测：集齐 B、S、V、H、T、R、共同的黎明 七大能力
    // =========================================================================
    public static bool HasAllSevenEasterEggPowers(Creature? creature)
    {
        if (creature?.Powers == null) return false;

        var activePowerNames = creature.Powers
            .Where(p => p != null && p.Amount > 0m)
            .Select(p => p.GetType().Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. B: BMS-损伤补贴 (BMSDamageSubsidyPower)
        bool hasB = creature.HasPower<BMSDamageSubsidyPower>()
                 || activePowerNames.Any(n => n.StartsWith("BMS", StringComparison.OrdinalIgnoreCase));

        // 2. S: SPY-暗度陈仓 (SPYAmbushPower)
        bool hasS = activePowerNames.Any(n => n.Equals("SPYAmbushPower", StringComparison.OrdinalIgnoreCase)
                                           || n.StartsWith("SPY", StringComparison.OrdinalIgnoreCase));

        // 3. V: VSL-以心拒之 (VSLTalkToTheMindPower)
        bool hasV = activePowerNames.Any(n => n.Equals("VSLTalkToTheMindPower", StringComparison.OrdinalIgnoreCase)
                                           || n.StartsWith("VSL", StringComparison.OrdinalIgnoreCase));

        // 4. H: HRE-混沌形态 (HREChaosFormPower)
        bool hasH = activePowerNames.Any(n => n.Equals("HREChaosFormPower", StringComparison.OrdinalIgnoreCase)
                                           || n.StartsWith("HRE", StringComparison.OrdinalIgnoreCase));

        // 5. T: TCS-群魔乱舞 (TCSPandemoniumPower)
        bool hasT = creature.HasPower<TCSPandemoniumPower>()
                 || activePowerNames.Any(n => n.StartsWith("TCS", StringComparison.OrdinalIgnoreCase));

        // 6. R: RND-此消彼长 (RNDEbbAndFlowPower)
        bool hasR = creature.HasPower<RNDEbbAndFlowPower>()
                 || activePowerNames.Any(n => n.StartsWith("RND", StringComparison.OrdinalIgnoreCase));

        // 7. 共同的黎明 (DawnOfCommunionPower)
        bool hasDawn = activePowerNames.Any(n => n.Equals("DawnOfCommunionPower", StringComparison.OrdinalIgnoreCase)
                                              || n.Contains("DawnOfCommunion", StringComparison.OrdinalIgnoreCase)
                                              || n.Contains("CommonDawn", StringComparison.OrdinalIgnoreCase));

        return hasB && hasS && hasV && hasH && hasT && hasR && hasDawn;
    }

    /// <summary>
    /// 在回合开始（因《共同的黎明》进入临界状态时）或集齐能力处于临界状态时调用：
    /// 将该玩家所有牌堆中的《未竟律令 (UnfinishedDecree)》全部变化为《律令-不曾存在（理想状态）+》！
    /// </summary>
    public static async Task TryTriggerEasterEggTransformation(Creature? creature)
    {
        if (creature == null || !creature.IsAlive || creature.Player == null || creature.CombatState == null)
            return;

        if (!creature.HasPower<CriticalState>() || !HasAllSevenEasterEggPowers(creature))
            return;

        Player player = creature.Player;
        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState == null) return;

        // 遍历所有牌堆（手牌、抽牌堆、弃牌堆、消耗堆）寻找《未竟律令》
        var allPiles = new[]
        {
            playerCombatState.Hand,
            playerCombatState.DrawPile,
            playerCombatState.DiscardPile,
            playerCombatState.ExhaustPile
        };

        var decreesToTransform = new List<CardModel>();
        foreach (var pile in allPiles)
        {
            if (pile?.Cards == null) continue;
            foreach (var card in pile.Cards.OfType<UnfinishedDecree>())
            {
                decreesToTransform.Add(card);
            }
        }

        if (decreesToTransform.Count == 0)
            return;

        var choiceContext = new ThrowingPlayerChoiceContext();

        foreach (var oldCard in decreesToTransform)
        {
            CardModel idealCard = creature.CombatState.CreateCard<DecreeNonexistenceIdeal>(player);
            CardCmd.Upgrade(idealCard); // 变化为“律令-不曾存在（理想状态）+”

            // 使用官方标准的 CardCmd.Transform 语法完成牌堆内原位替换
            await CardCmd.Transform(oldCard, idealCard);
        }
    }
}