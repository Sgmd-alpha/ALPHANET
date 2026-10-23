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
public sealed class RepulsionField : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Enter;
    
    public RepulsionField() : base(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 初始状态下没有关键字
    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    // 抽牌和易伤变量
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        ModCardVars.Int("Draw", 2),
        ModCardVars.Int("Vulnerable", 1)
    ];

    // 悬浮提示，临界状态和易伤
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            int vulnerable = DynamicVars.GetIntOrDefault("Vulnerable", 1);
            return new List<IHoverTip>
            {
                HoverTipFactory.FromPower<CriticalState>(),
                HoverTipFactory.FromPower<VulnerablePower>(vulnerable)
            };
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 施法动画
        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        // 2. 进入临界状态
        await CriticalState.Enter(choiceContext, player.Creature, player.Creature, this, false);

        // 3. 抽牌
        int drawAmt = DynamicVars.GetIntOrDefault("Draw", 2);
        await CardPileCmd.Draw(choiceContext, drawAmt, Owner);

        // 4. 群体易伤
        int vulnAmt = DynamicVars.GetIntOrDefault("Vulnerable", 1);
        foreach (Creature enemy in CombatState!.HittableEnemies)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy, vulnAmt, player.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Vulnerable"].UpgradeValueBy(1m);
        AddKeyword(CardKeyword.Retain);
    }
}