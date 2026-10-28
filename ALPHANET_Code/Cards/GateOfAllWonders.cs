using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class GateOfAllWonders : ModCardTemplate
{
    public GateOfAllWonders() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 显式声明该卡牌可获得格挡
    public override bool GainsBlock => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLoss", 6m),
        new BlockVar(5m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<AlphaStrike>(),
        HoverTipFactory.FromCard<AlphaDefend>()
    ];

    // 寻找基础打击
    private static bool IsStarterStrike(CardModel? card)
    {
        if (card == null) return false;
        
        // 基础稀有度，且带有 Strike 标签
        return card.Rarity == CardRarity.Basic && 
            card.Tags.Contains(CardTag.Strike);
    }

    // 寻找基础防御
    private static bool IsStarterDefend(CardModel? card)
    {
        if (card == null) return false;
        
        // 基础稀有度，且带有 Defend 标签
        return card.Rarity == CardRarity.Basic && 
            card.Tags.Contains(CardTag.Defend);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;
        var handPile = PileType.Hand.GetPile(player);

        // ==========================================
        // 1. 消耗手牌中的所有初始“打击”，随后统计消耗牌堆中的初始“打击”
        // ==========================================
        List<CardModel> strikesInHand = handPile.Cards.Where(IsStarterStrike).ToList();
        foreach (CardModel strike in strikesInHand)
        {
            await CardCmd.Exhaust(choiceContext, strike);
        }

        // ==========================================
        // 2. 消耗手牌中的所有初始“防御”，随后统计消耗牌堆中的初始“防御”
        // ==========================================
        List<CardModel> defendsInHand = handPile.Cards.Where(IsStarterDefend).ToList();
        foreach (CardModel defend in defendsInHand)
        {
            await CardCmd.Exhaust(choiceContext, defend);
        }
        
        var exhaustPile = PileType.Exhaust.GetPile(player);

        int totalExhaustedStrikes = exhaustPile.Cards.Count(IsStarterStrike);
        decimal hpLossPerStrike = DynamicVars["HpLoss"].BaseValue;

        int totalExhaustedDefends = exhaustPile.Cards.Count(IsStarterDefend);
        for (int i = 0; i < totalExhaustedDefends; i++)
        {
            await CreatureCmd.GainBlock(creature, DynamicVars.Block, cardPlay);
        }

        for (int i = 0; i < totalExhaustedStrikes; i++)
        {
            var hittableEnemies = CombatState?.HittableEnemies;
            if (hittableEnemies == null || hittableEnemies.Count == 0)
                break;

            Creature? target = player.RunState.Rng.CombatTargets.NextItem(hittableEnemies);
            if (target != null)
            {
                await CreatureCmd.Damage(
                    choiceContext,
                    target,
                    hpLossPerStrike,
                    ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
                    this,
                    cardPlay
                );
            }
        }

    }

    protected override void OnUpgrade()
    {
        DynamicVars["HpLoss"].UpgradeValueBy(3m); // 6 -> 9
        DynamicVars.Block.UpgradeValueBy(3m);     // 5 -> 8
    }
}