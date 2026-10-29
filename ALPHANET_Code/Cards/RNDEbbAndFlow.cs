using ALPHANET.Characters;
using ALPHANET.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class RNDEbbAndFlow : ModCardTemplate
{
    private const string BlockGainKey = "BlockGain";

    public RNDEbbAndFlow() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<RNDEbbAndFlowPower>(1m), // 每次打出叠加 1 层
        new DynamicVar(BlockGainKey, 3m)      // 基础 3 格挡
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;
        
        // 1. 赋予核心能力 (1 层)
        await PowerCmd.Apply<RNDEbbAndFlowPower>(choiceContext, creature, 1m, creature, this);

        // 2. 如果当前卡牌是升级版 (5 点格挡)，且玩家身上已经有了此能力，则将能力的格挡基准值刷新为最大值
        var power = creature.GetPower<RNDEbbAndFlowPower>();
        if (power != null && power.DynamicVars.TryGetValue(BlockGainKey, out var powerBlockVar))
        {
            decimal cardBlock = DynamicVars[BlockGainKey].BaseValue;
            powerBlockVar.BaseValue = Math.Max(powerBlockVar.BaseValue, cardBlock);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[BlockGainKey].UpgradeValueBy(2m); // 3 -> 5
    }
}