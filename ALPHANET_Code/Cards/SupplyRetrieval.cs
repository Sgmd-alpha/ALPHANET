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

        // 1. 防御性洗牌：只在绝对必要时才发起洗牌网络同步
        if (drawPile.Cards.Count < lookCount && discardPile.Cards.Count > 0)
        {
            await CardPileCmd.Shuffle(choiceContext, player);
        }

        // 2. 状态刷新：洗牌（跨越 await）后，重新获取最新鲜的抽牌堆状态
        List<CardModel> topCards = drawPile.Cards.Take(lookCount).ToList();
        if (topCards.Count == 0)
            return;

        // 3. 发起玩家选择网络握手
        var selectorPrefs = new CardSelectorPrefs(SelectionScreenPrompt, 0, 1);
        CardModel? selectedToExhaust = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            topCards,
            player,
            selectorPrefs
        )).FirstOrDefault();

        // 4. 【核心安全点】网络选择返回后，必须校验这张牌是否真的还在抽牌堆里！
        // 防止网络重传或因为其他被动机制导致这张牌已经不在原位
        if (selectedToExhaust != null && drawPile.Cards.Contains(selectedToExhaust))
        {
            await CardCmd.Exhaust(choiceContext, selectedToExhaust);
        }

        // 5. 动态计算安全的抽牌数量
        // 不再依赖修改本地列表，而是根据实际选过的数量来决定抽几张
        int drawAmount = topCards.Count - (selectedToExhaust != null ? 1 : 0);
        
        // 双重保险：确保要抽的数量不超过当前抽牌堆的真实剩余量
        drawAmount = Math.Min(drawAmount, drawPile.Cards.Count);

        if (drawAmount > 0)
        {
            await CardPileCmd.Draw(choiceContext, drawAmount, player);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m); // 3 -> 4
    }
}