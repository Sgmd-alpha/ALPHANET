using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Foot : ModCardTemplate
{
    // ==========================================
    // 核心机制：声明一个变量记录卡牌被保留的次数
    // ==========================================
    public int RetainedCount { get; set; } = 0;

    public Foot() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Retain 
    ];

    // ==========================================
    // 完美复刻官方 AshenStrike (灰烬打击) 的三变量分离模式
    // ==========================================
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 1. 基础基准值 (8)，绝不直接显示在卡面文本上
        new CalculationBaseVar(8m),
        
        // 2. 每次保留的增幅系数 (10)，专门用于本地化文本和绿色高亮
        new ExtraDamageVar(10m),
        
        // 3. 最终变色龙变量：将乘数动态绑定为“被保留的次数”
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? target) => 
        {
            return card is Foot footCard ? footCard.RetainedCount : 0;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        // 引擎会自动调用 CalculatedDamageVar 里的公式计算出正确的最终伤害，并附带力量/脆弱加成
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    public override Task AfterFlush(PlayerChoiceContext choiceContext, Player player, IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        if (retainedCards.Contains(this))
        {
            // 1. 每次被保留时，计数器 +1。图鉴和战斗 UI 会瞬间自动刷新伤害。
            RetainedCount++;

            // 2. 耗能永久 +1 
            EnergyCost.AddThisCombat(1);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        // 升级时：基础伤害 +2，保留增幅 +4
        // 如果 C# 编译器提示找不到 CalculationBase 属性，可替换为 DynamicVars["CalculationBase"].UpgradeValueBy(2m);
        DynamicVars.CalculationBase.UpgradeValueBy(2m);
        DynamicVars.ExtraDamage.UpgradeValueBy(4m);
    }
}