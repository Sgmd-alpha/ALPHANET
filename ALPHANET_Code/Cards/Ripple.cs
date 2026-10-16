using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
public sealed class Ripple : ModCardTemplate
{
    public override bool GainsBlock => true;

    public Ripple() : base(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new BlockVar(5m, ValueProp.Move), 
        ModCardVars.Int("Weak", 1),
        ModCardVars.Int("Vulnerable", 2)
    ];

    // 悬浮提示
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            int weak = DynamicVars.GetIntOrDefault("Weak", 1);
            int vulnerable = DynamicVars.GetIntOrDefault("Vulnerable", 2);
            return new List<IHoverTip>
            {
                HoverTipFactory.FromPower<WeakPower>(weak),
                HoverTipFactory.FromPower<VulnerablePower>(vulnerable)
            };
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);
        await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, cardPlay);

        int weakAmt = DynamicVars.GetIntOrDefault("Weak", 1);
        int vulnAmt = DynamicVars.GetIntOrDefault("Vulnerable", 2);

        foreach (Creature hittableEnemy in CombatState!.HittableEnemies)
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, hittableEnemy, weakAmt, player.Creature, this);
            await PowerCmd.Apply<VulnerablePower>(choiceContext, hittableEnemy, vulnAmt, player.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        RemoveKeyword(CardKeyword.Exhaust);
    }
}