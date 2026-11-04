using ALPHANET.Cards;
using ALPHANET.Characters;
using ALPHANET.Potions;
using ALPHANET.Relics;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.Timeline;
using MegaCrit.Sts2.Core.Timeline;

using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Timeline.Scaffolding;

namespace ALPHANET.Timeline;

// ==========================================
// Story
// ==========================================

[RegisterStory]
public class ALPHANETStory : ModStoryTemplate
{
    protected override string StoryKey => "alphanet";

    // 与原版 ObtainCharUnlockEpoch 保持一致：
    //
    // Act 1 -> CharacterEntry + "2_EPOCH"
    // Act 2 -> CharacterEntry + "3_EPOCH"
    // Act 3 -> CharacterEntry + "4_EPOCH"
    //
    // 注意：这里不额外添加下划线。

    internal static string ActEpochKey(int actNum)
    {
        string characterEntry = ModContentRegistry.GetFixedPublicEntry(
            Entry.ModId,
            typeof(ALPHANETCharacter)
        ).ToUpperInvariant();

        return characterEntry + $"{actNum + 1}_EPOCH";
    }
}

// ==========================================
// 角色解锁：使用铁甲战士完成一局
// ==========================================

[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 0)]
[AutoTimelineSlotBeforeColumn(EpochEra.Seeds0)]
public class ALPHANETCharacterEpoch
    : CharacterUnlockEpochTemplate<ALPHANETCharacter>
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
// Act 1：原版章节 Boss 解锁
// 奖励：三张卡牌
// ==========================================

[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 1)]
[AutoTimelineSlot(EpochEra.Blight1)]
public sealed class ALPHANETAct1Epoch : ModEpochTemplate
{
    public override string Id => ALPHANETStory.ActEpochKey(1);

    public override EpochAssetProfile AssetProfile => new(
        PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act1.png",
        BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act1_big.png"
    );

    public static List<CardModel> Cards =>
    [
        ModelDb.Card<RepulsionField>(),
        ModelDb.Card<ProperlyHandled>(),
        ModelDb.Card<InfiniteCognition>()
    ];

    public override string UnlockText =>
        CreateCardUnlockText(Cards);

    public override void QueueUnlocks()
    {
        NTimelineScreen.Instance.QueueCardUnlock(Cards);
    }
}

// ==========================================
// Act 2：原版章节 Boss 解锁
// 奖励：三个遗物
// ==========================================

[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 2)]
[AutoTimelineSlot(EpochEra.Peace0)]
public sealed class ALPHANETAct2Epoch : ModEpochTemplate
{
    public override string Id => ALPHANETStory.ActEpochKey(2);

    public override EpochAssetProfile AssetProfile => new(
        PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act2.png",
        BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act2_big.png"
    );

    public static List<RelicModel> Relics =>
    [
        ModelDb.Relic<SilkPouch>(),
        ModelDb.Relic<Relics.Glasswork>(),
        ModelDb.Relic<ArchitectureEnhancer>()
    ];

    public override string UnlockText =>
        CreateRelicUnlockText(Relics);

    public override void QueueUnlocks()
    {
        NTimelineScreen.Instance.QueueRelicUnlock(Relics);
    }
}

// ==========================================
// Act 3：原版章节 Boss 解锁
// 奖励：三瓶药水
// ==========================================

[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 3)]
[AutoTimelineSlot(EpochEra.Seeds2)]
public sealed class ALPHANETAct3Epoch : ModEpochTemplate
{
    public override string Id => ALPHANETStory.ActEpochKey(3);

    public override EpochAssetProfile AssetProfile => new(
        PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act3.png",
        BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_act3_big.png"
    );

    public static List<PotionModel> Potions =>
    [
        ModelDb.Potion<ToggleTonic>(),
        ModelDb.Potion<MasterCheapIcedTea>(),
        ModelDb.Potion<DilutedMengPoSoup>()
    ];

    public override string UnlockText =>
        CreatePotionUnlockText(Potions);

    public override void QueueUnlocks()
    {
        NTimelineScreen.Instance.QueuePotionUnlock(Potions);
    }
}

// ==========================================
// 精英里程碑：击败 15 个精英
// 奖励：三张卡牌
// ==========================================

[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 4)]
[AutoTimelineSlot(EpochEra.Prehistoria2)]
public sealed class ALPHANETEliteEpoch : ModEpochTemplate
{
    public override string Id => "ALPHANET_ELITE_MILESTONE_EPOCH";

    public override EpochAssetProfile AssetProfile => new(
        PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_elite.png",
        BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_elite_big.png"
    );

    public static List<CardModel> Cards =>
    [
        ModelDb.Card<Seal>(),
        ModelDb.Card<InstantAffinity>(),
        ModelDb.Card<TheGreatFormless>()
    ];

    public override string UnlockText =>
        CreateCardUnlockText(Cards);

    public override void QueueUnlocks()
    {
        NTimelineScreen.Instance.QueueCardUnlock(Cards);
    }
}

// ==========================================
// Boss 里程碑：击败 15 个 Boss
// 奖励：三个遗物
// ==========================================

