using ALPHANET.Characters;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Potions;

[RegisterPotion(typeof(ALPHANETPotionPool))]
public sealed class MasterCheapIcedTea : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: $"{Entry.ResPath}/images/potions/{GetType().Name}.png",
        OutlinePath: $"{Entry.ResPath}/images/potions/{GetType().Name}_outline.png"
    );

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        AssertValidForTargetedPotion(target);

        if (Owner?.Creature == null) return;

        // 1. 获取投掷者（你）的已损生命值并除以2，向上取整
        int maxHp = Owner.Creature.MaxHp;
        int currentHp = Owner.Creature.CurrentHp;
        int missingHp = Math.Max(0, maxHp - currentHp);
        
        decimal rawDamage = missingHp / 2m;
        int damageToDeal = (int)Math.Ceiling(rawDamage);

        if (damageToDeal <= 0) return;

        // 2. 播放冰茶泼洒特效（深琥珀色/红茶色）
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("a66532"));

        // 3. 造成无能力加成的伤害
        await CreatureCmd.Damage(
            choiceContext,
            target,
            damageToDeal,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            null
        );
    }
}