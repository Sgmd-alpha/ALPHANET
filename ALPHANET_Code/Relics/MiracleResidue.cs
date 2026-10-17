using ALPHANET.Cards;
using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Relics;

[RegisterRelic(typeof(ALPHANETRelicPool))] 
[RegisterCharacterStarterRelic(typeof(ALPHANETCharacter))]
public sealed class MiracleResidue : ModRelicTemplate
{
    private bool _usedThisCombat = false;
    public static bool IsPlayerMainPhase = false;

    // 公开只读属性：供 CriticalState 检查名刀是否还在
    public bool CanPreventDeath => !_usedThisCombat;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        .. HoverTipFactory.FromCardWithCardHoverTips<TokenFuture>(),
    ];

    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        IsPlayerMainPhase = false;
        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner && Owner.PlayerCombatState?.TurnNumber == 1)
        {
            Flash(); 
            List<CardModel> list =
            [
                Owner.Creature.CombatState!.CreateCard<TokenFuture>(Owner),
            ];
            await CardPileCmd.AddGeneratedCardsToCombat(list, PileType.Hand, Owner);
        }
    }

    public override Task AfterAutoPostPlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        // 【修复联机污染】：只监听自己的回合阶段变化
        if (player == Owner)
        {
            IsPlayerMainPhase = false; 
        }
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        // 【修复联机污染】：只监听自己的回合阶段变化
        if (player == Owner)
        {
            IsPlayerMainPhase = true;
        }
        return Task.CompletedTask;
    }

    public override bool ShouldDie(Creature creature)
    {
        if (creature != Owner.Creature || _usedThisCombat)
        {
            return true;
        }
        return false;
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        // 完美复刻官方 FairyInABottle 的写法，提供合法的上下文实例
        await TriggerDeathPrevention(new ThrowingPlayerChoiceContext(), creature);
    }

    public async Task TriggerDeathPrevention(PlayerChoiceContext choiceContext, Creature creature)
    {
        Flash();
        _usedThisCombat = true;

        decimal healAmount = 1m - creature.CurrentHp;
        if (healAmount > 0)
        {
            await CreatureCmd.Heal(creature, healAmount);
        }

        await CriticalState.Enter(choiceContext, creature, null, null, true);
    }

    public override async Task AfterCombatEnd(CombatRoom _)
    {
        decimal targetHp = Math.Min(10m, Owner.Creature.MaxHp);
        if (Owner.Creature.CurrentHp < targetHp)
        {
            Flash();
            decimal amountToHeal = targetHp - Owner.Creature.CurrentHp;
            await CreatureCmd.Heal(Owner.Creature, amountToHeal);
        }
    }
}