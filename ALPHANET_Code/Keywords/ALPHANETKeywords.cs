using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace ALPHANET.Keywords;

[RegisterOwnedCardKeyword(nameof(Miracle), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(MasterPlanMark), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.AfterCardDescription)]
public class ALPHANETKeywords
{
    public static readonly CardKeyword Miracle = 
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Miracle)).GetModCardKeyword();

    public static readonly CardKeyword MasterPlanMark = 
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(MasterPlanMark)).GetModCardKeyword();
}