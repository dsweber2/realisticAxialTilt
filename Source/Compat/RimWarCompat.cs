using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Compat
{
    internal static class RimWarCompat
    {
        private static bool? _isActive;
        private static MethodInfo _openSettings;

        internal static bool IsActive
        {
            get
            {
                if (!_isActive.HasValue)
                    _isActive = LoadedModManager.RunningMods
                        .Any(m => m.PackageId.Equals("Torann.RimWar", StringComparison.OrdinalIgnoreCase));
                return _isActive.Value;
            }
        }

        internal static void TryPatch(Harmony harmony)
        {
            if (!IsActive) return;

            var patchClass = AccessTools.TypeByName("RimWar.Harmony.RimWarMod+Patch_Page_CreateWorldParams_DoWindowContents");
            var postfix    = patchClass != null ? AccessTools.Method(patchClass, "Postfix") : null;
            if (postfix == null)
            {
                Log.Warning("[RAT] RimWarCompat: could not find RimWar postfix to suppress.");
                return;
            }

            _openSettings = patchClass != null ? AccessTools.Method(patchClass, "OpenSettingsWindow") : null;

            harmony.Patch(postfix, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(RimWarCompat), nameof(SuppressPostfix))));
        }

        private static bool SuppressPostfix() => false;

        internal static void DrawBottomButton(Rect btnRect, Page_CreateWorldParams instance)
        {
            if (!IsActive) return;
            if (Widgets.ButtonText(btnRect, "RW_RimWar".Translate()))
                _openSettings?.Invoke(null, new object[] { instance });
        }
    }
}
