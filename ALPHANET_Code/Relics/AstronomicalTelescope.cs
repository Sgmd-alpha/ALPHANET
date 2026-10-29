using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Relics;

[RegisterRelic(typeof(ALPHANETRelicPool))]
public sealed class AstronomicalTelescope : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        // 确保是当前玩家，且仅在战斗的第 1 回合触发
        if (player != Owner || Owner.PlayerCombatState == null || Owner.PlayerCombatState.TurnNumber > 1)
        {
            return;
        }

        // 获取抽牌堆的所有卡牌
        IReadOnlyList<CardModel> drawPile = PileType.Draw.GetPile(player).Cards;
        
        // 筛选出带有“保留”词条的卡牌
        List<CardModel> retainCards = drawPile
            .Where(c => c != null && c.Keywords != null && c.Keywords.Contains(CardKeyword.Retain))
            .ToList();

        if (retainCards.Count > 0)
        {
            Flash();
            
            // 最多抽取 2 张，如果抽牌堆中只有 1 张保留牌则只抽 1 张
            int cardsToPull = Math.Min(2, retainCards.Count);
            
            for (int i = 0; i < cardsToPull; i++)
            {
                // 使用官方的战斗随机数生成器抽取一张
                CardModel? selectedCard = player.RunState.Rng.CombatCardSelection.NextItem(retainCards);
                if (selectedCard is null)
                {
                    continue;
                }
                
                // 从候选池中移除该卡，防止第二次循环抽到相同的引用
                retainCards.Remove(selectedCard);
                
                // 将选中的卡牌从抽牌堆移动到手牌
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }
        }
    }
}