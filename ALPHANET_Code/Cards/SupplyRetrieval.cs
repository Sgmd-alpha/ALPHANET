using ALPHANET.Characters;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class SupplyRetrieval : ModCardTemplate
{
    public SupplyRetrieval() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var drawPile = PileType.Draw.GetPile(player);
        var discardPile = PileType.Discard.GetPile(player);

        int lookCount = DynamicVars.Cards.IntValue;

        // 1. 先取出当前抽牌堆顶端已有的牌（0 ~ lookCount 张）
        List<CardModel> topCards = drawPile.Cards.Take(lookCount).ToList();

        // 2. 如果抽牌堆为空（或不足 3/4 张），且弃牌堆里有牌，则触发洗牌并补齐剩余视野！
        // （如果你希望【只有当抽牌堆彻底为 0 张时】才洗牌，只需把下面的 topCards.Count < lookCount 改为 topCards.Count == 0 即可）
        if (topCards.Count < lookCount && discardPile.Cards.Count > 0)
        {
            // 调用官方洗牌指令：将弃牌堆洗入抽牌堆
            await CardPileCmd.Shuffle(choiceContext, player);

            // 洗牌完成后，从抽牌堆中按顺序补齐还差的那几张牌
            int needed = lookCount - topCards.Count;
            var newlyShuffledTopCards = drawPile.Cards
                .Where(c => !topCards.Contains(c))
                .Take(needed);

            topCards.AddRange(newlyShuffledTopCards);
        }

        // 如果洗完牌后依然没有任何牌（抽牌堆和弃牌堆全都空了），直接结束
        if (topCards.Count == 0)
            return;

        // 3. 打开选择窗口：直接传入排好序的 topCards 列表，从左到右依次为第 1、2、3、4 张
        var selectorPrefs = new CardSelectorPrefs(SelectionScreenPrompt, 0, 1);
        CardModel? selectedToExhaust = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            topCards,
            player,
            selectorPrefs
        )).FirstOrDefault();

        // 4. 如果玩家选择了其中 1 张牌，将其消耗并从待抽取列表中移除
        if (selectedToExhaust != null)
        {
            topCards.Remove(selectedToExhaust);
            await CardCmd.Exhaust(choiceContext, selectedToExhaust);
        }

        // 5. 尝试将剩余的顶端卡牌抽至手牌
        int remainingToDraw = topCards.Count;
        if (remainingToDraw > 0)
        {
            await CardPileCmd.Draw(choiceContext, remainingToDraw, player);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m); // 3 -> 4
    }
}