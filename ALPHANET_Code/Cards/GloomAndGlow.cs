using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class GloomAndGlow : ModCardTemplate, ICriticalIndicatorCard
{
    public bool IsModeB { get; set; } = false;
    public CriticalIndicatorType GetCriticalIndicator()
    {
        // A形态返回紫色圆圈(Enter)，B形态返回白色圆圈(Exit)
        return IsModeB ? CriticalIndicatorType.Exit : CriticalIndicatorType.Enter;
    }
    public CriticalIndicatorType GetPreviewCriticalIndicator() => CriticalIndicatorType.EnterThenExit;
    public GloomAndGlow() : base(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DynamicVar("DamageA", 12m),
        new DynamicVar("DamageB", 4m),
        new DynamicVar("BlockB", 4m),
        new DynamicVar("VulnB", 1m),
        
        // 声明一个状态开关变量，初始为 0 (代表模式 A)
        new DynamicVar("ModeBFlag", 0m) 
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            var tips = new List<IHoverTip>();
            if (!IsModeB) 
                tips.Add(HoverTipFactory.FromPower<CriticalState>()); 
            else 
                tips.Add(HoverTipFactory.FromPower<VulnerablePower>());
            return tips;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        if (!IsModeB)
        {
            // --- 效果 A ---
            await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);
            await CriticalState.Enter(choiceContext, player.Creature, player.Creature, this, false);

            await DamageCmd.Attack(DynamicVars["DamageA"].BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState!)
                .WithHitFx("vfx/vfx_heavy_blunt", null, "heavy_attack.mp3")
                .Execute(choiceContext);
        }
        else
        {
            // --- 效果 B ---
            await CreatureCmd.TriggerAnim(player.Creature, "Buff", player.Character.CastAnimDelay);
            
            var critPower = player.Creature.GetPower<CriticalState>();
            if (critPower != null) 
            {
                await PowerCmd.Remove(critPower);
            }

            decimal blk = DynamicVars["BlockB"].BaseValue;
            await CreatureCmd.GainBlock(player.Creature, blk, ValueProp.Unpowered, null);

            await DamageCmd.Attack(DynamicVars["DamageB"].BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState!)
                .WithHitFx("vfx/vfx_attack_blunt", null, "heavy_attack.mp3")
                .Execute(choiceContext);

            IReadOnlyList<Creature> enemies = CombatState!.HittableEnemies;
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext, 
                enemies, 
                DynamicVars["VulnB"].BaseValue, 
                player.Creature, 
                this
            );
        }
        IsModeB = !IsModeB;
        DynamicVars["ModeBFlag"].BaseValue = IsModeB ? 1m : 0m;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DamageA"].UpgradeValueBy(6m);
        DynamicVars["BlockB"].UpgradeValueBy(2m);
        DynamicVars["DamageB"].UpgradeValueBy(2m);
    }
}