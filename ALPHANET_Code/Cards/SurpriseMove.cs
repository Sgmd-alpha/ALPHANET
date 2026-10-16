using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.States;
using ALPHANET.Systems;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class SurpriseMove : ModCardTemplate
{
    // 耗能 2，技能牌，普通稀有度，目标为单体敌人
    public SurpriseMove() : base(2, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }
    // ==========================================
    // 1. 注册关键词 (用于 UI 悬浮提示)
    // ==========================================
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        ALPHANETKeywords.Miracle, // 奇迹
    ];
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");
    // ==========================================
    // 2. 注册标签 (供底层引擎补丁拦截，以及其他卡牌检索)
    // ==========================================
    protected override HashSet<CardTag> CanonicalTags => 
    [
        ALPHANETTags.Miracle
    ];

    // ==========================================
    // 3. 注册动态数值变量
    // ==========================================
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new BlockVar(6m, ValueProp.Move),
        new DynamicVar("HpLoss", 6m) 
    ];

    // ==========================================
    // 4. 打出效果
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 奇迹判定与代扣生命值
        await MiracleSystem.PayMiracleCostIfNeeded(this, choiceContext, cardPlay);

        // 敌人失去生命 (无视护甲、无视能力)
        if (cardPlay.Target != null)
        {
            await CreatureCmd.Damage(
                choiceContext, 
                cardPlay.Target, 
                DynamicVars["HpLoss"].BaseValue, // 核心修改：正常从字典读取，绝对安全且每张牌独立
                ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, 
                this, 
                cardPlay
            );
        }
        
        // 获得格挡
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        
        // 退出临界状态
        var criticalState = Owner.Creature.Powers.FirstOrDefault(p => p is CriticalState);
        await PowerCmd.Remove(criticalState);
        // 永久减费
        EnergyCost.AddThisCombat(-1);
    }

    // ==========================================
    // 5. 升级效果
    // ==========================================
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["HpLoss"].UpgradeValueBy(2m);
    }
}