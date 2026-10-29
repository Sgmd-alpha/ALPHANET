using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class GraceOfMiracles : ModCardTemplate
{
    private const string FirstBlockKey = "FirstBlock";
    private const string SubBlockKey = "SubBlock";

    // 声明为多人游戏专属卡牌
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public GraceOfMiracles() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.AllAllies, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1),                   // 打出时抽 1 张牌
        new DynamicVar(FirstBlockKey, 6m), // 首次触发获得 6 格挡
        new DynamicVar(SubBlockKey, 2m)    // 之后每次获得 2 (升级后 3) 格挡
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(ALPHANETKeywords.Miracle)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;

        // 1. 打出时抽 1 张牌
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);

        // 2. 赋予/叠加《奇迹助力》能力
        decimal firstBlock = DynamicVars[FirstBlockKey].BaseValue; // 6
        decimal subBlock = DynamicVars[SubBlockKey].BaseValue;     // 2 (3)

        var power = creature.GetPower<GraceOfMiraclesPower>();
        if (power == null)
        {
            await PowerCmd.Apply<GraceOfMiraclesPower>(choiceContext, creature, firstBlock, creature, this);
            power = creature.GetPower<GraceOfMiraclesPower>();
            if (power != null)
            {
                power.SubsequentBlock = subBlock;
                power.UpdateSubBlockVar();
            }
        }
        else
        {
            await PowerCmd.ModifyAmount(choiceContext, power, firstBlock, creature, this);
            power.SubsequentBlock += subBlock;
            power.UpdateSubBlockVar();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[SubBlockKey].UpgradeValueBy(1m); // 2 -> 3
    }
}