using ALPHANET.Characters;
using ALPHANET.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class OutsidersSlumber : ModCardTemplate
{
    public OutsidersSlumber() : base(1, CardType.Skill, CardRarity.Rare, TargetType.None) { }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Exhaust
    ];

    // 【核心修复1】完全遵循教程，使用引擎内置的方法安全读取当前层数
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            // 缺失时默认返回 3
            int turns = DynamicVars.GetIntOrDefault("Turns", 3); 
            return new List<IHoverTip>
            {
                HoverTipFactory.FromPower<IntangiblePower>(turns),
                HoverTipFactory.FromPower<OutsidersSlumberPower>(turns),
                HoverTipFactory.FromPower<AbsentMindedPower>(turns)
            };
        }
    }

    // 【核心修复2】抛弃私有字段，使用 RitsuLib 规范的声明方式！
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [ 
        ModCardVars.Int("Turns", 3) 
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 如果拥有者为空，或者拥有者的肉体为空，直接退出。
        if (Owner?.Creature == null) return;

        int duration = DynamicVars.GetIntOrDefault("Turns", 3);
        
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        if (Owner.PlayerCombatState != null)
        {
            OutsidersSlumberPower.SlumberingStates.Add(Owner.PlayerCombatState);
        }
        await PowerCmd.Apply<IntangiblePower>(choiceContext, Owner.Creature, duration, Owner.Creature, this);
        await PowerCmd.Apply<OutsidersSlumberPower>(choiceContext, Owner.Creature, duration, Owner.Creature, this);

        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    // 【核心修复3】用引擎的方法安全升级，不再污染原卡！
    protected override void OnUpgrade() 
    { 
        DynamicVars["Turns"].UpgradeValueBy(1m);
    }
}