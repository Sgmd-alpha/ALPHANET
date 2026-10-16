using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace ALPHANET.Characters;

public sealed class ALPHANETCardPool : TypeListCardPoolModel
{
    private static readonly Material? _poolFrameMaterial = MaterialUtils.CreateHsvShaderMaterial(0.75f, 0.7f, 0.7f);

    public override string Title => "ALPHANET";
    public override string EnergyColorName => "ALPHANET";

    public override string? BigEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_big.png";

    public override string? TextEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor =>
        Color.FromHsv(265f / 360f, 0.9f, 0.4f);

    public override Color EnergyOutlineColor =>
        Color.FromHsv(260f / 360f, 0.8f, 0.7f);

    public override Material? PoolFrameMaterial => _poolFrameMaterial;

    public override bool IsColorless => false;
}