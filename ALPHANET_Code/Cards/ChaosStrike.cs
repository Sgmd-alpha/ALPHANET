using ALPHANET.Characters;
using Godot;
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
public sealed class ChaosStrike : ModCardTemplate
{
    public ChaosStrike() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override HashSet<CardTag> CanonicalTags => 
    [
        CardTag.Strike
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
    ];

    // ==========================================
    // 动态变量与【ComputedDynamicVar 预测魔法】
    // ==========================================
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 基础伤害
        new DamageVar(8m, ValueProp.Move),
        
        // 增伤幅度 (等同于 new IntVar，但通过 RitsuLib 注册)
        ModCardVars.Int("Scale", 3),

        // 【新科技】：动态预测将要消耗的卡牌数量
        ModCardVars.Computed(
            "StatusCount",
            static ctx =>
            {
                if (ctx.Player == null) return 0m;
                var hand = PileType.Hand.GetPile(ctx.Player).Cards;
                return hand.Count(c => c.Type == CardType.Status || c is StatusChaos);
            },
            baseValue: 0m),

        // 【新科技】：动态预测本次打出后，这张牌会永久增加多少伤害
        ModCardVars.Computed(
            "GrowthPreview",
            static ctx =>
            {
                if (ctx.Player == null) return 0m;
                var hand = PileType.Hand.GetPile(ctx.Player).Cards;
                
                int normalCount = hand.Count(c => c.Type == CardType.Status && c is not StatusChaos);
                int chaosCount = hand.Count(c => c is StatusChaos);
                
                // 安全地通过上下文读取 Scale 的基础值
                decimal scale = ctx.GetCardBaseValueOrDefault("Scale", 3m);
                
                return (normalCount * scale) + (chaosCount * scale * 2m);
            },
            baseValue: 0m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 1. 【先造成伤害】：此时的面板伤害是绝对准确的，享受当前的力量和易伤
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 2. 获取手牌中的状态牌和混沌
        List<CardModel> handCards = PileType.Hand.GetPile(Owner).Cards.ToList();
        List<CardModel> statusCards = handCards.Where(c => c.Type == CardType.Status || c is StatusChaos).ToList();

        decimal totalBonusDamage = 0m;
        // 使用 RitsuLib 提供的扩展方法安全读取当前增伤幅度
        decimal scale = DynamicVars.GetValueOrDefault("Scale", 3m);

        // 3. 【后消耗与计算】：此时消耗的动作会触发你的“加力量和敏捷”能力，但这只会对你后续打出的牌生效，绝不干扰刚才的伤害结算！
        foreach (CardModel card in statusCards)
        {
            await CardCmd.Exhaust(choiceContext, card);

            if (card is StatusChaos)
                totalBonusDamage += scale * 2m;
            else
                totalBonusDamage += scale;
        }

        // 4. 【永久成长】：将本回合吸收的伤害永久写入这张牌！下一次打出时，它将变得更加致命。
        if (totalBonusDamage > 0)
        {
            DynamicVars.Damage.BaseValue += totalBonusDamage;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Scale"].UpgradeValueBy(1m);
    }
}