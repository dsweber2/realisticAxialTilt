using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Compat
{
    internal static class ConfigurableMapsCompat
    {
        private static bool? _isActive;
        private static Type _settingsWindowType;

        internal static bool IsActive
        {
            get
            {
                if (!_isActive.HasValue)
                    _isActive = LoadedModManager.RunningMods
                        .Any(m => m.PackageId.Equals("Mlie.KVConfigurableMaps", StringComparison.OrdinalIgnoreCase));
                return _isActive.Value;
            }
        }

        internal static void TryPatch(Harmony harmony)
        {
            if (!IsActive) return;

            var patchClass = AccessTools.TypeByName("ConfigurableMaps.Patch_Page_CreateWorldParams_DoWindowContents");
            var postfix    = patchClass != null ? AccessTools.Method(patchClass, "Postfix") : null;
            if (postfix == null)
            {
                Log.Warning("[RAT] ConfigurableMapsCompat: could not find ConfigurableMaps postfix to suppress.");
                return;
            }

            _settingsWindowType = AccessTools.TypeByName("ConfigurableMaps.SettingsWindow");

            harmony.Patch(postfix, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(ConfigurableMapsCompat), nameof(SuppressPostfix))));
        }

        private static bool SuppressPostfix() => false;

        internal static void DrawBottomButton(Rect btnRect)
        {
            if (!IsActive) return;
            if (Widgets.ButtonText(btnRect, "ConfigurableMaps".Translate())
                && _settingsWindowType != null
                && !Find.WindowStack.TryRemove(_settingsWindowType, true))
            {
                Find.WindowStack.Add((Window)Activator.CreateInstance(_settingsWindowType));
            }
        }
    }
}
