using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AutoReadMod
{
    /// <summary>
    /// Patches XUiC_CollectedItemList.removeLastEntry so entries we added (auto-read notifications)
    /// stay visible for VisualizationDurationSeconds instead of the game's default.
    /// </summary>
    [HarmonyPatch]
    internal static class CollectedItemListRemoveLastEntryPatch
    {
        static MethodBase TargetMethod()
        {
            var type = typeof(XUiC_CollectedItemList);
            var method = type.GetMethod("removeLastEntry", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                var ps = method.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(int))
                    return method;
            }
            return null;
        }

        static bool Prefix(XUiC_CollectedItemList __instance, int index)
        {
            if (!ModConfig.Instance.VisualizationEnabled || ModConfig.Instance.VisualizationDurationSeconds <= 0f)
                return true;

            try
            {
                var listType = __instance.GetType();
                var itemsProp = listType.GetProperty("items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (itemsProp == null) return true;

                var items = itemsProp.GetValue(__instance) as System.Collections.IList;
                if (items == null || index < 0 || index >= items.Count) return true;

                var data = items[index];
                if (data == null) return true;

                if (!AutoReadVisualizationOverlay.OurEntryDataRefs.Contains(data))
                    return true;

                var dataType = data.GetType();
                var timeAddedField = dataType.GetField("TimeAdded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (timeAddedField == null || timeAddedField.FieldType != typeof(float))
                    return true;

                float timeAdded = (float)timeAddedField.GetValue(data);
                float elapsed = Time.time - timeAdded;
                float required = ModConfig.Instance.VisualizationDurationSeconds;

                if (elapsed < required)
                    return false;

                AutoReadVisualizationOverlay.OurEntryDataRefs.Remove(data);
            }
            catch
            {
                // Non-fatal; allow game to remove
            }

            return true;
        }
    }
}
