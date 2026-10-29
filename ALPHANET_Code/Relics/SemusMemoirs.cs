using ALPHANET.Characters;
using ALPHANET.Keywords;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Relics;

[RegisterRelic(typeof(ALPHANETRelicPool))]
public sealed class SemusMemoirs : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    // 作为一个被动标记遗物，不需要动态变量
    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    // 悬浮提示：向玩家解释什么是“奇迹”
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(ALPHANETKeywords.Miracle)
    ];

    // 不需要写任何触发逻辑。
    // MiracleSystem 底层的 HasSemusMemoirs 方法会自动检测玩家是否拥有本遗物，
    // 并在计算透支代价时将其替换为 2，同时通过反射自动调用本遗物的 Flash() 发光。
}