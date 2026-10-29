using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class VividRecall : ModCardTemplate
{
    // 1. 声明为多人游戏专属卡牌
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public VividRecall() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        // 悬停预览直接展示升级后的“记忆+”
        HoverTipFactory.FromCard<TokenMemories>(upgrade: true),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selfCreature = Owner!.Creature;

        // 1. 自己获得 4 点格挡
        await CreatureCmd.GainBlock(selfCreature, DynamicVars.Block, cardPlay);

        // 2. 获取自己与所有存活的玩家队友（严格过滤 c.IsAlive && c.IsPlayer，排除召唤物）
        IEnumerable<Creature> targetAllies = CombatState!
            .GetTeammatesOf(selfCreature)
            .Prepend(selfCreature)
            .Where(c => c != null && c.IsAlive && c.IsPlayer && c.Player != null)
            .Distinct();

        // 3. 为自己和每位队友各生成一张“记忆+”并加入各自的手牌
        foreach (Creature ally in targetAllies)
        {
            var targetPlayer = ally.Player!;

            // 注意：卡牌的 Owner 必须绑定为目标队友 targetPlayer！
            CardModel memoryPlus = CombatState.CreateCard<TokenMemories>(targetPlayer);
            CardCmd.Upgrade(memoryPlus);

            // 将生成的“记忆+”加入该玩家的手牌
            CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
                memoryPlus,
                PileType.Hand,
                Owner
            );

            // 仅当该角色是当前客户端本地玩家时，才触发卡牌飞入手牌的 UI 预览表现
            if (LocalContext.IsMe(ally))
            {
                CardCmd.PreviewCardPileAdd(result);
            }
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}