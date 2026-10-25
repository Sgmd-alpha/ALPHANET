using ALPHANET.Systems;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class GodsendPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _triggeredThisTurn = false;

    static GodsendPower()
    {
        MiracleSystem.OnMiracleTriggered += HandleMiracleTriggered;
    }

    private static async Task HandleMiracleTriggered(PlayerChoiceContext choiceContext, CardModel card, CardPlay cardPlay, int hpLost)
    {
        var creature = card?.Owner?.Creature;
        if (creature == null || !creature.IsAlive) return;

        var power = creature.GetPower<GodsendPower>();
        if (power != null)
        {
            await power.OnOwnerMiracleTriggered(choiceContext);
        }
    }

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }

    private async Task OnOwnerMiracleTriggered(PlayerChoiceContext choiceContext)
    {
        if (_triggeredThisTurn || Owner.CombatState == null || Owner.Player == null) return;

        var hittableEnemies = Owner.CombatState.HittableEnemies;
        if (hittableEnemies == null || hittableEnemies.Count == 0) return;

        _triggeredThisTurn = true;
        Flash();

        // 完美对齐官方 SerpentFormPower 的随机目标选取方式
        Creature? target = Owner.Player.RunState.Rng.CombatTargets.NextItem(hittableEnemies);
        if (target != null)
        {
            VfxCmd.PlayOnCreatureCenter(target, "vfx/vfx_attack_blunt");
            await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Unpowered, Owner);
        }
    }
}