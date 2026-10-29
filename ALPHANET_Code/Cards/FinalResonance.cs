using ALPHANET.Characters;
using ALPHANET.Powers;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class FinalResonance : ModCardTemplate
{
    private const string NormalDmgKey = "NormalDmg";
    private const string CritDmgKey = "CritDmg";

    public FinalResonance() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(NormalDmgKey, 7m),
        new DynamicVar(CritDmgKey, 25m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;
        decimal normalDmg = DynamicVars[NormalDmgKey].BaseValue;
        decimal critDmg = DynamicVars[CritDmgKey].BaseValue;

        var power = creature.GetPower<FinalResonancePower>();
        if (power == null)
        {
            // 首次打出时赋予能力并立刻设置临界伤害值
            await PowerCmd.Apply<FinalResonancePower>(choiceContext, creature, normalDmg, creature, this);
            power = creature.GetPower<FinalResonancePower>();
            if (power != null)
            {
                power.CritAmount = critDmg;
                power.UpdateCritVar();
            }
        }
        else
        {
            // 多次打出时叠加层数并累加临界伤害值
            await PowerCmd.ModifyAmount(choiceContext, power, normalDmg, creature, this);
            power.CritAmount += critDmg;
            power.UpdateCritVar();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[NormalDmgKey].UpgradeValueBy(3m); // 7 -> 10
        DynamicVars[CritDmgKey].UpgradeValueBy(9m);   // 25 -> 34
    }
}