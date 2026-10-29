using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

/// <summary>
/// 挂载在打出《最优提案》的玩家自身上的状态（标记自己为伤害转移的接收方，敌方回合结束时移除）
/// </summary>
[RegisterPower]
public sealed class OptimalProposalSelfPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 在敌方回合结束时移除（保护持续整个本回合+敌方攻击回合）
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.Remove(this);
        }
    }
}

/// <summary>
/// 挂载在队友身上的状态：
/// 1. 队友自身的格挡、无实体、钨合金棍正常结算；
/// 2. 任何穿透格挡的未格挡伤害（Unblocked Damage）对队友清零，并全额转移给 Protector（打出《最优提案》的玩家）；
/// 3. 在队友头顶实时显示怪物攻击意图图标与即将承受的攻击伤害数值！
/// </summary>
[RegisterPower]
[HarmonyPatch]
public sealed class OptimalCoveredPower : ModPowerTemplate
{
    private const string OverheadIntentNodeName = "ALPHANET_TeammateDamageIntent";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    // 记录是谁打出了《最优提案》来保护这名队友
    public Creature? Protector { get; set; }

    // 队列记录每次受击时被拦截下来的未格挡伤害，在 AfterDamageReceived 中立即转移给 Protector
    private readonly Queue<decimal> _interceptedUnblockedDamages = new();

