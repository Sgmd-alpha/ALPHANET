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
public sealed class Burnout : ModCardTemplate
{
    public Burnout() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        ALPHANETKeywords.Miracle, // 奇迹
        CardKeyword.Exhaust       // 消耗
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
        // 伤害：6 (升级后 8)
        new DamageVar(6m, ValueProp.Move),
        // 获得能量：1
        new EnergyVar(1),
        // 抽牌数：3 (升级后 5)
        new CardsVar(3)
    ];

    // ==========================================
    // 打出效果（奇迹代价已由 MiracleSystem 自动结算）
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 1. 对目标敌人造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 2. 获得 1 点能量
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner!);

        // 3. 抽 3 (5) 张牌
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner!);
    }

    // ==========================================
    // 升级逻辑
    // ==========================================
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m); // 6 -> 8
        DynamicVars.Cards.UpgradeValueBy(2m);  // 3 -> 5
    }
}

