using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Counter : ModCardTemplate
{
    public Counter() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 1. 提取出的衰减系数，完全替代原先文本里的魔法数字 "2"
        ModCardVars.Int("LossPerEnergy", 2),

        // 2. 最终计算出的格挡值
        ModCardVars.ComputedBlock(
            "ComputedBlock",
            static ctx =>
            {
                // 获取基础护盾值 (9)
                decimal baseBlock = ctx.BaseValue;
                
                // 图鉴模式防空保护：如果不在战斗中，直接返回基础值 9
                if (!ctx.IsInCombat || ctx.CombatState is null || ctx.Player is null)
                {
                    return baseBlock;
                }

                // 动态获取当前的衰减系数 (2)
                decimal lossMultiplier = ctx.Card != null 
                    ? ctx.Card.DynamicVars.GetIntOrDefault("LossPerEnergy", 2) 
                    : 2m;

                // 结算本回合消耗的能量
                int energySpent = (
                    from e in CombatManager.Instance.History.Entries.OfType<EnergySpentEntry>()
                    where e.HappenedThisTurn(ctx.CombatState)
                       && e.Actor?.Player == ctx.Player
                    select e.Amount
                ).Sum();

                if (ctx.Card?.Pile != null && ctx.Card.Pile.Type == PileType.Play)
                {
                    energySpent -= ctx.Card.EnergyCost.GetWithModifiers(CostModifiers.All);
                }

                if (energySpent < 0) energySpent = 0;

                // 基础值 - (消耗能量 * 衰减系数)
                return Math.Max(0m, baseBlock - energySpent * lossMultiplier);
            },
            baseValue: 9m),

        // 3. 虚弱层数
        new PowerVar<WeakPower>(1m)
    ];

    protected override HashSet<CardTag> CanonicalTags => new();

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 获取经过委托计算后的真实格挡数值
        int finalBlock = (int)DynamicVars.EvaluateValueOrDefault("ComputedBlock");

        if (finalBlock > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature, finalBlock, ValueProp.Move, cardPlay);
        }

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 升级时，提升基础格挡值（9 -> 12）
        DynamicVars["ComputedBlock"].UpgradeValueBy(3m);
        
        // （可选）如果你希望升级后不仅格挡增加，还能让虚弱增加，可以继续追加：
        // DynamicVars.Weak.UpgradeValueBy(1m);
    }
}