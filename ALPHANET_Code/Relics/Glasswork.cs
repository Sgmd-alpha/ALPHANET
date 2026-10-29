using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Relics;

[RegisterRelic(typeof(ALPHANETRelicPool))]
public sealed class Glasswork : ModRelicTemplate
{
    private bool _usedThisTurn;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    private bool UsedThisTurn
    {
        get => _usedThisTurn;
        set
        {
            AssertMutable();
            _usedThisTurn = value;
        }
    }

    // 回合开始时，重置触发标记
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner != null && participants.Contains(Owner.Creature))
        {
            UsedThisTurn = false;
        }
        return Task.CompletedTask;
    }

    // 战斗结束时，重置触发标记以防跨战斗状态残留
    public override Task AfterCombatEnd(CombatRoom room)
    {
        UsedThisTurn = false;
        return Task.CompletedTask;
    }

    // 监听生成卡牌事件
    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator != null && creator == Owner && !UsedThisTurn)
        {
            UsedThisTurn = true;
            Flash();
            
            var ctx = new ThrowingPlayerChoiceContext();
            
            await PowerCmd.Apply<StrengthPower>(
                ctx, 
                Owner.Creature, 
                DynamicVars.Strength.BaseValue, 
                Owner.Creature, 
                null
            );
        }
    }
}