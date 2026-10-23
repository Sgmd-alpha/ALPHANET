using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace ALPHANET.Cards;


[RegisterCard(typeof(ALPHANETCardPool))]
[RegisterCharacterStarterCard(typeof(ALPHANETCharacter), 1)]
public sealed class Venture : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Enter;
    public Venture() : base(0, CardType.Skill, CardRarity.Basic, TargetType.AllEnemies) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // ==========================================
    // 显式声明数值变量，彻底告别 Magic
    // ==========================================
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Weak", 1),
        ModCardVars.Int("Vulnerable", 1)
    ];

    // ==========================================
    // 动态生成悬浮提示，与实际变量层数挂钩
    // ==========================================
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            int weak = DynamicVars.GetIntOrDefault("Weak", 1);
            int vulnerable = DynamicVars.GetIntOrDefault("Vulnerable", 1);
            return new List<IHoverTip>
            {
                HoverTipFactory.FromPower<WeakPower>(weak),
                HoverTipFactory.FromPower<VulnerablePower>(vulnerable),
                HoverTipFactory.FromPower<CriticalState>() 
            };
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 分别读取专属变量中的层数
        int weakAmt = DynamicVars.GetIntOrDefault("Weak", 1);
        int vulnAmt = DynamicVars.GetIntOrDefault("Vulnerable", 1);

        foreach (Creature enemy in CombatState!.HittableEnemies)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy, vulnAmt, Owner.Creature, this);
            await PowerCmd.Apply<WeakPower>(choiceContext, enemy, weakAmt, Owner.Creature, this);
        }

        // 核心修正：必须调用专用的 Enter 方法，而不是底层 Apply
        await CriticalState.Enter(choiceContext, Owner.Creature, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        // 升级时，同步提升虚弱和易伤的层数
        DynamicVars["Weak"].UpgradeValueBy(1m);
        DynamicVars["Vulnerable"].UpgradeValueBy(1m);
    }
}