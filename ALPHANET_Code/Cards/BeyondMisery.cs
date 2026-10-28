using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class BeyondMisery : ModCardTemplate
{
    public BeyondMisery() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<StatusChaos>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var player = Owner!;

        // 1. 在手牌中加入一张《混沌》（使用 CombatState.CreateCard 确保卡牌正确绑定当前战斗状态！）
        CardModel chaos = CombatState!.CreateCard<StatusChaos>(player);
        await CardPileCmd.AddGeneratedCardToCombat(chaos, PileType.Hand, player);

        // 2. 提取敌人身上的所有负面效果，并合并临时状态（官方《苦难》的逻辑）
        var debuffAmounts = cardPlay.Target.Powers
            .Where(p => p.TypeForCurrentAmount == PowerType.Debuff)
            .ToDictionary(p => (PowerModel)p.ClonePreservingMutability(), p => p.Amount);

        // 处理类似“临时力量下降”这种包裹型状态，将其合并至内在状态的数值中
        foreach (var item in debuffAmounts.ToList())
        {
            if (item.Key is ITemporaryPower temporaryPower)
            {
                var targetPair = debuffAmounts.FirstOrDefault(p => p.Key.Id == temporaryPower.InternallyAppliedPower.Id);
                if (targetPair.Key != null)
                {
                    debuffAmounts[targetPair.Key] += item.Value;
                }
            }
        }

        // 3. 将统计好的负面效果“原封不动”地再次施加给该敌人，实现完美翻倍
        foreach (var item in debuffAmounts)
        {
            if (item.Value != 0)
            {
                PowerModel? existingPower = PowerCmd.FindExistingInstanceForStacking(item.Key, cardPlay.Target, item.Key.Applier);
                if (existingPower != null)
                {
                    await PowerCmd.ModifyAmount(choiceContext, existingPower, item.Value, item.Key.Applier, this);
                }
                else
                {
                    PowerModel powerToApply = (PowerModel)item.Key.ClonePreservingMutability();
                    await PowerCmd.Apply(choiceContext, powerToApply, cardPlay.Target, item.Value, item.Key.Applier, this);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}