using ALPHANET.Characters;
using ALPHANET.Powers;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class TacticalReversal : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Exit;

    public TacticalReversal() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new BlockVar(5m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            CardModel previewCard = ModelDb.Card<TokenParadox>();
            if (IsUpgraded)
            {
                previewCard = previewCard.ToMutable();
                CardCmd.Upgrade(previewCard);
            }
            return 
            [
                HoverTipFactory.FromPower<CriticalState>(),
                HoverTipFactory.FromCard(previewCard)
            ];
        }
    }

    // 处于临界状态时发金光提示
    protected override bool ShouldGlowGoldInternal => Owner?.Creature?.HasPower<CriticalState>() ?? false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;

        // 1. 退出临界状态
        var criticalState = creature.GetPower<CriticalState>();
        if (criticalState != null)
        {
            await PowerCmd.Remove(criticalState);
        }

        // 2. 获得 5 (升级后 6) 点格挡
        await CreatureCmd.GainBlock(creature, DynamicVars.Block, cardPlay);

        // 3. 下回合开始时，将一张“理之矛，心之盾(+)”加入手牌
        if (IsUpgraded)
        {
            await PowerCmd.Apply<TacticalReversalUpgradedPower>(choiceContext, creature, 1m, creature, this);
        }
        else
        {
            await PowerCmd.Apply<TacticalReversalPower>(choiceContext, creature, 1m, creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m); // 5 -> 6
    }
}

