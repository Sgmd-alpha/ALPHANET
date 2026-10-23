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
    // 记录这张牌当前已经累计叠加了多少点伤害与格挡，方便重置时精准扣回（完美兼容升级前后的混合叠加）
    private decimal _accumulatedDamageBonus;
    private decimal _accumulatedBlockBonus;

    public ComfortZone() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 声明默认具有格挡属性
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 基础伤害 3，受力量等加成
        new DamageVar(3m, ValueProp.Move),
        // 攻击次数 2 次
        new RepeatVar(2),
        // 基础格挡 2，受敏捷等加成
        new BlockVar(2m, ValueProp.Move),
        // 每次使用的伤害增量 (3 -> 升级后 4)
        ModCardVars.Int("DamageScale", 3),
        // 每次使用的格挡增量 (2 -> 升级后 3)
        ModCardVars.Int("BlockScale", 2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 1. 造成 2 次伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 2. 获得格挡
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 3. 每次使用这张牌后，使本卡的伤害和格挡增加
        decimal dmgIncrease = DynamicVars["DamageScale"].BaseValue;
        decimal blkIncrease = DynamicVars["BlockScale"].BaseValue;

        DynamicVars.Damage.BaseValue += dmgIncrease;
        DynamicVars.Block.BaseValue += blkIncrease;

        _accumulatedDamageBonus += dmgIncrease;
        _accumulatedBlockBonus += blkIncrease;

        // 4. 遍历自己的所有牌堆：如果牌库里还有【另一张】《习常惯行》，对它而言你刚打出了“其他攻击牌”，需要将另一张重置！
        ResetOtherComfortZonesInPiles(this);
    }

    // 当场上有任何卡牌打出时触发（处理本卡在手牌中监听到其他攻击牌的情况）
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 必须是卡牌拥有者自己打出的攻击牌，且不是这张牌本身
        if (cardPlay.Card.Owner == Owner && cardPlay.Card.Type == CardType.Attack && cardPlay.Card != this)
        {
            ResetGrowth();
        }
        return Task.CompletedTask;
    }

    // 精准重置本卡叠加过的数值（即使叠加中途被武装升级过，也能精准退回升级后的初始白值！）
    public void ResetGrowth()
    {
        if (_accumulatedDamageBonus != 0m)
        {
            DynamicVars.Damage.BaseValue -= _accumulatedDamageBonus;
            _accumulatedDamageBonus = 0m;
        }

        if (_accumulatedBlockBonus != 0m)
        {
            DynamicVars.Block.BaseValue -= _accumulatedBlockBonus;
            _accumulatedBlockBonus = 0m;
        }
    }

    // 克隆卡牌时，同步复制累计账本，确保被复制出来的牌也能正常重置回初始值
    protected override void AfterCloned()
    {
        base.AfterCloned();
        if (CanonicalInstance is ComfortZone)
        {
            // 注意：MutableClone 会通过字段浅拷贝自动把 _accumulatedDamageBonus 和 _accumulatedBlockBonus 复制过来，无需清零
        }
    }

    // 辅助方法：重置该玩家所有牌堆中的其他《习常惯行》
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
                if (card is ComfortZone cz && cz != playedAttackCard)
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
        // 每次成长的格挡幅度 +1 (2 -> 3)
        DynamicVars["BlockScale"].UpgradeValueBy(1m);
    }
}