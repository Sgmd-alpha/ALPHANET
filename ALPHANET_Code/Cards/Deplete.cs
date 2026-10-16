using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Deplete : ModCardTemplate
{
    private const int BaseEnergyCost = 0; // 0费卡
    private const CardType CardKind = CardType.Attack; // 攻击牌
    private const CardRarity CardRarityValue = CardRarity.Common; // 普通
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8, ValueProp.Move) // 基础伤害8
    ];

    protected override HashSet<CardTag> CanonicalTags => new();

    public Deplete() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target);

            // 1. 造成伤害
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .Execute(choiceContext);

            // 2. 获取当前剩余能量：使用this.Owner
            decimal currentEnergy = Owner.GetEnergy();

            if (currentEnergy > 0)
            {
                // 3. 清除全部剩余能量
                await PlayerCmd.LoseEnergy(currentEnergy, Owner);
            }
        }

    protected override void OnUpgrade()
    {
        // 升级后伤害增加 2 点 (8 -> 10)
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}