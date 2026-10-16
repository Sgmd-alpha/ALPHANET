using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Decouple : ModCardTemplate
{
    public Decouple() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 悬浮提示
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        EnergyHoverTip,
        HoverTipFactory.FromCard<StatusChaos>()
    ];

    // 声明能量变量，初始值为 2
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [ 
        new EnergyVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        CardModel chaosCard = CombatState!.CreateCard<StatusChaos>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(chaosCard, PileType.Hand, Owner);
        await Cmd.Wait(0.5f);
    }

    protected override void OnUpgrade()
    {
        // 升级时基础能量加 1
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}