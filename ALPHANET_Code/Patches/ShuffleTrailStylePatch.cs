using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Systems;

[HarmonyPatch(typeof(NCardFlyShuffleVfx))]
public static class ShuffleTrailStylePatch
{
    private static readonly ConditionalWeakTable<NCardFlyShuffleVfx, CharacterModel> ShuffleOwners = new();

    private static readonly FieldInfo? VfxField =
        AccessTools.Field(typeof(NCardFlyShuffleVfx), "_vfx");

    private static readonly FieldInfo? TargetPileField =
        AccessTools.Field(typeof(NCardFlyShuffleVfx), "_targetPile");

    // 1. 在 Create 阶段记录本次跨牌堆飞卡所属的角色
    [HarmonyPatch(nameof(NCardFlyShuffleVfx.Create))]
    [HarmonyPostfix]
    public static void CaptureCharacterOnCreate(
        CardPile startPile,
        CardPile targetPile,
        NCardFlyShuffleVfx? __result)
    {
        if (__result == null) return;

        CharacterModel? character = ResolveCharacter(targetPile) ?? ResolveCharacter(startPile);
        if (character != null)
        {
            ShuffleOwners.Remove(__result);
            ShuffleOwners.Add(__result, character);
        }
    }

    [HarmonyPatch(nameof(NCardFlyShuffleVfx._Ready))]
    [HarmonyPostfix]
    public static void ApplyTrailStyleOnReady(NCardFlyShuffleVfx __instance)
    {
        if (VfxField?.GetValue(__instance) is not NCardTrailVfx trailVfx)
            return;

        if (!ShuffleOwners.TryGetValue(__instance, out CharacterModel? character))
        {
            var targetPile = TargetPileField?.GetValue(__instance) as CardPile;
            character = ResolveCharacter(targetPile);
        }

        CharacterTrailStyle? style = GetTrailStyle(character);
        if (style == null)
            return;

        ApplyStyleToTrailVfx(trailVfx, style);
    }

