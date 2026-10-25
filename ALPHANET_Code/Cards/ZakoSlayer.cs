using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class ZakoSlayer : ModCardTemplate
{
    public ZakoSlayer() : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }
    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 尝试造成的伤害：24 (升级后 30)
        new DamageVar(24m, ValueProp.Move),
        // 拼刀胜利获得的格挡：16 (升级后 20)
        new BlockVar(16m, ValueProp.Move),

        // 实时显示目标敌人的本回合意图总伤害
        ModCardVars.Computed(
            "EnemyIntentDmg",
            ctx => 
            {
                var target = ctx.Target ?? ctx.CombatState?.HittableEnemies.FirstOrDefault();
                var owner = ctx.Card?.Owner;
                return (target != null && owner != null) ? GetEnemyIntentTotalDamage(target, owner) : 0m;
            },
            baseValue: 0m)
    ];

    // 当场上存在任何一个敌人可以被这张牌“拼刀压制”时，边框发金光提示
    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            if (CombatState == null || Owner == null) return false;
            return CombatState.HittableEnemies.Any(enemy => 
                CalculateCardFinalDamageTo(enemy) > GetEnemyIntentTotalDamage(enemy, Owner));
        }
    }

    // ==========================================
    // 打出效果：拼刀判定
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        var target = cardPlay.Target;
        var player = Owner!;

        // 1. 计算这张牌即将对该敌人造成的最终攻击伤害（吃力量、易伤、虚弱等一切修正）
        int myAttackDamage = CalculateCardFinalDamageTo(target, cardPlay);

        // 2. 读取该敌人本回合针对本卡主人的意图总伤害（吃敌人力量、虚弱、以及我方无实体变1等修正）
        int enemyIntentDamage = GetEnemyIntentTotalDamage(target, player);

        // 3. 拼刀判定：只有当【即将造成的攻击伤害 > 该敌人本回合意图伤害】时，才造成伤害并获得格挡！
        if (myAttackDamage > enemyIntentDamage)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);

            await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, cardPlay);
        }
    }

    // ==========================================
    // 核心算法 1：精准计算本卡对指定目标的最终伤害
    // ==========================================
    private int CalculateCardFinalDamageTo(Creature target, CardPlay? cardPlay = null)
    {
        if (Owner?.Creature == null || CombatState == null)
            return DynamicVars.Damage.IntValue;

        // 直接调用与 AttackIntent 内部同款的官方伤害管线 Hook.ModifyDamage！
        decimal modified = Hook.ModifyDamage(
            Owner.RunState,
            CombatState,
            target,                 // 受击者：目标敌人
            Owner.Creature,         // 攻击者：玩家自己
            DynamicVars.Damage.BaseValue,
            ValueProp.Move,
            this,                   // 卡牌来源
            cardPlay,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out _
        );

        return Math.Max(0, (int)modified);
    }

    // ==========================================
    // 核心算法 2：精准读取敌人头顶意图的红字总伤害（100% 联机安全）
    // ==========================================
    public static int GetEnemyIntentTotalDamage(Creature enemy, Player cardOwner)
    {
        if (enemy == null || !enemy.IsMonster || enemy.Monster?.NextMove?.Intents == null || enemy.CombatState == null)
            return 0;

        int totalIntentDamage = 0;

        // 直接遍历敌人本回合动作中的所有 AttackIntent（原生强类型，告别反射！）
        foreach (var attackIntent in enemy.Monster.NextMove.Intents.OfType<AttackIntent>())
        {
            if (attackIntent.DamageCalc == null) continue;

            // 完美复刻 AttackIntent.GetSingleDamage，但将受击参考目标固定为 cardOwner.Creature（杜绝 LocalContext.GetMe 带来的联机不同步！）
            decimal rawDmg = attackIntent.DamageCalc();
            decimal modifiedSingleDmg = Hook.ModifyDamage(
                cardOwner.RunState,
                enemy.CombatState,
                cardOwner.Creature, // 受击者：打出这张牌的玩家（如果该玩家有【无实体】，这里会自动把伤害压成 1！）
                enemy,              // 攻击者：敌人
                rawDmg,
                ValueProp.Move,
                null,
                null,
                ModifyDamageHookType.All,
                CardPreviewMode.None,
                out _
            );

            int singleHit = Math.Max(0, (int)modifiedSingleDmg);

            // AttackIntent 默认单段攻击时 Repeats 为 0，多段攻击（如 6x3）时 Repeats 为 3
            int hitCount = Math.Max(1, attackIntent.Repeats);

            totalIntentDamage += singleHit * hitCount;
        }

        return totalIntentDamage;
    }

    // ==========================================
    // 升级逻辑
    // ==========================================
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m); // 24 -> 30
        DynamicVars.Block.UpgradeValueBy(4m);  // 16 -> 20
    }
}