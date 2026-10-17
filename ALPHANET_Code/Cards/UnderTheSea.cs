using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class UnderTheSea : ModCardTemplate
{
    public UnderTheSea() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 播放技能动画
        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        // 1. 获取抽牌堆
        CardPile drawPile = PileType.Draw.GetPile(player);

        // 2. 检查抽牌堆是否有牌
        if (drawPile.Cards.Count > 0)
        {
            // 在底层的卡牌列表中，索引 0 (First) 就是牌堆最底下的牌。
            CardModel? bottomCard = drawPile.Cards.LastOrDefault();

            if (bottomCard != null)
            {
                // 将其移动到手牌 (PileType.Hand)
                await CardPileCmd.Add(bottomCard, PileType.Hand); 

                // 获取这张牌的当前耗能
                int cost = bottomCard.EnergyCost.GetWithModifiers(CostModifiers.Local);

                // 如果耗能大于 0，且不是 X 费牌
                if (cost > 0 && !bottomCard.EnergyCost.CostsX)
                {
                    // 获得对应能量
                    await PlayerCmd.GainEnergy(cost, player);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}