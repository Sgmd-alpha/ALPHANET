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

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    public override RelicRarity Rarity => RelicRarity.Starter;

    // ==========================================
    // 展示【未来】的卡牌预览，并附带“虚无”、“消耗”的解释词条
    // ==========================================
    protected override IEnumerable<IHoverTip> AdditionalHoverTips 
    {
        get
        {
            List<IHoverTip> list =
            [
                // 使用 AddRange 将【未来】卡牌预览和它的关键字（虚无、消耗）一起塞入列表
                .. HoverTipFactory.FromCardWithCardHoverTips<TokenFuture>(),
            ];
            
            return list;
        }
    }

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
            
            // 1. 对齐官方：创建一个 CardModel 类型的列表
            List<CardModel> list =
            [
                // 2. 将创建好的【未来】实体塞进这个列表里
                Owner.Creature.CombatState!.CreateCard<TokenFuture>(Owner),
            ];
            
            // 3. 使用官方复数形式的添加命令，把整个列表的牌移入手牌
            await CardPileCmd.AddGeneratedCardsToCombat(list, PileType.Hand, Owner);
        }
    }

    public override Task AfterAutoPostPlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        IsPlayerMainPhase = false; 
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        IsPlayerMainPhase = true;
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
        Flash();
        _usedThisCombat = true;

        decimal healAmount = 1m - creature.CurrentHp;
        if (healAmount > 0)
        {
            await CreatureCmd.Heal(creature, healAmount);
        }

        await CriticalState.Enter(null!, creature, null, null, true);
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