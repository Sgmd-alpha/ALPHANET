using ALPHANET.Characters;
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
public sealed class BMSDamageSubsidy : ModCardTemplate
{
    private const string HealPctKey = "HealPct";
    private const string BlockMultKey = "BlockMult";

    public BMSDamageSubsidy() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(HealPctKey, 25m),  // 恢复累计值 25% (升级后 50%) 的生命
        new DynamicVar(BlockMultKey, 1m)  // 获得等同于累计值 1 倍的格挡
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;
        decimal cardHealPct = DynamicVars[HealPctKey].BaseValue;
        decimal blockMult = DynamicVars[BlockMultKey].BaseValue;

        // 1. 赋予/叠加 1 层能力（代表下回合获得的格挡倍率 +1）
        await PowerCmd.Apply<BMSDamageSubsidyPower>(choiceContext, creature, blockMult, creature, this);

        // 2. 更新恢复百分比（不可叠加，若先后打出未升级与升级版，取最高值 50%）
        var power = creature.GetPower<BMSDamageSubsidyPower>();
        if (power != null)
        {
            power.HealPercentage = Math.Max(power.HealPercentage, cardHealPct);
            power.SyncDynamicVars();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[HealPctKey].UpgradeValueBy(25m); // 25% -> 50%
    }
}