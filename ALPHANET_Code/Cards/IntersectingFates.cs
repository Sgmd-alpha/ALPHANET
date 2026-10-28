using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class IntersectingFates : ModCardTemplate
{
    public IntersectingFates() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 声明此卡为 X 费卡
    protected override bool HasEnergyCostX => true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 获取 X 的数值（ResolveEnergyXValue 会自动计算化学物X等加成）
        int xCount = ResolveEnergyXValue();
        if (xCount <= 0)
            return;

        // 2. 获取除当前角色外的所有角色卡池
        List<CardPoolModel> otherPools = player.UnlockState.CharacterCardPools.ToList();
        if (otherPools.Count > 1)
        {
            otherPools.Remove(player.Character.CardPool);
        }

        // 3. 汇总其他角色的已解锁卡牌，并屏蔽“奥斯提攻击牌 (CardTag.OstyAttack)”
        IEnumerable<CardModel> candidateCards = otherPools
            .SelectMany(pool => pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(c => c != null && (c.Tags == null || !c.Tags.Contains(CardTag.OstyAttack)));

        // 4. 随机生成 X 张其他角色卡牌
        List<CardModel> generatedCards = CardFactory.GetForCombat(
            player,
            candidateCards,
            xCount,
            player.RunState.Rng.CombatCardGeneration
        ).ToList();

        if (generatedCards.Count == 0)
            return;

        // 5. 若本卡已升级，则将生成的卡牌全部升级，并设置本回合免费打出
        if (IsUpgraded)
        {
            CardCmd.Upgrade(generatedCards, CardPreviewStyle.None);
        }

        foreach (CardModel card in generatedCards)
        {
            card.SetToFreeThisTurn();
        }

        // 6. 将生成的卡牌加入手牌
        await CardPileCmd.AddGeneratedCardsToCombat(generatedCards, PileType.Hand, player);
    }

    protected override void OnUpgrade()
    {
        // 升级效果由 OnPlay 中的 IsUpgraded 判断处理，配合本地化 descriptionUpgraded 切换文本
    }
}