using ALPHANET.Keywords;
using ALPHANET.Systems;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using ALPHANET.Characters;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Trickery : ModCardTemplate
{
    // ==========================================
    // 基础设置：1费，攻击牌，全体敌人
    // ==========================================
    public Trickery() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // ==========================================
    // 关键词与标签注册
    // ==========================================
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        ALPHANETKeywords.Miracle, // 奇迹
    ];

    protected override HashSet<CardTag> CanonicalTags => 
    [
        ALPHANETTags.Miracle
    ];

    // ==========================================
    // 动态变量声明
    // ==========================================
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 群体伤害 (13点，受力量加成)
        new DamageVar(13m, ValueProp.Move)
    ];

    // ==========================================
    // 核心限制逻辑：仅在剩余能量为 0 时可打出
    // ==========================================
    protected override bool IsPlayable
    {
        get
        {
            // 确保 Owner 和 PlayerCombatState 不为空
            if (Owner?.PlayerCombatState == null) return false;

            // 检查玩家当前的剩余能量是否等于 0
            // (注意：这里使用 <= 0 是为了容错，尽管通常能量不会小于 0)
            return Owner.PlayerCombatState.Energy <= 0;
        }
    }

    // ==========================================
    // 打出时的效果
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 奇迹判定与代扣代价 (如果该牌当前不是 0 费，则触发奇迹代价)
        await MiracleSystem.PayMiracleCostIfNeeded(this, choiceContext, cardPlay);

        // 造成全体伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!) // 使用官方推荐的 AOE 目标选定方法
            .WithHitFx("vfx/vfx_attack_slash")      // 替换为你想要的攻击特效
            .Execute(choiceContext);
    }

    // ==========================================
    // 升级逻辑
    // ==========================================
    protected override void OnUpgrade()
    {
        // 升级时伤害 +4 (13 -> 17)
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}