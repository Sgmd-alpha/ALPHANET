using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ALPHANET.Tags;

// 注意：类名同样不能带有 static
[RegisterOwnedCardTag(nameof(Miracle))]
public class ALPHANETTags
{
    // 获取引擎识别的 CardTag 对象，供底层代码逻辑检索使用
    public static readonly CardTag Miracle = ModContentRegistry.GetQualifiedCardTagId(Entry.ModId, nameof(Miracle)).GetModCardTag();
}