using ALPHANET.Characters;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Seal : ModCardTemplate
{
    public Seal() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 基础带有“保留”关键字
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Retain 
    ];

    // 基础伤害声明 (7点)
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(7m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        var player = Owner!;

        // 1. 造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 2. 呼出手牌选择界面
        // 严格沿用 Snap 的参数传递方式，带上防呆过滤（不可选择已有保留的牌）
        var selectedCards = await CardSelectCmd.FromHand(
            context: choiceContext, 
            player: player, 
            prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1), 
            filter: (CardModel c) => !c.Keywords.Contains(CardKeyword.Retain), 
            source: this
        );

        CardModel? cardToRetain = selectedCards.FirstOrDefault();

        // 3. 为选中的卡牌挂载保留关键字
        if (cardToRetain != null)
        {
            CardCmd.ApplyKeyword(cardToRetain, CardKeyword.Retain);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级时伤害 +3（7 -> 10）
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}