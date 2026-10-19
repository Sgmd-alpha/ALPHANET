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
public sealed class Prospect : ModCardTemplate
{
    public Prospect() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 基础伤害声明 (8点，受力量加成)
        new DamageVar(8m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        var player = Owner!;

        // 1. 对目标敌人造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 2. 获取玩家的抽牌堆
        CardPile drawPile = PileType.Draw.GetPile(player);

        // 3. 检查抽牌堆是否有牌，防止无牌可找导致报错
        if (drawPile.Cards.Count > 0)
        {
            // 呼出选牌界面，允许且仅允许选择 1 张牌
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            var selectedCards = await CardSelectCmd.FromCombatPile(
                choiceContext, 
                drawPile, 
                player, 
                prefs
            );

            // 获取选中的那张牌
            CardModel? selectedCard = selectedCards.FirstOrDefault();

            if (selectedCard != null)
            {
                // 4. 将选中的牌放置到抽牌堆顶部 (完美复刻 CosmicIndifference 的操作)
                await CardPileCmd.Add(selectedCard, PileType.Draw, CardPilePosition.Top);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级时伤害 +4 (8 -> 12)
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}