using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class ChainReaction : ModCardTemplate
{
    // 核心解法：通过这个专属标记，完美解决卡牌溯源与时序问题
    public bool IsTriggeredByChainReaction { get; set; } = false;

    public ChainReaction() : base(3, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 显式声明基础 13 点伤害
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(13m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        var player = Owner!;

        // ==========================================
        // 阶段 1：先结算被同名卡牌打出时的抽牌逻辑
        // ==========================================
        if (IsTriggeredByChainReaction)
        {
            // 触发后立刻重置，防止后续由于某些复活/回响机制导致意外触发
            IsTriggeredByChainReaction = false; 
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }

        // ==========================================
        // 阶段 2：造成 2 次伤害
        // ==========================================
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(2) // 官方原生支持的多段攻击机制
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // ==========================================
        // 阶段 3：随机打出手牌中的另一张攻击牌
        // ==========================================
        CardPile handPile = PileType.Hand.GetPile(player);
        
        // 获取手牌中可被打出的其他攻击牌（排除掉自己 c != this，防止某些极端情况下的无限死循环）
        CardModel? cardToAutoPlay = handPile.Cards
            .Where(c => c.Type == CardType.Attack && c != this && !c.Keywords.Contains(CardKeyword.Unplayable))
            .ToList()
            .StableShuffle(player.RunState.Rng.Shuffle)
            .FirstOrDefault();

        // 完全照抄官方 Uproar 的安全兜底逻辑：如果没有正常的攻击牌，就把不能打出的也算进去
        if (cardToAutoPlay == null)
        {
            cardToAutoPlay = handPile.Cards
                .Where(c => c.Type == CardType.Attack && c != this)
                .ToList()
                .StableShuffle(player.RunState.Rng.Shuffle)
                .FirstOrDefault();
        }

        if (cardToAutoPlay != null)
        {
            // 【核心联动机制】：如果我们随机抽到的下一张牌恰好也是《连锁》，则提前为它打上标记！
            if (cardToAutoPlay is ChainReaction nextChainCard)
            {
                nextChainCard.IsTriggeredByChainReaction = true;
            }
            await CardCmd.AutoPlay(choiceContext, cardToAutoPlay, null);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级时伤害 +3（13 -> 16）
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}