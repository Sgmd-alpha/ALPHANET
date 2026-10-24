using System.Runtime.CompilerServices;
using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class ChaosStrike : ModCardTemplate
{
    // =========================================================
    // 跨战斗安全隔离的“全体混沌打击增伤账本”
    // （即使是后续回合新生成/复制出的混沌打击，也能自动享受全部加成！）
    // =========================================================
    private class ChaosStrikeBonusTracker
    {
        public WeakReference<ICombatState>? CombatRef;
        public decimal BonusDamage = 0m;

        public decimal GetBonus(ICombatState? currentCombat)
        {
            if (currentCombat == null || CombatRef == null)
                return 0m;

            if (CombatRef.TryGetTarget(out var recordedCombat) && ReferenceEquals(recordedCombat, currentCombat))
            {
                return BonusDamage;
            }

            return 0m;
        }

        public void AddBonus(ICombatState? currentCombat, decimal amount)
        {
            if (currentCombat == null || amount <= 0m) return;

            if (CombatRef != null && CombatRef.TryGetTarget(out var recordedCombat) && ReferenceEquals(recordedCombat, currentCombat))
            {
                BonusDamage += amount;
            }
            else
            {
                CombatRef = new WeakReference<ICombatState>(currentCombat);
                BonusDamage = amount;
            }
        }
    }

    private static readonly ConditionalWeakTable<Creature, ChaosStrikeBonusTracker> _bonusTrackers = new();

    private static decimal GetCombatBonusDamage(Creature? owner)
    {
        if (owner == null || owner.CombatState == null) return 0m;
        return _bonusTrackers.GetOrCreateValue(owner).GetBonus(owner.CombatState);
    }

    private static void AddCombatBonusDamage(Creature? owner, decimal amount)
    {
        if (owner == null || owner.CombatState == null) return;
        _bonusTrackers.GetOrCreateValue(owner).AddBonus(owner.CombatState, amount);
    }

    // =========================================================

    public ChaosStrike() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override HashSet<CardTag> CanonicalTags => 
    [
        CardTag.Strike
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard(ModelDb.Card<StatusChaos>())
    ];

    // ==========================================
    // 动态变量
    // ==========================================
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 基础伤害：8 (升级后 10)
        new DamageVar(8m, ValueProp.Move),
        
        // 每张状态牌增伤幅度：3 (升级后 4)
        ModCardVars.Int("Scale", 3),

        // 动态预测将要消耗的状态牌数量
        ModCardVars.Computed(
            "StatusCount",
            static ctx =>
            {
                if (ctx.Player == null) return 0m;
                var hand = PileType.Hand.GetPile(ctx.Player).Cards;
                return hand.Count(c => c.Type == CardType.Status || c is StatusChaos);
            },
            baseValue: 0m),

        // 动态预测本次打出后，所有混沌打击会增加多少伤害
        ModCardVars.Computed(
            "GrowthPreview",
            static ctx =>
            {
                if (ctx.Player == null) return 0m;
                var hand = PileType.Hand.GetPile(ctx.Player).Cards;
                
                int normalCount = hand.Count(c => c.Type == CardType.Status && c is not StatusChaos);
                int chaosCount = hand.Count(c => c is StatusChaos);
                
                decimal scale = ctx.GetCardBaseValueOrDefault("Scale", 3m);
                
                return (normalCount * scale) + (chaosCount * scale * 2m);
            },
            baseValue: 0m)
    ];

    // ==========================================
    // 核心：将本场战斗累积的增伤实时加到所有《混沌打击》上！
    // （自动同步手牌绿字面板预览与实际攻击结算）
    // ==========================================
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (cardSource == this && Owner?.Creature != null)
        {
            return GetCombatBonusDamage(Owner.Creature);
        }
        return 0m;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 1. 【先造成伤害】：ModifyDamageAdditive 会自动加上此前累积的全局混沌打击增伤
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 2. 获取手牌中的状态牌和混沌
        List<CardModel> handCards = PileType.Hand.GetPile(Owner!).Cards.ToList();
        List<CardModel> statusCards = handCards.Where(c => c.Type == CardType.Status || c is StatusChaos).ToList();

        decimal totalBonusDamage = 0m;
        decimal scale = DynamicVars.GetValueOrDefault("Scale", 3m);

        // 3. 【后消耗与计算】
        foreach (CardModel card in statusCards)
        {
            await CardCmd.Exhaust(choiceContext, card);

            if (card is StatusChaos)
                totalBonusDamage += scale * 2m;
            else
                totalBonusDamage += scale;
        }

        // 4. 【全局成长】：将本次吸收的伤害记入本场战斗的全局账本，所有《混沌打击》（包括未来新生成的）立刻同步享受增伤！
        if (totalBonusDamage > 0m)
        {
            AddCombatBonusDamage(Owner!.Creature, totalBonusDamage);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);      // 8 -> 10
        DynamicVars["Scale"].UpgradeValueBy(1m);    // 3 -> 4
    }
}