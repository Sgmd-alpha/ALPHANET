using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
[RegisterCharacterStarterCard(typeof(ALPHANETCharacter), 1)] 
public sealed class Salvage : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Basic;
    private const TargetType CardTarget = TargetType.AllEnemies; 
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => [ CardKeyword.Retain ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    public Salvage() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // ==========================================
    // 视觉反馈
    // ==========================================
    protected override bool ShouldGlowGoldInternal => Owner?.Creature?.HasPower<CriticalState>() ?? false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 获取临界状态（利用引擎自带的安全机制，没有则返回 null）
        var criticalState = player.Creature.GetPower<CriticalState>();
        
        // 2. 根据是否存在临界状态，决定攻击段数
        int attackTimes = criticalState != null ? 2 : 1;

        // 3. 执行官方标准的多段群体攻击
        // 使用 WithHitCount 告诉引擎这是多段连击，
        // 使用 TargetingAllOpponents 一次性锁定所有敌人
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(attackTimes)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute(choiceContext);

        // 4. 退出临界状态（底层 PowerCmd.Remove 自带防 null 检测）
        await PowerCmd.Remove(criticalState);
    }
    
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}