    private static CharacterTrailStyle? GetTrailStyle(CharacterModel? character)
    {
        if (character == null)
            return null;

        object? profile = character.GetType()
            .GetProperty("AssetProfile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(character);

        object? vfx = profile?.GetType()
            .GetProperty("Vfx", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(profile);

        return vfx?.GetType()
            .GetProperty("TrailStyle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(vfx) as CharacterTrailStyle;
    }
    
    private static CharacterModel? ResolveCharacter(CardPile? pile)
    {
        if (pile == null) return null;

        // 优先直接从牌堆现有卡牌中取 Owner（CardPileCmd.Add 在播放动画前已将卡牌加入 targetPile）
        CharacterModel? fromCards = pile.Cards.FirstOrDefault()?.Owner?.Character;
        if (fromCards != null) return fromCards;

        // 兜底：通过反射读取 CardPile 可能持有的 Player 字段/属性
        Type pileType = pile.GetType();
        foreach (PropertyInfo prop in pileType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (typeof(Player).IsAssignableFrom(prop.PropertyType) && prop.GetValue(pile) is Player p)
                return p.Character;
        }
        foreach (FieldInfo field in pileType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (typeof(Player).IsAssignableFrom(field.FieldType) && field.GetValue(pile) is Player p)
                return p.Character;
        }

        return null;
    }

    private static void ApplyStyleToTrailVfx(NCardTrailVfx trailVfx, CharacterTrailStyle style)
    {
        var lines = new List<Line2D>();
        var particles = new List<CpuParticles2D>();
        var sprites = new List<Sprite2D>();

        CollectNodes(trailVfx, lines, particles, sprites);

        // 1. 轨迹带（Line2D）：优先按节点名匹配，否则按宽度从粗到细区分外/内轨迹
        var unmatchedLines = new List<Line2D>();
        foreach (Line2D line in lines)
        {
            string name = line.Name.ToString();
            if (name.Contains("Inner", StringComparison.OrdinalIgnoreCase))
                ApplyLineStyle(line, style.InnerTrailModulate, style.InnerTrailWidth);
            else if (name.Contains("Outer", StringComparison.OrdinalIgnoreCase))
                ApplyLineStyle(line, style.OuterTrailModulate, style.OuterTrailWidth);
            else
                unmatchedLines.Add(line);
        }

        if (unmatchedLines.Count > 0)
        {
            unmatchedLines.Sort((a, b) => b.Width.CompareTo(a.Width));
            ApplyLineStyle(unmatchedLines[0], style.OuterTrailModulate, style.OuterTrailWidth);
            for (int i = 1; i < unmatchedLines.Count; i++)
                ApplyLineStyle(unmatchedLines[i], style.InnerTrailModulate, style.InnerTrailWidth);
        }

        // 2. 火花粒子（CpuParticles2D）：优先按节点名匹配，否则按粒子尺寸区分大/小火花
        var unmatchedParticles = new List<CpuParticles2D>();
        foreach (CpuParticles2D cpu in particles)
        {
            string name = cpu.Name.ToString();
            if (name.Contains("Little", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Small", StringComparison.OrdinalIgnoreCase))
                ApplyParticleStyle(cpu, style.LittleSparksColor);
            else if (name.Contains("Big", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Large", StringComparison.OrdinalIgnoreCase))
                ApplyParticleStyle(cpu, style.BigSparksColor);
            else
                unmatchedParticles.Add(cpu);
        }

        if (unmatchedParticles.Count > 0)
        {
            unmatchedParticles.Sort((a, b) => b.ScaleAmountMax.CompareTo(a.ScaleAmountMax));
            ApplyParticleStyle(unmatchedParticles[0], style.BigSparksColor);
            for (int i = 1; i < unmatchedParticles.Count; i++)
                ApplyParticleStyle(unmatchedParticles[i], style.LittleSparksColor);
        }

        // 3. 微缩卡牌剪影（Sprite2D）：不修改父容器 Sprites 的 Modulate（避免与 _introTween 冲突）
        var unmatchedSprites = new List<Sprite2D>();
        foreach (Sprite2D sprite in sprites)
        {
            string name = sprite.Name.ToString();
            if (name.Contains("Secondary", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Inner", StringComparison.OrdinalIgnoreCase))
                ApplySpriteStyle(sprite, style.SecondarySpriteModulate, style.SecondarySpriteScale);
            else if (name.Contains("Primary", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Outer", StringComparison.OrdinalIgnoreCase))
                ApplySpriteStyle(sprite, style.PrimarySpriteModulate, style.PrimarySpriteScale);
            else
                unmatchedSprites.Add(sprite);
        }

        if (unmatchedSprites.Count > 0)
        {
            ApplySpriteStyle(unmatchedSprites[0], style.PrimarySpriteModulate, style.PrimarySpriteScale);
            for (int i = 1; i < unmatchedSprites.Count; i++)
                ApplySpriteStyle(unmatchedSprites[i], style.SecondarySpriteModulate, style.SecondarySpriteScale);
        }
    }

    private static void CollectNodes(
        Node root,
        List<Line2D> lines,
        List<CpuParticles2D> particles,
        List<Sprite2D> sprites)
    {
        foreach (Node child in root.GetChildren())
        {
            switch (child)
            {
                case Line2D line:
                    lines.Add(line);
                    break;
                case CpuParticles2D cpu:
                    particles.Add(cpu);
                    break;
                case Sprite2D sprite:
                    sprites.Add(sprite);
                    break;
            }

            if (child.GetChildCount() > 0)
                CollectNodes(child, lines, particles, sprites);
        }
    }

    private static void ApplyLineStyle(Line2D line, Color? modulate, float? widthScale)
    {
        if (modulate.HasValue)
            line.Modulate = modulate.Value;
        if (widthScale.HasValue)
            line.Width *= widthScale.Value;
    }

    private static void ApplyParticleStyle(CpuParticles2D particles, Color? color)
    {
        if (color.HasValue)
            particles.Color = color.Value;
    }

    private static void ApplySpriteStyle(Sprite2D sprite, Color? modulate, Vector2? scale)
    {
        if (modulate.HasValue)
            sprite.Modulate = modulate.Value;
        if (scale.HasValue)
            sprite.Scale = scale.Value;
    }
}

internal class ModCharacterTemplate
{
}