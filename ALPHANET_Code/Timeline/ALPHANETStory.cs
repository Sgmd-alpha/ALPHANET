using ALPHANET.Cards;
using ALPHANET.Characters;
using ALPHANET.Potions;
using ALPHANET.Relics;
using MegaCrit.Sts2.Core.Timeline;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Timeline.Scaffolding;

namespace ALPHANET.Timeline;

[RegisterStory]
public class ALPHANETStory : ModStoryTemplate
{
    protected override string StoryKey => "alphanet";

    // 辅助生成每一幕专属的 ID 键
    internal static string ActEpochKey(int actNum) => ModContentRegistry.GetFixedPublicEntry(Entry.ModId, typeof(ALPHANETCharacter)) + $"_{actNum + 1}_EPOCH";
}

// ==========================================
// 第 7 组：初始角色解锁（铁甲战士完成一局游戏）
// ==========================================
[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 0)]
[AutoTimelineSlotBeforeColumn(EpochEra.Seeds0)]
[RequireAllCardsInPool(typeof(ALPHANETCardPool))]
public class ALPHANETCharacterEpoch : CharacterUnlockEpochTemplate<ALPHANETCharacter>
{
    public override string Id => "ALPHANET_CHARACTER_EPOCH";

    public override EpochAssetProfile AssetProfile => new(
        PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_character.png",
        BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_character_big.png"
    );

    protected override IEnumerable<Type> ExpansionEpochTypes =>
    [
        typeof(ALPHANETAct1Epoch),
        typeof(ALPHANETAct2Epoch),
        typeof(ALPHANETAct3Epoch),
        typeof(ALPHANETEliteEpoch),
        typeof(ALPHANETBossEpoch),
        typeof(ALPHANETAscensionOneEpoch)
    ];
}

// ==========================================
// 第 1 组：通关第一阶段 (打败第一幕 Boss)
// ==========================================
[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 1)]
[AutoTimelineSlot(EpochEra.Blight1)]
[RegisterEpochCards(typeof(RepulsionField), typeof(ProperlyHandled), typeof(InfiniteCognition))]
public sealed class ALPHANETAct1Epoch : PackDeclaredCardUnlockEpochTemplate
{
    public override string Id => ALPHANETStory.ActEpochKey(1);
    public override EpochAssetProfile AssetProfile => new(PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act1.png", BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act1_big.png");
}

// ==========================================
// 第 2 组：通关第二阶段 (打败第二幕 Boss)
// ==========================================
[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 2)]
[AutoTimelineSlot(EpochEra.Peace0)]
[RegisterEpochCards(typeof(SilkPouch), typeof(Relics.Glasswork), typeof(ArchitectureEnhancer))]
public sealed class ALPHANETAct2Epoch : PackDeclaredCardUnlockEpochTemplate
{
    public override string Id => ALPHANETStory.ActEpochKey(2);
    public override EpochAssetProfile AssetProfile => new(PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act2.png", BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act2_big.png");
}

// ==========================================
// 第 3 组：通关第三阶段 (打败第三幕 Boss)
// ==========================================
[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 3)]
[AutoTimelineSlot(EpochEra.Seeds2)]
[RegisterEpochCards(typeof(ToggleTonic), typeof(MasterCheapIcedTea), typeof(DilutedMengPoSoup))]
public sealed class ALPHANETAct3Epoch : PackDeclaredCardUnlockEpochTemplate
{
    public override string Id => ALPHANETStory.ActEpochKey(3);
    public override EpochAssetProfile AssetProfile => new(PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act3.png", BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act3_big.png");
}

// ==========================================
// 第 4 组：击杀 15 个精英
// ==========================================
[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 4)]
[AutoTimelineSlot(EpochEra.Prehistoria2)]
[RegisterEpochCards(typeof(Seal), typeof(InstantAffinity), typeof(TheGreatFormless))]
public sealed class ALPHANETEliteEpoch : PackDeclaredCardUnlockEpochTemplate
{
    public override string Id => "ALPHANET_ELITE_MILESTONE_EPOCH";
    public override EpochAssetProfile AssetProfile => new(PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_elite.png", BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_elite_big.png");
}

// ==========================================
// 第 5 组：击杀 15 个 Boss
// ==========================================
[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 5)]
[AutoTimelineSlot(EpochEra.Flourish0)]
[RegisterEpochCards(typeof(MemorialPhotos), typeof(SemusMemoirs), typeof(AstronomicalTelescope))]
public sealed class ALPHANETBossEpoch : PackDeclaredCardUnlockEpochTemplate
{
    public override string Id => "ALPHANET_BOSS_MILESTONE_EPOCH";
    public override EpochAssetProfile AssetProfile => new(PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_boss.png", BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_boss_big.png");
}

// ==========================================
// 第 6 组：通关进阶 1
// ==========================================
[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 6)]
[AutoTimelineSlot(EpochEra.Invitation5)]
[RegisterEpochCards(typeof(Gambit), typeof(ChaosBounty), typeof(CascadeOfMiracles))]
public sealed class ALPHANETAscensionOneEpoch : PackDeclaredCardUnlockEpochTemplate
{
    public override string Id => "ALPHANET_ASCENSION_ONE_EPOCH";
    public override EpochAssetProfile AssetProfile => new(PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_asc1.png", BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_asc1_big.png");
}