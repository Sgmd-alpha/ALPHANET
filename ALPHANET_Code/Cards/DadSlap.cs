using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class DadSlap : ModCardTemplate
{
    public DadSlap() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // ==========================================
    // 动态变量声明
    // ==========================================
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(14m, ValueProp.Move)
    ];

    // ==========================================
    // 条件判定：手牌中是否存在其他的攻击或技能牌
    // ==========================================
    private bool CanDealDamage
    {
        get
        {
            if (Owner == null) return false;
            
            // 获取当前手牌
            var handCards = CardPile.GetCards(Owner, PileType.Hand);
            
            // 如果手牌里没有任何一张（不是自己，且类型为攻击或技能）的牌，则返回 true
            return !handCards.Any(c => c != this && (c.Type == CardType.Attack || c.Type == CardType.Skill));
        }
    }

    // 满足条件时边框发金光
    protected override bool ShouldGlowGoldInternal => CanDealDamage;

    // ==========================================
    // 打出效果
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 只有满足条件才造成伤害，否则空打
        if (CanDealDamage)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState!) // 对所有敌人造成伤害
                .Execute(choiceContext);
        }
    }

    // ==========================================
    // 回合开始时：从消耗牌堆返回手牌
    // ==========================================
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        // 如果触发事件的是卡牌主人，并且这张牌当前在消耗牌堆中
        if (player == Owner && Pile?.Type == PileType.Exhaust)
        {
            // 将其放入手牌
            await CardPileCmd.Add(this, PileType.Hand);
        }
    }

    // ==========================================
    // 升级逻辑
    // ==========================================
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m); // 14 -> 20
    }
}