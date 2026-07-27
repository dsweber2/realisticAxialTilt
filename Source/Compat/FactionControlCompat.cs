using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Compat
{
    internal static class FactionControlCompat
    {
        private static bool? _isActive;
        private static Type _settingsWindowType;

        internal static bool IsActive
        {
            get
            {
                if (!_isActive.HasValue)
                    _isActive = LoadedModManager.RunningMods.Any(
                        m => m.PackageId.Equals("thereallemon.factioncontrol", StringComparison.OrdinalIgnoreCase));
                return _isActive.Value;
            }
        }

        internal static void TryPatch(Harmony harmony)
        {
            if (!IsActive) return;

            var patchClass = AccessTools.TypeByName("FactionControl.Patch_Page_CreateWorldParams_DoWindowContents");
            var postfix    = patchClass != null ? AccessTools.Method(patchClass, "Postfix") : null;
            if (postfix == null)
            {
                Log.Warning("[RAT] FactionControlCompat: could not find FactionControl postfix to suppress.");
                return;
            }

            harmony.Patch(postfix, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(FactionControlCompat), nameof(SuppressFCPostfix))));
        }

        // Suppresses FactionControl's own button (drawn at wrong y).
        // We redraw it ourselves in the correct bottom-bar position.
        private static bool SuppressFCPostfix() => false;

        internal static void DrawBottomButton(Rect btnRect)
        {
            if (!IsActive) return;

            if (Widgets.ButtonText(btnRect, "Faction Control"))
            {
                EnsureSettingsWindowType();
                if (_settingsWindowType != null)
                {
                    if (!Find.WindowStack.TryRemove(_settingsWindowType, true))
                        Find.WindowStack.Add((Window)Activator.CreateInstance(_settingsWindowType));
                }
            }
        }

        private static void EnsureSettingsWindowType()
        {
            if (_settingsWindowType != null) return;
            Assembly a = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetName().Name == "FactionControl");
            _settingsWindowType = a?.GetType("FactionControl.SettingsWindow");
        }
    }
}
