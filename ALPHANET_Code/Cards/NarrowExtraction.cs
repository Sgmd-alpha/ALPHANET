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
public sealed class NarrowExtraction : ModCardTemplate
{
    public NarrowExtraction() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new CardsVar(1),
        new DynamicVar("Block", 3m) 
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        await CreatureCmd.TriggerAnim(player.Creature, "PowerUp", player.Character.PowerUpAnimDelay);

        // 1. 挂载能力（将抽牌数作为主层数 Amount 传入）
        await PowerCmd.Apply<NarrowExtractionPower>(choiceContext, player.Creature, DynamicVars.Cards.BaseValue, player.Creature, this);

        // 2. 获取玩家身上的能力实例，更新它的 DynamicVars 供 UI 渲染和逻辑结算
        var power = player.Creature.GetPower<NarrowExtractionPower>();
        if (power != null)
        {
            // 累加格挡值（打出多张时，3 -> 6 -> 9 正常叠加）
            power.DynamicVars["BlockAmount"].BaseValue += DynamicVars["Block"].BaseValue;
            
            // 将抽牌变量同步为当前能力的总层数
            power.DynamicVars.Cards.BaseValue = power.Amount;
        }
    }

    protected override void OnUpgrade()
    {
        // 抽牌数 +1
        DynamicVars.Cards.UpgradeValueBy(1m);
        // 格挡
    }
}