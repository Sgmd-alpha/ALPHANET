using ALPHANET.Cards;
using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Relics;

[RegisterRelic(typeof(ALPHANETRelicPool))]
public sealed class MiracleShard : MiracleResidue
{
    // 与官方《黑血 (BlackBlood)》保持一致，设为 Starter 稀有度
    public override RelicRarity Rarity => RelicRarity.Starter;

    // 重写三大强化数值：
    protected override bool UpgradeFutureCard => true;          // 开局给“未来+”
    protected override decimal PostCombatHealThreshold => 20m;  // 战后回血到 20
    protected override int PostCombatGoldReward => 20;          // 战后获得 20 金币
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(20m),
        ModCardVars.Int("Gold", 20)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            // 悬停提示里展示升级版的“未来+”以及“临界状态”
            var upgradedFuture = ModelDb.Card<TokenFuture>().ToMutable();
            upgradedFuture.UpgradeInternal();

            return
            [
                HoverTipFactory.FromCard(upgradedFuture),
                HoverTipFactory.FromPower<CriticalState>()
            ];
        }
    }
}