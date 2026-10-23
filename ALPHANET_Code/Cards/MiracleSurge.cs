using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class MiracleSurge : ModCardTemplate
{
    // 标记当前是否正处于本卡的抽牌结算链条中
    private bool _isDrawingChain;
    // 记录当前待执行的额外抽牌次数
    private int _pendingExtraDraws;
    // 记录本次单抽是否确实成功抽到了牌（防止牌库抽空或手牌满时死循环）
    private bool _drewAnyThisStep;

    public MiracleSurge() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    // 悬停提示展示“奇迹”词条说明（若你的 Miracle 关键词有对应的 HoverTip 工厂方法，可在此处添加）
    // protected override IEnumerable<IHoverTip> AdditionalHoverTips => [ ... ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        int initialDraws = DynamicVars.Cards.IntValue;

        _isDrawingChain = true;
        _pendingExtraDraws = 0;

        try
        {
            // 1. 先逐张执行基础抽牌（2张 / 升级后3张）
            for (int i = 0; i < initialDraws; i++)
            {
                _drewAnyThisStep = false;
                await CardPileCmd.Draw(choiceContext, 1m, player);
                
                // 如果连 1 张都没抽上来（手牌满了或牌库彻底空了），直接终止后续抽牌
                if (!_drewAnyThisStep) break;
            }

            // 2. 结算因为抽到“奇迹”牌而触发的额外抽牌（支持连环触发：额外抽到的奇迹牌也会让 _pendingExtraDraws 继续 +1）
            int safetyCap = 30; // 安全上限，防止全奇迹空牌库无限洗牌死循环
            while (_pendingExtraDraws > 0 && safetyCap > 0)
            {
                _pendingExtraDraws--;
                safetyCap--;

                _drewAnyThisStep = false;
                await CardPileCmd.Draw(choiceContext, 1m, player);

                if (!_drewAnyThisStep)
                {
                    _pendingExtraDraws = 0;
                    break;
                }
            }
        }
        finally
        {
            _isDrawingChain = false;
            _pendingExtraDraws = 0;
        }
    }

    // 监听本卡结算期间抽上来的每一张牌
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (_isDrawingChain && card.Owner == Owner)
        {
            _drewAnyThisStep = true;

            // 判定抽到的牌是否带有“奇迹”关键词或标签
            if (IsMiracleCard(card))
            {
                _pendingExtraDraws++;
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsMiracleCard(CardModel card)
    {
        return (card.Keywords != null && card.Keywords.Contains(ALPHANETKeywords.Miracle))
            || (card.Tags != null && card.Tags.Contains(ALPHANETTags.Miracle));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m); // 2 -> 3
    }
}