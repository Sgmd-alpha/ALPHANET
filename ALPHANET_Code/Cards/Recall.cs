using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Recall : ModCardTemplate
{
    public override bool GainsBlock => true;

    public Recall() : base(2, CardType.Skill, CardRarity.Common, TargetType.Self) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("LossPerUse", 2),

        ModCardVars.ComputedBlock(
            "ComputedBlock",
            ctx =>
            {
                decimal baseBlock = ctx.BaseValue;

                if (!ctx.IsInCombat || ctx.CombatState is null || ctx.Player is null || ctx.Card is null)
                {
                    return baseBlock;
                }

                decimal lossPerUse = ctx.Card.DynamicVars.GetIntOrDefault("LossPerUse", 2);

                // 统计本回合中这张卡已经开始结算过的次数
                int playCountThisTurn = 0;
                if (CombatManager.Instance?.History != null)
                {
                    playCountThisTurn = CombatManager.Instance.History.CardPlaysStarted
                        .Count(e => e.CardPlay?.Card == ctx.Card && e.HappenedThisTurn(ctx.CombatState));
                }

                return Math.Max(0m, baseBlock - playCountThisTurn * lossPerUse);
            },
            baseValue: 10m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<TokenMemories>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        int finalBlock = (int)DynamicVars.EvaluateValueOrDefault("ComputedBlock");
        if (finalBlock > 0)
        {
            await CreatureCmd.GainBlock(player.Creature, finalBlock, ValueProp.Move, cardPlay);
        }

        CardModel generatedCard = CombatState!.CreateCard<TokenMemories>(player);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(generatedCard);
        }

        await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, Owner);

        // 移除了手动升级 TimesPlayed 的逻辑，历史记录会自动处理且下回合自动清零！
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ComputedBlock"].UpgradeValueBy(2m); // 10 -> 12
    }
}