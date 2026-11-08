using System.Reflection;
using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class ComfortZone : ModCardTemplate
{
    private int _growthCount;

    public ComfortZone() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(3m, ValueProp.Move),
        new RepeatVar(2),
        new BlockVar(2m, ValueProp.Move),
        ModCardVars.Int("DamageScale", 3),
        ModCardVars.Int("BlockScale", 2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        SyncGrowthToVars();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        await CreatureCmd.GainBlock(Owner!.Creature, DynamicVars.Block, cardPlay);

        // 在打出时尝试寻找并消耗手牌中符合条件且稀有度最高的一张攻击牌
        var targetCard = PileType.Hand.GetPile(Owner)?.Cards
            .Where(c => c.Type == CardType.Attack && c != this)
            .OrderByDescending(c => c.Rarity switch
            {
                CardRarity.Ancient => 5,
                CardRarity.Rare => 4,
                CardRarity.Uncommon => 3,
                CardRarity.Common => 2,
                CardRarity.Basic => 1,
                _ => 0
            })
            .FirstOrDefault();

        // 基础成长 1 次
        int growthToAdd = 1;

        if (targetCard != null)
        {
            // 成功消耗手牌中的该卡牌，额外成长 1 次
            await CardCmd.Exhaust(choiceContext, targetCard);
            growthToAdd++; 
        }

        // 一次性加上所有成长并同步
        _growthCount += growthToAdd;
        SyncGrowthToVars();

        // 重置牌堆里【其他】的《习常惯行》
        ResetOtherComfortZonesInPiles(this);
    }

    // 静态判断方法，供全局 Harmony 补丁调用
    public static bool IsBasicCard(CardModel card)
    {
        if (card.Rarity == CardRarity.Basic)
            return true;

        var cardType = card.GetType();
        if (cardType.GetCustomAttribute<RegisterCharacterStarterCardAttribute>() != null)
            return true;

        string typeName = cardType.Name;
        if (typeName is "AlphaStrike" or "Salvage" or "Strike")
            return true;

        return false;
    }

    public void SyncGrowthToVars()
    {
        decimal baseDmg = IsUpgraded ? 4m : 3m;
        decimal baseBlk = IsUpgraded ? 3m : 2m;
        decimal scaleDmg = IsUpgraded ? 4m : 3m;
        decimal scaleBlk = IsUpgraded ? 3m : 2m;

        DynamicVars.Damage.BaseValue = baseDmg + (_growthCount * scaleDmg);
        DynamicVars.Block.BaseValue = baseBlk + (_growthCount * scaleBlk);
    }

    public void ResetGrowth()
    {
        if (_growthCount > 0)
        {
            _growthCount = 0;
            SyncGrowthToVars();
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        SyncGrowthToVars();
    }

    public static void ResetOtherComfortZonesInPiles(CardModel playedAttackCard)
    {
        var player = playedAttackCard.Owner;
        if (player == null) return;

        PileType[] piles = [PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust];
        foreach (var pileType in piles)
        {
            var pile = pileType.GetPile(player);
            if (pile?.Cards == null) continue;

            foreach (var card in pile.Cards)
            {
                if (card is ComfortZone cz && !ReferenceEquals(cz, playedAttackCard))
                {
                    cz.ResetGrowth();
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 基础伤害 +1 (3 -> 4)
        DynamicVars.Damage.UpgradeValueBy(1m);
        // 基础格挡 +1 (2 -> 3)
        DynamicVars.Block.UpgradeValueBy(1m);
        // 每次成长的伤害幅度 +1 (3 -> 4)
        DynamicVars["DamageScale"].UpgradeValueBy(1m);
        DynamicVars["BlockScale"].UpgradeValueBy(1m);
        SyncGrowthToVars();
    }
}