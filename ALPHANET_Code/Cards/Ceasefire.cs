using System.Collections.Generic;
using System.Threading.Tasks;
using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Ceasefire : ModCardTemplate, ICriticalIndicatorCard
{
    public Ceasefire() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 固定退出临界状态 -> 显示白色圆圈
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Exit;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    // 处于临界状态时边框发金光提示
    protected override bool ShouldGlowGoldInternal => Owner?.Creature?.HasPower<CriticalState>() ?? false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;

        // 1. 获得 7 (升级后 10) 点格挡
        await CreatureCmd.GainBlock(creature, DynamicVars.Block, cardPlay);

        // 2. 退出临界状态
        var criticalState = creature.GetPower<CriticalState>();
        if (criticalState != null)
        {
            await PowerCmd.Remove(criticalState);
        }

        // 3. 将这张牌放入抽牌堆（随机位置）
        // 注：如果下方 GetResultPileType 生效，引擎也会在结算末尾自动将其送入抽牌堆
        await CardPileCmd.Add(this, PileType.Draw, CardPilePosition.Random);
    }

    // ==========================================
    // 核心机制：重写打出后的目标牌堆为【抽牌堆】
    // 防止 OnPlay 结束后被引擎默认结算逻辑拉回弃牌堆！
    // ==========================================
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card == this && Pile?.Type == PileType.Discard)
        {
            await CardPileCmd.Add(this, PileType.Draw, CardPilePosition.Random);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m); // 7 -> 10
    }
}