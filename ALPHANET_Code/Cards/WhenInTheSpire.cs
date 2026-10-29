using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
public sealed class WhenInTheSpire : ModCardTemplate
{
    // 声明为多人游戏专属卡牌
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public WhenInTheSpire() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selfPlayer = Owner!;
        var selfCreature = selfPlayer.Creature;

        // 1. 生成 3 张不重复的随机无色牌供当前玩家选择
        List<CardModel> options = CardFactory.GetDistinctForCombat(
            selfPlayer,
            ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(
                selfPlayer.UnlockState,
                selfPlayer.RunState.CardMultiplayerConstraint
            ),
            3,
            selfPlayer.RunState.Rng.CombatCardGeneration
        ).ToList();

        if (options.Count == 0)
            return;

        // 如果本牌已升级，将 3 张候选无色牌全部预先升级
        if (IsUpgraded)
        {
            CardCmd.Upgrade(options, CardPreviewStyle.HorizontalLayout);
        }

        // 2. 弹出三选一界面（与官方 Quasar 一致）
        CardModel? selectedCard = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            options,
            selfPlayer,
            canSkip: true
        );

        if (selectedCard == null)
            return;

        // 3. 获取所有存活的玩家单位（自己 + 所有队友，过滤召唤物）
        IEnumerable<Creature> targetAllies = CombatState!
            .GetTeammatesOf(selfCreature)
            .Prepend(selfCreature)
            .Where(c => c != null && c.IsAlive && c.IsPlayer && c.Player != null)
            .Distinct();

        // 4. 为每位玩家发放这张无色牌（附加虚无 + 本回合免费打出）
        foreach (Creature ally in targetAllies)
        {
            var targetPlayer = ally.Player!;
            CardModel cardToGive;

            if (targetPlayer == selfPlayer)
            {
                // 自己直接使用选中的实例
                cardToGive = selectedCard;
            }
            else
            {
                // 队友必须基于原型创建属于 targetPlayer 的独立实例！
                CardModel canonical = ModelDb.GetById<CardModel>(selectedCard.Id);
                cardToGive = CombatState.CreateCard(canonical, targetPlayer);

                if (selectedCard.IsUpgraded)
                {
                    CardCmd.Upgrade(cardToGive);
                }
            }

            // A. 附加【虚无】词条
            if (cardToGive.Keywords == null || !cardToGive.Keywords.Contains(CardKeyword.Ethereal))
            {
                cardToGive.AddKeyword(CardKeyword.Ethereal);
            }

            // B. 这张牌在本回合可以免费打出
            if (cardToGive.EnergyCost != null && cardToGive.EnergyCost.Canonical >= 0 && !cardToGive.EnergyCost.CostsX)
            {
                cardToGive.SetToFreeThisTurn();
            }

            // C. 加入对应玩家的手牌，并仅为本地玩家播放飞入预览动画
            CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
                cardToGive,
                PileType.Hand,
                selfPlayer
            );

            if (LocalContext.IsMe(ally))
            {
                CardCmd.PreviewCardPileAdd(result);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级逻辑在 OnPlay 中通过 IsUpgraded 判断并将候选无色牌升级，描述通过 descriptionUpgraded 切换
    }
}