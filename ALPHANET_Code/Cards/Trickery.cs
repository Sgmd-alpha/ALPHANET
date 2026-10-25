using ALPHANET.Characters;
using ALPHANET.Keywords;
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
public sealed class Trickery : ModCardTemplate
{
    // ==========================================
    // 基础设置：1费，攻击牌，全体敌人
    // ==========================================
    public Trickery() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies, true)
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
        ALPHANETKeywords.Miracle
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
            if (Owner?.PlayerCombatState == null) return false;
            return Owner.PlayerCombatState.Energy <= 0;
        }
    }

    // ==========================================
    // 打出时的效果（奇迹代价已由 MiracleSystem 自动结算）
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 造成全体伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_slash")
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

