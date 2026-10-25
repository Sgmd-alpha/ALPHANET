using ALPHANET.Characters;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class PowerAmplification : ModCardTemplate
{
    public PowerAmplification() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new BlockVar(5m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;

        // 1. 获得 5 (升级后 8) 点格挡
        await CreatureCmd.GainBlock(creature, DynamicVars.Block, cardPlay);

        // 2. 筛选手牌中耗能不超过 1 的能力牌
        var handPile = PileType.Hand.GetPile(player);
        static bool IsEligiblePower(CardModel c) =>
            c != null &&
            c.Type == CardType.Power &&
            c.EnergyCost != null &&
            Math.Max(0, c.EnergyCost.GetWithModifiers(CostModifiers.All)) <= 1;

        if (!handPile.Cards.Any(IsEligiblePower))
            return;

        // 3. 选择手牌中 1 张符合条件的能力牌
        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        CardModel? selectedPower = (await CardSelectCmd.FromHand(
            context: choiceContext,
            player: player,
            prefs: prefs,
            filter: IsEligiblePower,
            source: this
        )).FirstOrDefault();

        // 4. 将其自动打出 2 次（第二次打出通过克隆实例或额外结算触发，确保能力效果叠加两次）
        if (selectedPower != null)
        {
            await CardCmd.AutoPlay(choiceContext, selectedPower, null);

            CardModel secondCopy = selectedPower.CreateClone();
            await CardCmd.AutoPlay(choiceContext, secondCopy, null);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
        DynamicVars.Block.UpgradeValueBy(3m); // 5 -> 8
    }
}

