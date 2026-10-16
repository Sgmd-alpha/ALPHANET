using ALPHANET.Characters;
using ALPHANET.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Bludgeon : ModCardTemplate
{
    public Bludgeon() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Retain 
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 预设一个基础伤害用于引擎的正规流转（它会正常吃到力量加成）
        new DamageVar(14m, ValueProp.Move),
        // 显式声明保底伤害变量
        ModCardVars.Int("MinDamage", 14),
        ModCardVars.Int("Weak", 1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        var player = Owner!;
        var target = cardPlay.Target;

        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        // 1. 获取保底伤害数值
        decimal minDmg = DynamicVars.GetIntOrDefault("MinDamage", 14);

        // 2. 【核心修改】：寻找是否已经挂载过幽灵拦截器
        var interceptor = player.Creature.GetPower<BludgeonPower>();
        if (interceptor == null)
        {
            // 如果这场战斗是第一次打出《钝击》，则静默挂载它 (传入 true)
            await PowerCmd.Apply<BludgeonPower>(choiceContext, player.Creature, minDmg, player.Creature, this, true);
            interceptor = player.Creature.GetPower<BludgeonPower>();
        }

        // 3. 更新幽灵拦截器的状态（以防这回合保底伤害因升级或其他因素变化），并重置触发开关
        if (interceptor != null)
        {
            interceptor.Amount = (int)minDmg;
            interceptor.Triggered = false;
        }

        // 4. 执行官方 Standard 攻击流程
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_heavy_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);

        // 5. 攻击执行完毕后，读取拦截器是否被触发
        // 【注意：这里我们彻底删除了 await PowerCmd.Remove 的代码！】
        bool triggered = interceptor?.Triggered ?? false;

        // 6. 如果拦截器被触发且有护盾，执行破盾
        if (triggered && target.Block > 0)
        {
            VfxCmd.PlayOnCreatureCenter(player.Creature, "vfx/vfx_flying_slash");
            await CreatureCmd.LoseBlock(choiceContext, target, target.Block, player.Creature);
        }

        // 7. 给予虚弱
        int weakAmt = DynamicVars.GetIntOrDefault("Weak", 1);
        await PowerCmd.Apply<WeakPower>(choiceContext, target, weakAmt, player.Creature, this);
    }
    protected override void OnUpgrade()
    {
        // 升级时：基础与保底伤害同步增加，虚弱层数增加
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars["MinDamage"].UpgradeValueBy(4m);
        DynamicVars["Weak"].UpgradeValueBy(1m);
    }
}