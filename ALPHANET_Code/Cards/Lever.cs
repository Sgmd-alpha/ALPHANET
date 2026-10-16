using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Lever : ModCardTemplate
{
    public Lever() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // ==========================================
    // 完美沿用“三变量分离法”，并利用底层机制做差值补偿
    // ==========================================
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 1. 基础伤害 (8)
        new CalculationBaseVar(8m),
        
        // 2. 专门用于显示在卡面上的“力量倍率”（3倍 / 升级后4倍）
        ModCardVars.Int("StrengthMult", 3),

        // 3. 给引擎底层 CalculatedDamageVar 乘法公式做垫脚石的固定系数 1
        new ExtraDamageVar(1m),
        
        // 4. 最终计算值 (变色龙变量)
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? target) => 
        {
            if (card.Owner?.Creature == null) return 0;

            // 获取玩家身上的真实力量层数 (完全兼容负数力量)
            int strength = card.Owner.Creature.GetPower<StrengthPower>()?.Amount ?? 0;
            
            // 读取卡面上显示的那个倍率 (3 或 4)
            int mult = card.DynamicVars.GetIntOrDefault("StrengthMult", 3);

            // 【核心算法】：
            // 公式：CalculatedDamage = Base + Extra(1) * Multiplier
            // 因为 ValueProp.Move 在引擎最终结算时会自动加上 1 倍力量。
            // 因此我们在这里只需要让 Multiplier 等于 (倍率 - 1) 倍的力量。
            // 例：玩家力量为 5，倍率为 3。我们在这里返回 5 * 2 = 10 点额外伤害。
            // 引擎底层再加 5 点。总伤害增加 15 点（完美等于 5 * 3）。
            return strength * (mult - 1);
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        // 直接把变色龙变量塞给底层，引擎会自动解包
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt", null, "blunt_attack.mp3") // 配一个符合“杠杆”的钝器重击音效
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 升级时：基础伤害从 8 变为 10
        DynamicVars.CalculationBase.UpgradeValueBy(2m);
        // 力量倍率从 3 变为 4
        DynamicVars["StrengthMult"].UpgradeValueBy(1m);
    }
}