using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Recall : ModCardTemplate
{
    // 显式声明本牌包含格挡，激活 UI 护盾预览
    public override bool GainsBlock => true;

    public Recall() : base(2, CardType.Skill, CardRarity.Common, TargetType.Self) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 1. 每次打出衰减的数值 (2)
        ModCardVars.Int("LossPerUse", 2),

        // 记录本场战斗中打出次数
        ModCardVars.Int("TimesPlayed", 0),

        // 2. 利用 ComputedBlock 动态计算当前应获得的格挡值
        ModCardVars.ComputedBlock(
            "ComputedBlock",
            static ctx =>
            {
                decimal baseBlock = ctx.BaseValue;

                // 图鉴模式防空保护：如果不在战斗中，直接返回基础格挡值
                if (!ctx.IsInCombat || ctx.CombatState is null || ctx.Player is null || ctx.Card is null)
                {
                    return baseBlock;
                }

                // 获取衰减步长 (默认 2)
                decimal lossPerUse = ctx.Card.DynamicVars.GetIntOrDefault("LossPerUse", 2);

                // 通过卡牌自身的某种计数，或者直接读取一个状态/持久化属性来计算被使用过几次
                // 这里我们假设你用了一个自定义变量或者通过 CardModel 的扩展/状态来记录打出次数，
                // 或者更标准地：直接读取卡牌上记录的“已打出次数”
                int playCount = ctx.Card.DynamicVars.GetIntOrDefault("TimesPlayed", 0);

                // 最终格挡 = 基础值 - (打出次数 * 衰减值)，最低为 0
                return Math.Max(0m, baseBlock - playCount * lossPerUse);
            },
            baseValue: 10m), // 未升级基础格挡为 10
    ];

    // 悬浮提示：向玩家展示《记忆》这张衍生牌的效果
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<TokenMemories>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 播放施法动画
        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        // 2. 获取经过动态计算后的真实格挡数值
        int finalBlock = (int)DynamicVars.EvaluateValueOrDefault("ComputedBlock");

        if (finalBlock > 0)
        {
            await CreatureCmd.GainBlock(player.Creature, finalBlock, ValueProp.Move, cardPlay);
        }

        // 3. 生成一张《记忆》衍生牌
        CardModel generatedCard = CombatState!.CreateCard<TokenMemories>(player);

        // 核心联动：如果本牌（回忆）已升级，那么生成的衍生牌（记忆）也一并升级
        if (IsUpgraded)
        {
            CardCmd.Upgrade(generatedCard);
        }

        // 4. 将其加入手牌
        await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, Owner);

        // 5. 每次成功打出后，让记录“打出次数”的计数器 +1，这样下一次在手里时 ComputedBlock 会自动扣减格挡
        DynamicVars["TimesPlayed"].UpgradeValueBy(1m);
    }

    protected override void OnUpgrade()
    {
        // 升级时，提升基础格挡值（10 -> 12）
        DynamicVars["ComputedBlock"].UpgradeValueBy(2m);
    }
}