using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class LifeDrain : ModCardTemplate
{
    private const string HealKey = "Heal";

    public LifeDrain() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 严禁在战斗中被随机生成
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust,
        ALPHANETKeywords.Miracle
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLoss", 12m),
        new DynamicVar(HealKey, 0m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<StatusChaos>()
    ];

    // 在战斗内根据当前指向的目标实时计算可恢复的溢出生命值
    private void UpdateDynamicVarsInCombat(Creature? target)
    {
        decimal hpLoss = DynamicVars["HpLoss"].BaseValue;
        decimal currentHeal = 0m;

        if (target != null && target.IsAlive)
        {
            currentHeal = Math.Max(0m, hpLoss - target.CurrentHp);
        }

        if (DynamicVars.TryGetValue(HealKey, out var healVar))
        {
            healVar.BaseValue = currentHeal;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var player = Owner!;
        var target = cardPlay.Target;

        UpdateDynamicVarsInCombat(target);

        // 1. 在手牌中加入一张《混沌》
        CardModel chaos = CombatState!.CreateCard<StatusChaos>(player);
        await CardPileCmd.AddGeneratedCardToCombat(chaos, PileType.Hand, player);

        // 2. 计算实际溢出量
        decimal hpLoss = DynamicVars["HpLoss"].BaseValue;
        decimal overflow = Math.Max(0m, hpLoss - target.CurrentHp);

        // 3. 使敌人失去生命
        await CreatureCmd.Damage(
            choiceContext,
            target,
            hpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            this,
            cardPlay
        );

        // 4. 溢出量治疗自身
        if (overflow > 0)
        {
            await CreatureCmd.Heal(player.Creature, overflow);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HpLoss"].UpgradeValueBy(4m); // 12 -> 16
    }
}