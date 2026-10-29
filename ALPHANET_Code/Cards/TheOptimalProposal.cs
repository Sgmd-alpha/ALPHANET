using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Powers;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class TheOptimalProposal : ModCardTemplate
{
    private const string AllyBlockKey = "AllyBlock";

    // 声明为多人游戏专属卡牌
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override bool GainsBlock => true;

    public TheOptimalProposal() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AllAllies, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust,
        ALPHANETKeywords.Miracle
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move),
        new DynamicVar(AllyBlockKey, 5m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
        HoverTipFactory.FromKeyword(ALPHANETKeywords.Miracle),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selfCreature = Owner!.Creature;

        // 1. 筛选出除自己以外所有存活的玩家队友（严格排除自己 c != selfCreature 且排除召唤物 c.IsPlayer）
        List<Creature> otherTeammates = CombatState!
            .GetTeammatesOf(selfCreature)
            .Where(c => c != null && c.IsAlive && c.IsPlayer && c != selfCreature)
            .Distinct()
            .ToList();

        int teammateCount = otherTeammates.Count;

        // 2. 获得格挡：基础 10 (16) 点 + 每名队友额外 5 (8) 点
        //    先获得基础格挡，再按队友人数获得额外格挡（或者合并后一次性获得）
        decimal baseBlock = DynamicVars.Block.BaseValue;
        decimal extraPerAlly = DynamicVars[AllyBlockKey].BaseValue;
        decimal totalBaseBlock = baseBlock + (teammateCount * extraPerAlly);

        await CreatureCmd.GainBlock(selfCreature, totalBaseBlock, ValueProp.Move, cardPlay);

        // 3. 为自己挂上承受转移伤害的标记能力，并为所有队友挂上【提案庇护（免受伤害+转移未格挡伤害+头顶显示攻击意图）】
        if (teammateCount > 0)
        {
            await PowerCmd.Apply<OptimalProposalSelfPower>(choiceContext, selfCreature, 1m, selfCreature, this);

            foreach (Creature ally in otherTeammates)
            {
                await PowerCmd.Apply<OptimalCoveredPower>(choiceContext, ally, 1m, selfCreature, this);
                var covered = ally.GetPower<OptimalCoveredPower>();
                if (covered != null)
                {
                    covered.Protector = selfCreature;
                    covered.RefreshOverheadIntent();
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(6m);          // 10 -> 16
        DynamicVars[AllyBlockKey].UpgradeValueBy(3m);  // 5 -> 8
    }
}