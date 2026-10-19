using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class PickOff : ModCardTemplate
{
    // ==========================================
    // 核心声明：这是一张 X 费牌
    // ==========================================
    protected override bool HasEnergyCostX => true;

    // X费牌的构造函数，基础费用必须填 0
    // 因为是全自动寻敌，不需要玩家用鼠标指目标，所以 TargetType 设为 None
    public PickOff() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.None)
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
        // 基础伤害 (9点，受力量加成)
        new DamageVar(9m, ValueProp.Move)
    ];

    // ==========================================
    // 打出效果
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 获取本次 X 的真实值 (引擎会自动扣除全部剩余能量，并计算诸如“下张X费牌效果+1”之类的遗物修饰)
        int xValue = ResolveEnergyXValue();

        // 2. 循环 X 次，实现动态“逐个击破”
        for (int i = 0; i < xValue; i++)
        {
            // 每次攻击前，重新获取场上可被攻击的敌人
            IReadOnlyList<Creature> hittableEnemies = base.CombatState!.HittableEnemies;
            
            // 如果敌人都被砍光了，提前结束循环，防止报错
            if (hittableEnemies.Count == 0)
            {
                break;
            }

            // 3. 寻找【当前】生命值最低的敌人 (完美复刻机器人的黑球逻辑)
            Creature? weakestEnemy = hittableEnemies.MinBy(c => c.CurrentHp);

            if (weakestEnemy != null)
            {
                // 4. 对这个倒霉蛋劈一刀
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(weakestEnemy)
                    .Execute(choiceContext);
            }
        }
    }

    // ==========================================
    // 升级逻辑
    // ==========================================
    protected override void OnUpgrade()
    {
        // 伤害 +3 (9 -> 12)
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}