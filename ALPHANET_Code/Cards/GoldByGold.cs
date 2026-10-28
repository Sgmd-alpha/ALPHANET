using ALPHANET.Characters;
using ALPHANET.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class GoldByGold : ModCardTemplate
{
    public GoldByGold() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(18m, ValueProp.Move),
        new GoldVar(15),
        new PowerVar<GoldByGoldPower>(3m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GoldByGoldPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var target = cardPlay.Target;
        var creature = Owner!.Creature;
        decimal turns = DynamicVars["GoldByGoldPower"].BaseValue; // 3 回合
        int gold = DynamicVars.Gold.IntValue;                      // 15 金币

        // 1. 施加/叠加状态
        var existingPower = target.GetPower<GoldByGoldPower>();
        if (existingPower != null)
        {
            await existingPower.StackGoldAndRefreshTurns(choiceContext, gold, turns, creature, this);
        }
        else
        {
            await PowerCmd.Apply<GoldByGoldPower>(
                choiceContext,
                target,
                turns,
                creature,
                this
            );

            var newlyApplied = target.GetPower<GoldByGoldPower>();
            newlyApplied?.SetInitialGold(gold);
        }

        // 2. 规范的攻击结算（修复特效引起的 NullReferenceException）
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m); // 18 -> 22
    }
}