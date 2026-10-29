using ALPHANET.Characters;
using ALPHANET.States;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Potions;

[RegisterPotion(typeof(ALPHANETPotionPool))]
public sealed class ToggleTonic : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: $"{Entry.ResPath}/images/potions/{GetType().Name}.png",
        OutlinePath: $"{Entry.ResPath}/images/potions/{GetType().Name}_outline.png"
    );

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        // 必须从 target 参数获取目标玩家，这样扔给队友时才能对队友生效
        var player = target?.Player; 
        if (player == null || player.Creature == null) return;

        // 播放药水泼洒特效（青蓝色）
        NCombatRoom.Instance?.PlaySplashVfx(player.Creature, new Color("00ffff"));

        // 1. 抽 2 张牌
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);

        // 2. 状态切换（自适应切换）
        if (player.Creature.HasPower<CriticalState>())
        {
            var criticalPower = player.Creature.GetPower<CriticalState>();
            if (criticalPower != null)
            {
                await PowerCmd.Remove(criticalPower);
            }
        }
        else
        {
            await PowerCmd.Apply<CriticalState>(choiceContext, player.Creature, 1m, player.Creature, null);
        }
    }
}