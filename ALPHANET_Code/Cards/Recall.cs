using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
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
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png",
        PortraitMaterialPath: $"{Entry.ResPath}/images/Portrait/{GetType().Name}PortraitMat.tres"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            CardModel previewCard = ModelDb.Card<TokenMemories>();
            if (IsUpgraded)
            {
                previewCard = previewCard.ToMutable();
                CardCmd.Upgrade(previewCard);
            }
            return
            [
                HoverTipFactory.FromCard(previewCard)
            ];
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);
        await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, cardPlay);

        CardModel generatedCard = CombatState!.CreateCard<TokenMemories>(player);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(generatedCard);
        }

        await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, player);
    }

    // 参考官方 HowlFromBeyond：回合开始（抽牌后的自动结算阶段）时，若此牌位于消耗堆则移回弃牌堆
    public override async Task AfterAutoPostPlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        CardPile? pile = Pile;
        if (pile != null && pile.Type == PileType.Exhaust && player == Owner)
        {
            await CardPileCmd.Add(this, PileType.Discard);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m); // 10 -> 12
    }
}