[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 5)]
[AutoTimelineSlot(EpochEra.Flourish0)]
public sealed class ALPHANETBossEpoch : ModEpochTemplate
{
    public override string Id => "ALPHANET_BOSS_MILESTONE_EPOCH";

    public override EpochAssetProfile AssetProfile => new(
        PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_boss.png",
        BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_boss_big.png"
    );

    public static List<RelicModel> Relics =>
    [
        ModelDb.Relic<MemorialPhotos>(),
        ModelDb.Relic<SemusMemoirs>(),
        ModelDb.Relic<AstronomicalTelescope>()
    ];

    public override string UnlockText =>
        CreateRelicUnlockText(Relics);

    public override void QueueUnlocks()
    {
        NTimelineScreen.Instance.QueueRelicUnlock(Relics);
    }
}

// ==========================================
// 进阶 1：使用诺奥瑟完成进阶 1
// 奖励：三张卡牌
// ==========================================

[RegisterEpoch]
[RegisterStoryEpoch(typeof(ALPHANETStory), Order = 6)]
[AutoTimelineSlot(EpochEra.Invitation5)]
public sealed class ALPHANETAscensionOneEpoch : ModEpochTemplate
{
    public override string Id => "ALPHANET_ASCENSION_ONE_EPOCH";

    public override EpochAssetProfile AssetProfile => new(
        PackedPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_asc1.png",
        BigPortraitPath: $"{Entry.ResPath}/images/timeline/epoch_asc1_big.png"
    );

    public static List<CardModel> Cards =>
    [
        ModelDb.Card<Gambit>(),
        ModelDb.Card<ChaosBounty>(),
        ModelDb.Card<CascadeOfMiracles>()
    ];

    public override string UnlockText =>
        CreateCardUnlockText(Cards);

    public override void QueueUnlocks()
    {
        NTimelineScreen.Instance.QueueCardUnlock(Cards);
    }
}

// ==========================================
// 集中注册：解锁条件与内容绑定
// ==========================================
public static class ALPHANETTimelineUnlocks
{
    public static void Register()
    {
        RitsuLibFramework.CreateContentPack(Entry.ModId)

            // ----------------------------------
            // 角色解锁
            // 使用铁甲战士完成一局
            // ----------------------------------

            .UnlockCharacterAfterRunAs<Ironclad, ALPHANETCharacterEpoch>()

            // ----------------------------------
            // 精英里程碑：击败 15 个精英
            // ----------------------------------

            .UnlockEpochAfterEliteVictories<ALPHANETCharacter, ALPHANETEliteEpoch>(15)

            // ----------------------------------
            // Boss 里程碑：击败 15 个 Boss
            // ----------------------------------

            .UnlockEpochAfterBossVictories<ALPHANETCharacter, ALPHANETBossEpoch>(15)

            // ----------------------------------
            // 进阶 1 通关
            // ----------------------------------

            .UnlockEpochAfterAscensionOneWin<ALPHANETCharacter, ALPHANETAscensionOneEpoch>()

            // ----------------------------------
            // Act 1：卡牌解锁
            // ----------------------------------

            .RequireEpoch<RepulsionField, ALPHANETAct1Epoch>()
            .RequireEpoch<ProperlyHandled, ALPHANETAct1Epoch>()
            .RequireEpoch<InfiniteCognition, ALPHANETAct1Epoch>()

            // ----------------------------------
            // Act 2：遗物解锁
            // ----------------------------------

            .RequireEpoch<SilkPouch, ALPHANETAct2Epoch>()
            .RequireEpoch<Relics.Glasswork, ALPHANETAct2Epoch>()
            .RequireEpoch<ArchitectureEnhancer, ALPHANETAct2Epoch>()

            // ----------------------------------
            // Act 3：药水解锁
            // ----------------------------------

            .RequireEpoch<ToggleTonic, ALPHANETAct3Epoch>()
            .RequireEpoch<MasterCheapIcedTea, ALPHANETAct3Epoch>()
            .RequireEpoch<DilutedMengPoSoup, ALPHANETAct3Epoch>()

            // ----------------------------------
            // 精英里程碑：卡牌解锁
            // ----------------------------------

            .RequireEpoch<Seal, ALPHANETEliteEpoch>()
            .RequireEpoch<InstantAffinity, ALPHANETEliteEpoch>()
            .RequireEpoch<TheGreatFormless, ALPHANETEliteEpoch>()

            // ----------------------------------
            // Boss 里程碑：遗物解锁
            // ----------------------------------

            .RequireEpoch<MemorialPhotos, ALPHANETBossEpoch>()
            .RequireEpoch<SemusMemoirs, ALPHANETBossEpoch>()
            .RequireEpoch<AstronomicalTelescope, ALPHANETBossEpoch>()

            // ----------------------------------
            // 进阶 1：卡牌解锁
            // ----------------------------------

            .RequireEpoch<Gambit, ALPHANETAscensionOneEpoch>()
            .RequireEpoch<ChaosBounty, ALPHANETAscensionOneEpoch>()
            .RequireEpoch<CascadeOfMiracles, ALPHANETAscensionOneEpoch>()

            .Apply();
    }
}
