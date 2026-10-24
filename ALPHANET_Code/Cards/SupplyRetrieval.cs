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

        // 1. 获取抽牌堆顶端的 3 (升级后 4) 张牌
        int lookCount = DynamicVars.Cards.IntValue;
        List<CardModel> topCards = drawPile.Cards.Take(lookCount).ToList();

        // 如果当前抽牌堆是空的，直接结束
        if (topCards.Count == 0)
            return;

        // 2. 打开选择窗口：允许从这几张顶端牌中选择 0 到 1 张进行消耗
        var selectorPrefs = new CardSelectorPrefs(SelectionScreenPrompt, 0, 1);
        CardModel? selectedToExhaust = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            drawPile,
            player,
            selectorPrefs,
            c => topCards.Contains(c)
        )).FirstOrDefault();

        // 3. 如果玩家选择了其中 1 张牌，将其消耗并从待抽取列表中移除
        if (selectedToExhaust != null)
        {
            topCards.Remove(selectedToExhaust);
            await CardCmd.Exhaust(choiceContext, selectedToExhaust);
        }

        // 4. 尝试将剩余的顶端卡牌抽至手牌（自动处理手牌上限及抽牌触发器）
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