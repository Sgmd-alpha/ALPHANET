using ALPHANET.Characters;
using ALPHANET.Powers;
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
public sealed class DilutedMengPoSoup : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

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
        AssertValidForTargetedPotion(target);

        if (target == null || target.Player == null) return;

        // 播放孟婆汤泼洒特效（惨淡的孟婆汤色，如苍绿色/灰白色）
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("c8d5b9"));

        // 1. 如果目标玩家处于临界状态，强制退出
        var criticalPower = target.GetPower<CriticalState>();
        if (criticalPower != null)
        {
            await PowerCmd.Remove(criticalPower);
        }

        // 2. 赋予额外回合调度能力
        // 这里赋予的目标是 target，施加者是 Owner?.Creature（扔药水的人）
        await PowerCmd.Apply<DilutedMengPoSoupPower>(choiceContext, target, 1m, Owner?.Creature, null);
    }
}