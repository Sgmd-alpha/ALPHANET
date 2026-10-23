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
public class MiracleResidue : ModRelicTemplate
{
    private bool _usedThisCombat = false;
    public static bool IsPlayerMainPhase = false;
    // 在 MiracleResidue 类中添加这三个虚属性，供子类《奇迹碎片》重写：
    protected virtual bool UpgradeFutureCard => false;
    protected virtual decimal PostCombatHealThreshold => 10m;
    protected virtual int PostCombatGoldReward => 0;

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

            // 1. 先创建出“未来”卡牌实例
            CardModel futureCard = Owner.Creature.CombatState!.CreateCard<TokenFuture>(Owner);

            // 2. 如果是《奇迹碎片》（UpgradeFutureCard 为 true），则在加入手牌前将其升级为“未来+”！
            if (UpgradeFutureCard)
            {
                CardCmd.Upgrade(futureCard);
            }

            // 3. 将卡牌加入手牌
            List<CardModel> list = [ futureCard ];
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

    public override async Task AfterCombatVictory(CombatRoom room)
{
    if (Owner == null || Owner.Creature.IsDead) return;

    bool triggered = false;

    // 1. 如果有战后金币奖励（奇迹碎片：20金币）
    if (PostCombatGoldReward > 0)
    {
        triggered = true;
        await PlayerCmd.GainGold(PostCombatGoldReward, Owner);
    }

    // 2. 回复生命值到阈值（10点 / 20点），若最大生命值低于阈值则回满
    decimal targetHp = Math.Min(PostCombatHealThreshold, Owner.Creature.MaxHp);
    if (Owner.Creature.CurrentHp < targetHp)
    {
        triggered = true;
        await CreatureCmd.Heal(Owner.Creature, targetHp - Owner.Creature.CurrentHp);
    }

    if (triggered)
    {
        Flash();
    }
}
}