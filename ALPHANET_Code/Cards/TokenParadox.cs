using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class TokenParadox : ModCardTemplate
{
    public TokenParadox() : base(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        CardKeyword.Exhaust
    ];

    // ==========================================
    // 动态变量声明：12 伤害 / 6 格挡 (升级后 16 / 8)
    // ==========================================
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(12m, ValueProp.Move),
        new BlockVar(6m, ValueProp.Move)
    ];

    // ==========================================
    // 核心判定：是否为本回合打出的第一张牌
    // ==========================================
    private bool IsFirstCardPlayedThisTurn
    {
        get
        {
            if (Owner == null || CombatState == null || !CombatManager.Instance.IsInProgress)
                return false;

            // 检查本回合历史记录中，该玩家是否还没有完成打出过任何其他卡牌
            return !CombatManager.Instance.History.CardPlaysFinished.Any(e =>
                e.HappenedThisTurn(CombatState) &&
                e.CardPlay.Card.Owner == Owner &&
                e.CardPlay.Card != this);
        }
    }

    // 满足“本回合第一张牌”条件时，边框发金光提示
    protected override bool ShouldGlowGoldInternal => IsFirstCardPlayedThisTurn;

    // ==========================================
    // 伤害与格挡 +50% 乘算修正（自动同步卡面预览与实际结算）
    // ==========================================
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 仅对这张牌自身造成的伤害生效，且必须位于手牌或正在打出中
        if (cardSource == this && IsFirstCardPlayedThisTurn)
        {
            return 1.5m;
        }
        return 1m;
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 仅对这张牌自身获得的格挡生效
        if (cardSource == this && IsFirstCardPlayedThisTurn)
        {
            return 1.5m;
        }
        return 1m;
    }

    // ==========================================
    // 打出效果
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 1. 造成伤害（若为本回合第一张牌，ModifyDamageMultiplicative 会自动将其乘以 1.5）
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 2. 获得格挡（若为本回合第一张牌，ModifyBlockMultiplicative 会自动将其乘以 1.5）
        await CreatureCmd.GainBlock(Owner!.Creature, DynamicVars.Block, cardPlay);
    }

    // ==========================================
    // 升级逻辑
    // ==========================================
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m); // 12 -> 16
        DynamicVars.Block.UpgradeValueBy(2m);  // 6 -> 8
    }
}