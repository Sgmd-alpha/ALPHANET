using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class MindScoop : ModCardTemplate
{
    public MindScoop() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(3m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            CardModel previewCard = ModelDb.Card<TokenParadox>();
            if (IsUpgraded)
            {
                previewCard = previewCard.ToMutable();
                CardCmd.Upgrade(previewCard);
            }
            return [ HoverTipFactory.FromCard(previewCard) ];
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 1. 造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 2. 生成“理之矛，心之盾”（若本卡已升级，则生成升级版）并加入抽牌堆
        CardModel paradoxCard = CombatState!.CreateCard<TokenParadox>(Owner!);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(paradoxCard);
        }
        CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(paradoxCard, PileType.Draw, Owner, CardPilePosition.Random));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m); // 3 -> 5
    }
}