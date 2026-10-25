using ALPHANET.Keywords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class MasterPlanPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    private const string TrackedCardsVarKey = "TrackedCards";

    // 记录被《运筹帷幄》放入抽牌堆底端、尚未被首次抽到的卡牌实例
    private readonly List<CardModel> _trackedCards = new();

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    // 1. 声明动态文本变量：用于在状态描述里实时显示被压在牌底的卡牌名字！
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar(TrackedCardsVarKey)
    ];

    // 2. 鼠标悬停在状态图标上时，直接在旁边弹出被压在牌底的那张牌的完整卡面预览！
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        _trackedCards
            .Where(c => c != null)
            .Select(c => HoverTipFactory.FromCard(c))
            .Append(HoverTipFactory.FromPower<NoDrawPower>());

    public void TrackCard(CardModel card)
    {
        if (!_trackedCards.Contains(card))
        {
            _trackedCards.Add(card);
            CardCmd.ApplyKeyword(card, ALPHANETKeywords.MasterPlanMark);
            UpdateTrackedCardsText();
        }
    }

    private void UpdateTrackedCardsText()
    {
        if (DynamicVars.TryGetValue(TrackedCardsVarKey, out var dynVar) && dynVar is StringVar strVar)
        {
            strVar.StringValue = _trackedCards.Count > 0
                ? string.Join("、", _trackedCards.Select(c => $"[gold]{c.Title}[/gold]"))
                : "";
        }
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner != Owner.Player) return;

        // 如果抽到的正是被《运筹帷幄》压在牌底的那张牌（且是此后第一次抽到）
        if (_trackedCards.Remove(card))
        {
            CardCmd.RemoveKeyword(card, ALPHANETKeywords.MasterPlanMark);
            UpdateTrackedCardsText();
            Flash();

            // 1. 施加“不能再抽牌”（精准卡住抽牌，防止把弃牌堆洗回来！）
            await PowerCmd.Apply<NoDrawPower>(choiceContext, Owner, 1m, Owner, null);

            // 2. 减少一层运筹帷幄计数，若所有追踪牌都已触发完毕则移除此状态
            if (_trackedCards.Count == 0 || Amount <= 1m)
            {
                await PowerCmd.Remove(this);
            }
            else
            {
                await PowerCmd.Decrement(this);
            }
        }
    }
}