using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
    // 显式声明本牌包含格挡，激活 UI 护盾预览
    public override bool GainsBlock => true;

    public Recall() : base(2, CardType.Skill, CardRarity.Common, TargetType.Self) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 声明基础值为 10 的格挡变量
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new BlockVar(10m, ValueProp.Move)
    ];

    // 悬浮提示：向玩家展示《记忆》这张衍生牌的效果
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<TokenMemories>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 播放施法动画
        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        // 2. 获得格挡（调用系统底层方法，自动计算敏捷和脆弱）
        await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, cardPlay);

        // 3. 生成一张《记忆》衍生牌
        CardModel generatedCard = CombatState!.CreateCard<TokenMemories>(player);

        // 核心联动：如果本牌（回忆）已升级，那么生成的衍生牌（记忆）也一并升级
        if (IsUpgraded)
        {
            CardCmd.Upgrade(generatedCard);
        }

        // 4. 将其加入手牌，并套上官方的视觉展示特效
        CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, player));
    }

    protected override void OnUpgrade()
    {
        // 升级时格挡值增加 2（从 10 变成 12）
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}