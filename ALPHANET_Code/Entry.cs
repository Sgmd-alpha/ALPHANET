using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace ALPHANET;

[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    public const string ModId = "ALPHANET";
    public const string ResPath = $"res://ALPHANET";

    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();

        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        var harmony = new Harmony(ModId);
        harmony.PatchAll();
        Logger.Info("ALPHANET initialized. H-ARCHITECTURE online.");
    }
}