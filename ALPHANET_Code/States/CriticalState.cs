using System.Reflection;
using ALPHANET.Relics;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.States;

[RegisterPower]
public sealed class CriticalState : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single; 
    private const string OverheadMarkerNodeName = "ALPHANET_CriticalOverheadMarker";
    public bool RelicImmunityActive = false;
    public bool HasEnjoyedPlayableTurn = false;
    private bool _hasEnemyTurnStarted = false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public static async Task Enter(PlayerChoiceContext? choiceContext, Creature creature, Creature? applier = null, CardModel? cardSource = null, bool fromStarterRelic = false)
    {
        // 【核心修复】：如果外部传入了 null，自动兜底创建一个 ThrowingPlayerChoiceContext
        choiceContext ??= new ThrowingPlayerChoiceContext();

        RecordEnterTrigger(creature);

        bool wasAlreadyInCritical = creature.HasPower<CriticalState>();

        // 机制触发：如果已经处于临界状态，扣除 1 点最大生命值
        if (wasAlreadyInCritical)
        {
            await CreatureCmd.LoseMaxHp(choiceContext, creature, 1m, false);
        }

        // 施加状态本身（此时 choiceContext 绝对非空，绝处逢生抽牌再也不会报错！）
        await PowerCmd.Apply<CriticalState>(choiceContext, creature, 1m, applier, cardSource);

        var power = creature.GetPower<CriticalState>();
        if (power != null)
        {
            if (fromStarterRelic) 
            {
                power.RelicImmunityActive = true;
                // 被动名刀触发时重置寿命锁，确保能活过接下来的完整回合
                power.HasEnjoyedPlayableTurn = false;
                power._hasEnemyTurnStarted = false;
            }
            else if (!wasAlreadyInCritical)
            {
                power.HasEnjoyedPlayableTurn = MiracleResidue.IsPlayerMainPhase;
            }
        }
    }

    // =========================================================
    // 视觉与动画参数配置（可根据实际立绘效果微调）
    // =========================================================
    // 图标在角色局部坐标系下的目标显示宽高（无论原图是64x64还是512x512，都会自动等比缩放到该尺寸）
    private static readonly float TargetMarkerSize = 64f;
    // 距离角色头顶基准点（或碰撞框顶部）的额外悬浮距离
    private static readonly float HeadClearanceOffset = 64f;
    // 找不到碰撞框时的默认头顶 Y 坐标兜底值
    private static readonly float DefaultFallbackTopY = -210f;


    /// <summary>
    /// 在角色头顶生成或移除临界状态标记（动画）
    /// </summary>
    private static void UpdateOverheadMarker(Creature? creature, bool show)
    {
        if (creature == null) return;

        NCreature? nCreature = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (nCreature == null || !GodotObject.IsInstanceValid(nCreature)) return;

        var parentNode = (Node)nCreature;
        var existingMarker = parentNode.GetNodeOrNull<Sprite2D>(OverheadMarkerNodeName);

        // 动态计算角色头顶的目标落点坐标
        Vector2 targetPos = CalculateOverheadPosition(nCreature);

        if (show)
        {
            // 如果标记已经存在且正在播放消失动画，先清理掉旧节点重新生成，否则直接跳过
            if (existingMarker != null && GodotObject.IsInstanceValid(existingMarker))
            {
                if (existingMarker.HasMeta("IsExiting"))
                {
                    existingMarker.QueueFree();
                }
                else
                {
                    return;
                }
            }

            var texture = ResourceLoader.Load<Texture2D>($"{Entry.ResPath}/images/vfx/CriticalMarker.png");
            if (texture == null) return;

            // 1. 根据纹理实际像素尺寸，自动计算缩放到 TargetMarkerSize 所需的 Scale
            Vector2 texSize = texture.GetSize();
            float maxDim = Math.Max(1f, Math.Max(texSize.X, texSize.Y));
            float baseScaleFactor = TargetMarkerSize / maxDim;
            Vector2 targetScale = new Vector2(baseScaleFactor, baseScaleFactor);

            // 初始位置设在目标位置正上方 45 像素处，完全透明
            Vector2 startPos = targetPos + new Vector2(0f, -45f);
            Vector2 overshootPos = targetPos + new Vector2(0f, 8f); // 滑落到底部略微过冲的位置

            var marker = new Sprite2D
            {
                Name = OverheadMarkerNodeName,
                Texture = texture,
                Position = startPos,
                Scale = targetScale * 0.85f, // 出现时略小一点，下落过程中恢复标准大小
                Modulate = new Color(1f, 1f, 1f, 0f), // 初始透明
                ZIndex = 0,
                ZAsRelative = true
            };

            parentNode.AddChild(marker);

            // 2. 编排进场动画：上方淡入滑落 -> 略微过冲下沉 -> 回弹上浮到指定位置
            Tween enterTween = marker.CreateTween();
            marker.SetMeta("ActiveTween", enterTween);

            // 第一阶段：从上方自然滑落至略微偏低的位置，同时完全淡入、恢复标准大小
            enterTween.SetParallel(true);
            enterTween.TweenProperty(marker, "position", overshootPos, 0.26)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            enterTween.TweenProperty(marker, "modulate:a", 1.0f, 0.20)
                .SetTrans(Tween.TransitionType.Linear);
            enterTween.TweenProperty(marker, "scale", targetScale, 0.24)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);

            // // 第二阶段：从最低点略微回弹上浮，停在最终的 targetPos
            // enterTween.Chain().TweenProperty(marker, "position", targetPos, 0.24)
            //     .SetTrans(Tween.TransitionType.Sine)
            //     .SetEase(Tween.EaseType.InOut);

            // // 3. 进场动画结束后，无缝衔接的上下呼吸循环动画
            // enterTween.Chain().TweenCallback(Callable.From(() =>
            // {
            //     if (!GodotObject.IsInstanceValid(marker) || marker.HasMeta("IsExiting")) return;

            //     Tween idleTween = marker.CreateTween().SetLoops();
            //     marker.SetMeta("ActiveTween", idleTween);

            //     idleTween.TweenProperty(marker, "position:y", targetPos.Y - 6f, 0.85)
            //         .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            //     idleTween.TweenProperty(marker, "position:y", targetPos.Y, 0.85)
            //         .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            // }
            
            // 回弹与呼吸参数
            const float breathAmplitude = 3f;
            const float breathPeriod = 1.7f;
            const float bounceDuration = 0.24f;

            float startX = overshootPos.X;
            float startY = overshootPos.Y;

            // 呼吸动画在 targetPos 处开始向上的速度
            float endVelocity = -breathAmplitude * Mathf.Tau / breathPeriod;

            // 五次曲线的系数：起点速度、加速度均为 0；
            // 终点速度与呼吸动画匹配，终点加速度为 0。
            float dy = targetPos.Y - startY;
            float tangent = endVelocity * bounceDuration;

            float c3 = 10f * dy - 4f * tangent;
            float c4 = -15f * dy + 7f * tangent;
            float c5 = 6f * dy - 3f * tangent;

            // 第二阶段：平滑回弹，并在终点保留向上的速度
            enterTween.Chain().TweenMethod(
                Callable.From<float>(t =>
                {
                    if (!GodotObject.IsInstanceValid(marker))
                        return;

                    float t2 = t * t;
                    float t3 = t2 * t;
                    float t4 = t3 * t;
                    float t5 = t4 * t;

                    float y = startY + c3 * t3 + c4 * t4 + c5 * t5;

                    // X 方向平滑抵达目标，避免额外的速度突变
                    float smooth = 6f * t5 - 15f * t4 + 10f * t3;
                    float x = Mathf.Lerp(startX, targetPos.X, smooth);

                    marker.Position = new Vector2(x, y);
                }),
                0f, 1f, bounceDuration
            ).SetTrans(Tween.TransitionType.Linear);

            // 第三阶段：从 targetPos 处直接进入连续的呼吸循环
            enterTween.Chain().TweenCallback(Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(marker) ||
                    marker.HasMeta("IsExiting"))
                    return;

                Tween idleTween = marker.CreateTween().SetLoops();
                marker.SetMeta("ActiveTween", idleTween);

                idleTween.TweenMethod(
                    Callable.From<float>(phase =>
                    {
                        if (!GodotObject.IsInstanceValid(marker) ||
                            marker.HasMeta("IsExiting"))
                            return;

                        float y = targetPos.Y
                            - breathAmplitude * Mathf.Sin(phase);

                        marker.Position = new Vector2(targetPos.X, y);
                    }),
                    0f, Mathf.Tau, breathPeriod
                ).SetTrans(Tween.TransitionType.Linear);
            }
            
            ));
        }
        else
        {
            if (existingMarker == null || !GodotObject.IsInstanceValid(existingMarker)) return;
            if (existingMarker.HasMeta("IsExiting")) return; // 防止重复触发退场动画

            existingMarker.SetMeta("IsExiting", true);

            // 打断正在运行的进场或呼吸动画
            if (existingMarker.HasMeta("ActiveTween"))
            {
                var oldTween = existingMarker.GetMeta("ActiveTween").As<Tween>();
                if (oldTween != null && oldTween.IsValid())
                {
                    oldTween.Kill();
                }
            }

            Vector2 currentPos = existingMarker.Position;
            Vector2 dipPos = currentPos + new Vector2(0f, 8f);     // 略微下沉蓄力点
            Vector2 risePos = currentPos + new Vector2(0f, -45f);  // 向上弹升消失点
            Vector2 currentScale = existingMarker.Scale;

            // 编排退场动画：略微下沉 -> 向上弹升并淡出 -> 销毁节点
            Tween exitTween = existingMarker.CreateTween();

            // 第一阶段：略微下沉
            exitTween.TweenProperty(existingMarker, "position", dipPos, 0.14)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);

            // 第二阶段：向上弹升并淡出，同时略微缩小
            exitTween.Chain().SetParallel(true);
            exitTween.TweenProperty(existingMarker, "position", risePos, 0.24)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.In);
            exitTween.TweenProperty(existingMarker, "modulate:a", 0.0f, 0.20)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            exitTween.TweenProperty(existingMarker, "scale", currentScale * 0.8f, 0.24)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);

            // 第三阶段：动画完毕后安全释放节点
            exitTween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(existingMarker))
                {
                    existingMarker.QueueFree();
                }
            }));
        }
    }

    /// <summary>
    /// 自动探测 NCreature 的头顶高度（优先读取碰撞框/意图锚点，实现高矮体型自适应）
    /// </summary>
    private static Vector2 CalculateOverheadPosition(NCreature nCreature)
    {
        float topY = DefaultFallbackTopY;

        try
        {
            var parentNode = (Node)nCreature;

            // 1. 尝试寻找 NCreature 内部的头顶意图容器或状态锚点
            var intentNode = parentNode.GetNodeOrNull<Node2D>("Intent") 
                          ?? parentNode.GetNodeOrNull<Node2D>("IntentContainer");
            if (intentNode != null && intentNode.Position.Y < -50f)
            {
                return new Vector2(intentNode.Position.X, intentNode.Position.Y - HeadClearanceOffset);
            }

            // 2. 尝试读取 NCreature 的 Hitbox（碰撞框 Control），根据角色实际身高动态计算头顶位置
            var hitbox = parentNode.GetNodeOrNull<Control>("Hitbox")
                      ?? typeof(NCreature).GetProperty("Hitbox", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                          ?.GetValue(nCreature) as Control;

            if (hitbox != null && hitbox.Size.Y > 10f)
            {
                // Control 的 Position.Y 在局部坐标系中即为碰撞框顶部边缘（若中心对齐则为 -Size.Y）
                float boxTop = hitbox.Position.Y < 0f ? hitbox.Position.Y : -hitbox.Size.Y;
                topY = boxTop - HeadClearanceOffset;
            }
        }
        catch
        {
            // 忽略反射或节点查找异常，安全使用 DefaultFallbackTopY
        }

        return new Vector2(0f, topY);
    }




    // =========================================================
    // 联动遗物【锦囊 (SilkPouch)】：有借有还的属性增减锁
    // =========================================================
    private bool _hasSilkPouchBonus = false;
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        UpdateOverheadMarker(Owner, show: true);
        Systems.CardCriticalBadgeSystem.RefreshAllVisibleCardBadges();

        // 当真正进入临界状态（状态实例首次挂载到身上）时检查玩家是否有《锦囊》
        if (!_hasSilkPouchBonus && Owner.IsPlayer && Owner.Player?.Relics != null)
        {
            var silkPouch = Owner.Player.Relics.OfType<SilkPouch>().FirstOrDefault();
            if (silkPouch != null)
            {
                _hasSilkPouchBonus = true;
                silkPouch.TriggerFlash();

                var ctx = new ThrowingPlayerChoiceContext();
                await PowerCmd.Apply<StrengthPower>(ctx, Owner, SilkPouch.BonusAmount, Owner, null);
                await PowerCmd.Apply<DexterityPower>(ctx, Owner, SilkPouch.BonusAmount, Owner, null);
            }
        }
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner == null || !oldOwner.IsAlive || !CombatManager.Instance.IsInProgress)
            return;
        
        UpdateOverheadMarker(oldOwner, show: false);
        Systems.CardCriticalBadgeSystem.RefreshAllVisibleCardBadges();

        // 1. 结算【锦囊 (SilkPouch)】：扣除临时给予的 2点力量 与 2点敏捷
        if (_hasSilkPouchBonus)
        {
            _hasSilkPouchBonus = false;

            var silkPouch = oldOwner.Player?.Relics?.OfType<SilkPouch>().FirstOrDefault();
            silkPouch?.TriggerFlash();

            var ctx = new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<StrengthPower>(ctx, oldOwner, -SilkPouch.BonusAmount, oldOwner, null);
            await PowerCmd.Apply<DexterityPower>(ctx, oldOwner, -SilkPouch.BonusAmount, oldOwner, null);
        }

        // 2. 结算【逻辑器A1 (LogicGateA1)】：退出临界状态时获得 6 点格挡
        if (oldOwner.IsPlayer && oldOwner.Player?.Relics != null)
        {
            foreach (var logicGate in oldOwner.Player.Relics.OfType<LogicGateA1>())
            {
                await logicGate.TriggerOnExitCritical(oldOwner);
            }
        }
    }
        
    // =========================================================
    // 跨战斗安全隔离的“临界状态触发次数”统计器
    // =========================================================
    private class TriggerTracker
    {
        // 记录上一次计数所属的战斗实例（使用 WeakReference 防止阻止战斗对象被内存回收）
        public WeakReference<ICombatState>? CombatRef;
        public int Count = 0;

        public int GetCountForCurrentCombat(ICombatState? currentCombat)
        {
            // 如果当前不在战斗中（如在地图、奖励界面查看牌组），或者还没有记录过任何战斗，直接返回 0
            if (currentCombat == null || CombatRef == null)
                return 0;

            // 只有当记录的战斗与当前正在进行的战斗是【同一场战斗】时，才返回累计次数；否则说明已经是下一场战斗了，返回 0
            if (CombatRef.TryGetTarget(out var recordedCombat) && ReferenceEquals(recordedCombat, currentCombat))
            {
                return Count;
            }

            return 0;
        }

        public void IncrementForCurrentCombat(ICombatState? currentCombat)
        {
            if (currentCombat == null) return;

            // 检查是否还在同一场战斗中
            if (CombatRef != null && CombatRef.TryGetTarget(out var recordedCombat) && ReferenceEquals(recordedCombat, currentCombat))
            {
                Count++;
            }
            else
            {
                // 开启了一场新战斗！自动重置计数器为 1，并绑定新的战斗实例
                CombatRef = new WeakReference<ICombatState>(currentCombat);
                Count = 1;
            }
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Creature, TriggerTracker> _triggerCounts = new();

    /// <summary>
    /// 获取指定生物在【当前这场战斗】中“触发进入临界状态效果”的总次数
    /// </summary>
    public static int GetEnterTriggerCount(Creature? creature)
    {
        if (creature == null || creature.CombatState == null) return 0;
        return _triggerCounts.GetOrCreateValue(creature).GetCountForCurrentCombat(creature.CombatState);
    }

    /// <summary>
    /// 记录一次【当前这场战斗】中“进入临界状态效果”的触发
    /// </summary>
    public static void RecordEnterTrigger(Creature? creature)
    {
        if (creature == null || creature.CombatState == null) return;
        _triggerCounts.GetOrCreateValue(creature).IncrementForCurrentCombat(creature.CombatState);
    }
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target == Owner && RelicImmunityActive && !MiracleResidue.IsPlayerMainPhase)
        {
            Flash();
            return 0m; 
        }
        return 1m;
    }

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && RelicImmunityActive && !MiracleResidue.IsPlayerMainPhase)
        {
            Flash();
            return 0m; 
        }
        return amount;
    }
    
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Enemy)
        {
            _hasEnemyTurnStarted = true;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player) return;

        RelicImmunityActive = false; 

        if (_hasEnemyTurnStarted && HasEnjoyedPlayableTurn)
        {
            Flash();

            // 【核心修复】：检查身上是否有未使用的《奇迹残渣》
            // 如果有，直接走免死续命逻辑并传入当前合法的 choiceContext，不在回合初调用 Kill 破坏联机状态机！
            var miracle = player.Relics?.OfType<MiracleResidue>().FirstOrDefault();
            if (miracle != null && miracle.CanPreventDeath)
            {
                // 将生命值扣到 1 点，并直接触发名刀续命
                if (Owner.CurrentHp > 1m)
                {
                    await CreatureCmd.Damage(choiceContext, Owner, Owner.CurrentHp - 1m, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
                }
                await miracle.TriggerDeathPrevention(choiceContext, Owner);
                
                // 因为已经在玩家回合初续命，本回合就是他享受的最后一个出牌回合，扣上寿命锁但剥夺当回合免伤罩
                RelicImmunityActive = false;
                HasEnjoyedPlayableTurn = true;
                _hasEnemyTurnStarted = false;
            }
            else
            {
                // 没有名刀保护，真正执行处决
                await CreatureCmd.Kill(Owner);
            }
        }
        else
        {
            HasEnjoyedPlayableTurn = true;
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (target.Side == CombatSide.Enemy)
        {
            Flash();
            await PowerCmd.Remove<CriticalState>(Owner);
        }
    }
}