    // static OptimalCoveredPower()
    // {
    //     try
    //     {
    //         var harmony = new Harmony("alphanet.optimalproposal.patch");
    //         harmony.PatchAll();
    //     }
    //     catch (Exception ex)
    //     {
    //         GD.PrintErr($"[ALPHANET] OptimalCoveredPower patch error: {ex}");
    //     }
    // }

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/OptimalProposalSelfPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/OptimalProposalSelfPower.png"
    );

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (applier != null)
        {
            Protector = applier;
        }
        RefreshOverheadIntent();
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        RemoveOverheadIntent(oldOwner);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner != null)
        {
            RemoveOverheadIntent(Owner);
        }
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.Remove(this);
        }
    }

    // =========================================================================
    // 核心机制 1：拦截队友穿透格挡后的最终扣血 (LoseHpInternal)
    // 此时队友的格挡已经被正常扣除，无实体/钨合金棍也已结算完毕！
    // =========================================================================
    // 标记当前是否正在执行“最优提案的代承伤害转移”，防止双人互换伤害时死循环
    private static bool _isRedirectingDamage = false;
    [HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
    [HarmonyPrefix]
    public static void InterceptTeammateUnblockedHpLoss(Creature __instance, ref decimal amount, ValueProp props)
    {
        if (__instance == null || amount <= 0m)
            return;

        // 如果当前正在处理一笔转移伤害，绝对不要二次拦截！
        // 这确保了：A 替 B 承受伤害时，即使 A 自己也开了最优提案，这笔伤害也会结结实实扣在 A 身上，而不会被 A 再次甩回去给 B！
        if (_isRedirectingDamage)
            return;

        var coveredPower = __instance.GetPower<OptimalCoveredPower>();
        if (coveredPower == null || coveredPower.Protector == null || !coveredPower.Protector.IsAlive)
            return;

        // 仅拦截敌人攻击穿透格挡造成的未格挡伤害
        coveredPower._interceptedUnblockedDamages.Enqueue(amount);
        amount = 0m;
    }

    // =========================================================================
    // 核心机制 2：在队友受击结算完毕后，立刻将刚才拦截下的未格挡伤害转移给《最优提案》发起者！
    // =========================================================================
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        RefreshOverheadIntent();

        if (target != Owner || _interceptedUnblockedDamages.Count == 0)
            return;

        decimal totalToRedirect = 0m;
        while (_interceptedUnblockedDamages.Count > 0)
        {
            totalToRedirect += _interceptedUnblockedDamages.Dequeue();
        }

        if (totalToRedirect > 0m && Protector != null && Protector.IsAlive)
        {
            Flash();
            var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();

            _isRedirectingDamage = true;
            try
            {
                // 此时造成的扣血，由于 _isRedirectingDamage = true，
                // 即使 Protector 身上也有 OptimalCoveredPower，也不会触发二次转移！
                await CreatureCmd.Damage(
                    ctx,
                    Protector,
                    totalToRedirect,
                    ValueProp.Unpowered | ValueProp.Move,
                    null, // dealer
                    null, // cardSource
                    null  // cardPlay
                );
            }
            finally
            {
                _isRedirectingDamage = false;
            }
        }
    }

    // 当任何生物格挡变化、能力变化或出牌后，实时刷新队友头顶的代承伤害意图数字
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockGained))]
    [HarmonyPostfix]
    public static void OnAnyBlockGained(ICombatState combatState) => RefreshAllTeammateIntents(combatState);

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPowerAmountChanged))]
    [HarmonyPostfix]
    public static void OnAnyPowerChangedForIntent(ICombatState combatState) => RefreshAllTeammateIntents(combatState);

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
    [HarmonyPostfix]
    public static void OnAnyCardPlayedForIntent(ICombatState combatState) => RefreshAllTeammateIntents(combatState);

    public static void RefreshAllTeammateIntents(ICombatState? combatState)
    {
        if (combatState == null) return;
        foreach (var creature in combatState.Creatures)
        {
            creature?.GetPower<OptimalCoveredPower>()?.RefreshOverheadIntent();
        }
    }

    // =========================================================================
    // 核心机制 3：计算队友即将受到的敌人攻击伤害，并在队友头顶渲染怪物同款 AttackIntent 图标与数字！
    // =========================================================================
    public void RefreshOverheadIntent()
    {
        if (Owner == null || !Owner.IsAlive || Owner.CombatState == null)
        {
            if (Owner != null) RemoveOverheadIntent(Owner);
            return;
        }

        int incomingAttackDmg = CalculateIncomingAttackDamageFor(Owner);
        UpdateOverheadIntentVisual(Owner, incomingAttackDmg);
    }

    /// <summary>
    /// 遍历场上所有存活的敌人，读取其当前回合的 AttackIntent 并汇总对该队友造成的预期攻击总伤害
    /// </summary>
    private static int CalculateIncomingAttackDamageFor(Creature ally)
    {
        var combatState = ally.CombatState;
        if (combatState == null) return 0;

        int totalIncoming = 0;
        var singleTargetList = new List<Creature> { ally };

        foreach (var enemy in combatState.GetOpponentsOf(ally))
        {
            if (enemy == null || !enemy.IsAlive) continue;

            // 通过反射兼容提取敌人身上的 NextMove / Intent 列表
            var intents = ExtractEnemyAttackIntents(enemy);
            foreach (var attackIntent in intents)
            {
                try
                {
                    // 使用官方 AttackIntent 的计算逻辑，但将伤害目标代入为这名队友 ally！
                    decimal rawDmg = attackIntent.DamageCalc != null ? attackIntent.DamageCalc() : 0m;
                    if (rawDmg <= 0m) continue;

                    decimal modifiedDmg = Hook.ModifyDamage(
                        ally.Player?.RunState ?? combatState.RunState,
                        combatState,
                        ally,
                        enemy,
                        rawDmg,
                        ValueProp.Move,
                        null,
                        null,
                        ModifyDamageHookType.All,
                        MegaCrit.Sts2.Core.Entities.Cards.CardPreviewMode.None,
                        out _
                    );

                    int singleHit = Math.Max(0, (int)modifiedDmg);
                    int hits = Math.Max(1, attackIntent.Repeats > 0 ? attackIntent.Repeats : 1);
                    totalIncoming += singleHit * hits;
                }
                catch
                {
                    // 兜底直接调用 GetTotalDamage
                    try
                    {
                        totalIncoming += Math.Max(0, attackIntent.GetTotalDamage(singleTargetList, enemy));
                    }
                    catch { }
                }
            }
        }

        return totalIncoming;
    }

    private static IEnumerable<AttackIntent> ExtractEnemyAttackIntents(Creature enemy)
    {
        var result = new List<AttackIntent>();
        object? monster = Traverse.Create(enemy).Property("Monster")?.GetValue()
                       ?? Traverse.Create(enemy).Field("_monster")?.GetValue();

        object? targetObj = monster ?? (object)enemy;
        foreach (var propName in new[] { "Intents", "CurrentIntents", "NextMove" })
        {
            var val = Traverse.Create(targetObj).Property(propName)?.GetValue()
                   ?? Traverse.Create(enemy).Property(propName)?.GetValue();

            if (val is IEnumerable<AbstractIntent> intentList)
            {
                result.AddRange(intentList.OfType<AttackIntent>());
            }
            else if (val is AbstractIntent singleIntent && singleIntent is AttackIntent ai)
            {
                result.Add(ai);
            }
            else if (val != null)
            {
                var moveIntents = Traverse.Create(val).Property("Intents")?.GetValue() as IEnumerable<AbstractIntent>;
                if (moveIntents != null)
                {
                    result.AddRange(moveIntents.OfType<AttackIntent>());
                }
            }
        }

        return result.Distinct();
    }

    /// <summary>
    /// 在队友头顶挂载/更新与怪物完全一致的五档攻击意图贴图 (intent_attack_1 ~ 5) 与伤害数字
    /// </summary>
    private static void UpdateOverheadIntentVisual(Creature ally, int totalIncomingAttackDamage)
    {
        var combatRoom = NCombatRoom.Instance;
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)) return;

        Node? creatureNode = combatRoom.GetCreatureNode(ally);
        if (creatureNode == null || !GodotObject.IsInstanceValid(creatureNode)) return;

        var container = creatureNode.GetNodeOrNull<Control>(OverheadIntentNodeName);
        if (container == null)
        {
            container = new Control
            {
                Name = OverheadIntentNodeName,
                Position = new Vector2(-32f, -230f), // 位于队友角色头顶正上方
                ZIndex = 20,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            var iconRect = new TextureRect
            {
                Name = "IntentIcon",
                CustomMinimumSize = new Vector2(64f, 64f),
                Size = new Vector2(64f, 64f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            var dmgLabel = new Label
            {
                Name = "IntentDamageLabel",
                Position = new Vector2(28f, 26f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            dmgLabel.AddThemeFontSizeOverride("font_size", 24);
            dmgLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.9f));
            dmgLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            dmgLabel.AddThemeConstantOverride("outline_size", 8);

            container.AddChild(iconRect);
            container.AddChild(dmgLabel);
            creatureNode.AddChild(container);
        }

        container.Visible = true;

        // 按照官方 AttackIntent.GetTexture 的五档阈值加载原版攻击意图刀剑贴图
        string tier = totalIncomingAttackDamage < 5 ? "1"
                    : totalIncomingAttackDamage < 10 ? "2"
                    : totalIncomingAttackDamage < 20 ? "3"
                    : totalIncomingAttackDamage >= 40 ? "5" : "4";

        string imagePath = ImageHelper.GetImagePath($"atlases/intent_atlas.sprites/attack/intent_attack_{tier}.tres");
        var iconNode = container.GetNode<TextureRect>("IntentIcon");
        var labelNode = container.GetNode<Label>("IntentDamageLabel");

        try
        {
            iconNode.Texture = PreloadManager.Cache.GetTexture2D(imagePath);
        }
        catch
        {
            // 若缓存未命中则直接通过 ResourceLoader 加载
            if (ResourceLoader.Exists(imagePath))
            {
                iconNode.Texture = ResourceLoader.Load<Texture2D>(imagePath);
            }
        }

        // 计算扣除队友当前格挡后，实际还将溢出转移给你的未格挡伤害：
        // 显示格式：直接显示队友即将受到的攻击伤害（若有格挡，括号内或高亮展示未格挡部分）
        int unblockedPreview = Math.Max(0, totalIncomingAttackDamage - ally.Block);
        labelNode.Text = ally.Block > 0
            ? $"{unblockedPreview} ({totalIncomingAttackDamage})"
            : $"{totalIncomingAttackDamage}";
    }

    private static void RemoveOverheadIntent(Creature ally)
    {
        var combatRoom = NCombatRoom.Instance;
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)) return;

        Node? creatureNode = combatRoom.GetCreatureNode(ally);
        var container = creatureNode?.GetNodeOrNull<Control>(OverheadIntentNodeName);
        if (container != null)
        {
            container.Visible = false;
            container.QueueFree();
        }
    }
}