using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Parallel : ModCardTemplate
{
    // 这张卡牌的主动效果包含提供格挡，因此开启护盾预览
    public override bool GainsBlock => true;

    // 因为它的效果是对着所有敌人生效（数人头），官方标准做法是设定为 AllEnemies
    public Parallel() : base(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 添加“虚无”关键字
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Ethereal 
    ];

    // 声明单次的格挡基础值：6 点
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new BlockVar(6m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 播放施法动画
        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        // 2. 遍历所有可被命中的敌人（自动排除了尸体和逃跑的怪）
        foreach (Creature enemy in CombatState!.HittableEnemies)
        {
            // 每次遍历到一个活着的敌人，就调用底层方法给玩家套一次格挡
            // 底层 GainBlock 方法会自动处理玩家的敏捷、脆弱等状态计算，以及飘字音效
            await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级时格挡值增加 2（从 6 变成 8）
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}