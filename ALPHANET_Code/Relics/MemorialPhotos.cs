using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Relics;

[RegisterRelic(typeof(ALPHANETRelicPool))]
public sealed class MemorialPhotos : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            IReadOnlyList<CardModel> handCards = PileType.Hand.GetPile(Owner).Cards;
            
            if (handCards.Count != 0)
            {
                // 统计手牌中带有【保留】词条的卡牌数量
                int retainCount = handCards.Count(c => c != null && c.Keywords != null && c.Keywords.Contains(CardKeyword.Retain));

                if (retainCount > 0)
                {
                    decimal totalDamage = retainCount * DynamicVars.Damage.BaseValue;
                    
                    Flash();
                    var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();
                    await CreatureCmd.Damage(
                        ctx,
                        Owner.Creature.CombatState!.HittableEnemies,
                        totalDamage,
                        ValueProp.Unpowered,
                        Owner.Creature
                    );
                }
            }
        }
    }
}