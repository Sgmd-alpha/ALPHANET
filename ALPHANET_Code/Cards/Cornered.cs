using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Cornered : ModCardTemplate
{
    public Cornered() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 判定当前是否满足绝境条件（血量 <= 20）
    private bool IsCorneredActive => 
        Owner?.Creature != null && 
        Owner.Creature.CurrentHp <= DynamicVars.GetIntOrDefault("HpThreshold", 20);

    // 满足条件时发金光
    protected override bool ShouldGlowGoldInternal => IsCorneredActive;

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(8m, ValueProp.Move),
        new RepeatVar(2),
        ModCardVars.Int("HpThreshold", 20)
    ];

    // ==========================================
    // 官方风格的动态费用同步机制
    // ==========================================
    
    // 1. 当卡牌进入战斗时初始化动态费用
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this)
        {
            UpdateCostState();
        }
        return Task.CompletedTask;
    }

    // 2. 只要场上有任何卡牌打出（比如玩家受到了伤害、或者用了回复手段导致血量变化），立刻刷新费用
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner)
        {
            UpdateCostState();
        }
        return Task.CompletedTask;
    }

    // 核心费率计算与刷新方法
    private void UpdateCostState()
    {
        if (Owner?.Creature == null) return;

        // 基础费用是 1。如果满足血量 <= 20，我们需要让它的费用在本回合变成 0
        // 通过计算当前有效费用与 0 的差值进行动态增减
        int currentEffectiveCost = EnergyCost.GetWithModifiers(CostModifiers.Local);
        
        if (IsCorneredActive && currentEffectiveCost > 0)
        {
            EnergyCost.AddThisTurn(-currentEffectiveCost); // 差多少扣多少，直接变成 0 费！
        }
    }

    // ==========================================
    // 打出效果
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 再次安全确认打出瞬间是否处于绝境
        int hitCount = IsCorneredActive ? DynamicVars.Repeat.IntValue : 1;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hitCount)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    // ==========================================
    // 升级逻辑
    // ==========================================
    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}