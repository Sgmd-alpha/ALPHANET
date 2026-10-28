using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class TheGreatFormless : ModCardTemplate
{
    public TheGreatFormless() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<StatusChaos>()
    ];

    // 判定手牌是否带有“保留”或“奇迹”
    private static bool HasRetainOrMiracle(CardModel? card)
    {
        if (card == null) return false;

        bool hasRetain = card.Keywords != null && card.Keywords.Contains(CardKeyword.Retain);
        bool hasMiracleKeyword = card.Keywords != null && card.Keywords.Contains(ALPHANETKeywords.Miracle);
        bool hasMiracleTag = card.Tags != null && card.Tags.Contains(ALPHANETTags.Miracle);

        return hasRetain || hasMiracleKeyword || hasMiracleTag;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        var handPile = PileType.Hand.GetPile(player);

        // 1. 获取所有符合条件的牌，建立快照并消耗
        List<CardModel> targetCards = handPile.Cards.Where(HasRetainOrMiracle).ToList();
        int exhaustCount = targetCards.Count;

        foreach (CardModel item in targetCards)
        {
            await CardCmd.Exhaust(choiceContext, item);
        }

        // 2. 生成等量的随机无色牌
        if (exhaustCount > 0)
        {
            var colorlessPool = ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint);
            
            // 使用 GetForCombat 允许生成重复牌（不同于 GetDistinctForCombat）
            List<CardModel> colorlessCards = CardFactory.GetForCombat(
                player,
                colorlessPool,
                exhaustCount,
                player.RunState.Rng.CombatCardGeneration
            ).ToList();

            // 如果本牌已升级，将生成的牌全部升级
            if (IsUpgraded)
            {
                CardCmd.Upgrade(colorlessCards, CardPreviewStyle.None);
            }

            await CardPileCmd.AddGeneratedCardsToCombat(colorlessCards, PileType.Hand, player);
        }

        // 3. 将一张《混沌》添加到手牌
        CardModel chaos = CombatState!.CreateCard<StatusChaos>(player);
        await CardPileCmd.AddGeneratedCardToCombat(chaos, PileType.Hand, player);
    }

    protected override void OnUpgrade()
    {
        // 升级逻辑已经在 OnPlay 中的 IsUpgraded 判断里实现了，不需要操作任何变量
    }
}