using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class ChaosConversion : ModCardTemplate
{
    public ChaosConversion() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        ALPHANETKeywords.Miracle
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<StatusChaos>(),
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;

        // 1. 获得 6 (升级后 8) 点格挡
        await CreatureCmd.GainBlock(creature, DynamicVars.Block, cardPlay);

        // 2. 获取手牌中所有可变化的“混沌”
        List<CardModel> chaosCards = PileType.Hand.GetPile(player).Cards
            .Where(c => c != null && c.IsTransformable && c is StatusChaos)
            .ToList();

        if (chaosCards.Count == 0)
            return;

        // 3. 获取角色卡池中的所有已解锁【普通 (Common)】卡牌作为随机池
        IEnumerable<CardModel> commonPool = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => c.Rarity == CardRarity.Common);

        // 4. 生成对应数量的随机普通牌
        List<CardModel> generatedCards = CardFactory.GetForCombat(
            player,
            commonPool,
            chaosCards.Count,
            player.RunState.Rng.CombatCardGeneration
        ).ToList();

        // 5. 将手牌中的“混沌”逐一变化为生成的随机普通牌（附加升级与虚无）
        for (int i = 0; i < chaosCards.Count && i < generatedCards.Count; i++)
        {
            CardModel targetChaos = chaosCards[i];
            CardModel replacement = generatedCards[i];

            if (IsUpgraded)
            {
                CardCmd.Upgrade(replacement);
            }

            CardCmd.ApplyKeyword(replacement, CardKeyword.Ethereal);

            await CardCmd.Transform(targetChaos, replacement);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m); // 6 -> 8
    }
}

