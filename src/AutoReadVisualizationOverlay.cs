using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AutoReadMod
{
    /// <summary>
    /// Renders auto-read visualization events. Uses the game's XUiC_CollectedItemList (same area as loot pickups)
    /// so icons appear in a consistent, recognizable place for both container and bundle reads.
    /// Prefers AddItemStack for correct item icons; falls back to AddIconNotification. Applies type-based color when possible.
    /// </summary>
    internal static class AutoReadVisualizationOverlay
    {
        private static MethodInfo _addIconNotificationMethod;
        private static MethodInfo _addItemStackMethod;

        /// <summary>Pending color re-apply: (Data ref, color, time added). Re-apply each frame for a short window so color sticks if game rebinds.</summary>
        private static readonly List<PendingColor> PendingColors = new List<PendingColor>();
        /// <summary>Bundle entries: game may replace list refs so we re-apply by index (first N entries = our bundle notifications).</summary>
        private static readonly List<(UnityEngine.Color Color, float AddedAt)> PendingBundleColors = new List<(UnityEngine.Color, float)>();
        private const float PendingColorWindowSeconds = 2f;

        private static float _lastCollectedListNullLogTime = -999f;
        private const float CollectedListNullLogIntervalSeconds = 10f;
        private static float _lastVisualizationExceptionLogTime = -999f;
        private const float VisualizationExceptionLogIntervalSeconds = 10f;
        /// <summary>Rows already matched this frame when styling vanilla bundle notifications (same class, multiple stacks).</summary>
        private static readonly HashSet<int> DecorateVanillaUsedIndices = new HashSet<int>();

        private sealed class PendingColor
        {
            internal object DataRef;
            internal UnityEngine.Color Color;
            internal float AddedAt;
        }

        public static void OnUpdate(object xui)
        {
            // DeferredContainerTick runs in XUiUpdatePatch.Postfix before this method — do not tick again here (avoids double entry per frame).
            if (xui == null || !ModConfig.Instance.VisualizationEnabled)
            {
                return;
            }

            try
            {
                OnUpdateVisualizationCore(xui);
            }
            catch (Exception ex)
            {
                if (Time.time - _lastVisualizationExceptionLogTime >= VisualizationExceptionLogIntervalSeconds)
                {
                    _lastVisualizationExceptionLogTime = Time.time;
                    try
                    {
                        UnityEngine.Debug.LogWarning("[AutoReadMod] Visualization OnUpdate (suppressed repeat): " + ex.Message);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }

        private static void OnUpdateVisualizationCore(object xui)
        {
            var collectedList = GameReflection.GetCollectedItemList(xui);
            if (collectedList == null)
            {
                if (ModConfig.Instance.DebugMode && (Time.time - _lastCollectedListNullLogTime) >= CollectedListNullLogIntervalSeconds)
                {
                    _lastCollectedListNullLogTime = Time.time;
                    try { UnityEngine.Debug.Log("[AutoReadMod] Visualization: CollectedItemList is null (XUi may not have it yet)"); } catch { }
                }
                return;
            }

            int shown = 0;
            int maxVisible = ModConfig.Instance.VisualizationMaxVisible;
            if (maxVisible <= 0)
            {
                maxVisible = 8;
            }

            DecorateVanillaUsedIndices.Clear();

            while (AutoReadVisualizationQueue.TryDequeue(out var evt))
            {
                if (evt.DecorateVanillaEntryOnly)
                {
                    TryDecorateVanillaBundleRow(collectedList, evt);
                    continue;
                }

                if (shown >= maxVisible)
                {
                    AutoReadVisualizationQueue.Enqueue(evt);
                    break;
                }

                if (ModConfig.Instance.DebugMode)
                {
                    try { UnityEngine.Debug.Log($"[AutoReadMod] Visualization: dequeued event {evt.ItemClassName ?? evt.IconName}, ItemValue={evt.ItemValue != null}"); } catch { }
                }
                bool added = false;

                if (evt.ItemValue != null)
                {
                    try
                    {
                        added = TryAddItemStackWithValue(collectedList, evt.ItemValue, evt.Count);
                        if (added)
                        {
                            RegisterLastEntryAndApplyColor(collectedList, evt.Kind, evt.Source);
                            TryClearLastEntryCountText(collectedList, evt.Source);
                        }
                        else if (ModConfig.Instance.DebugMode)
                        {
                            try { UnityEngine.Debug.Log($"[AutoReadMod] Visualization: AddItemStack (with ItemValue) failed for '{evt.ItemClassName}'"); } catch { }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        if (ModConfig.Instance.DebugMode)
                        {
                            try { UnityEngine.Debug.Log($"[AutoReadMod] Visualization: exception adding entry: {ex.Message}\n{ex.StackTrace}"); } catch { }
                        }
                    }
                }
                if (!added && !string.IsNullOrEmpty(evt.ItemClassName))
                {
                    added = TryAddItemStack(collectedList, evt.ItemClassName, evt.Count);
                    if (added)
                    {
                        RegisterLastEntryAndApplyColor(collectedList, evt.Kind, evt.Source);
                        TryClearLastEntryCountText(collectedList, evt.Source);
                    }
                    else if (ModConfig.Instance.DebugMode)
                    {
                        try { UnityEngine.Debug.Log($"[AutoReadMod] Visualization: AddItemStack failed for '{evt.ItemClassName}', using fallback"); } catch { }
                    }
                }

                if (!added && !string.IsNullOrEmpty(evt.IconName))
                {
                    added = TryAddIconNotification(collectedList, evt.IconName, evt.Count > 0 ? evt.Count : 1);
                }

                if (added)
                {
                    shown++;
                }
            }

            ReapplyPendingColors(collectedList);
            PrunePendingColors();
        }

        private static void RegisterLastEntryAndApplyColor(object collectedItemList, AutoReader.ReadableItemKind kind, AutoReadVisualizationSource source)
        {
            object lastData = GetNewlyAddedEntryData(collectedItemList, source);
            if (lastData == null)
            {
                if (ModConfig.Instance.DebugMode)
                {
                    try { UnityEngine.Debug.Log("[AutoReadMod] Visualization: GetNewlyAddedEntryData returned null (list structure may differ)"); } catch { }
                }
                return;
            }
            var color = GetColorForKind(kind);
            bool applied = TrySetLastEntryColor(collectedItemList, lastData, color, kind);
            if (ModConfig.Instance.DebugMode)
            {
                try
                {
                    UnityEngine.Debug.Log(applied
                        ? $"[AutoReadMod] Visualization: set color for last entry (Kind={kind})"
                        : "[AutoReadMod] Visualization: no UISprite/formatter found for last entry");
                }
                catch { }
            }
            if (source == AutoReadVisualizationSource.Bundle)
                PendingBundleColors.Add((color, Time.time));
            else
                PendingColors.Add(new PendingColor { DataRef = lastData, Color = color, AddedAt = Time.time });
        }

        /// <summary>Returns the newly added entry: index 0 for Bundle (game prepends), last for Container (game appends).</summary>
        private static object GetNewlyAddedEntryData(object collectedItemList, AutoReadVisualizationSource source)
        {
            var items = GetItemsList(collectedItemList);
            if (items == null || items.Count == 0) return null;
            if (source == AutoReadVisualizationSource.Bundle)
                return items[0];
            return items[items.Count - 1];
        }

        /// <summary>
        /// Match vanilla CollectedItemList row to auto-read item (by ItemStack class or uiAtlasIcon), apply type color, strip trailing "(N)".
        /// </summary>
        private static void TryDecorateVanillaBundleRow(object collectedList, AutoReadVisualizationEvent evt)
        {
            try
            {
                var items = GetItemsList(collectedList);
                if (items == null || string.IsNullOrEmpty(evt.ItemClassName))
                {
                    return;
                }

                for (int i = 0; i < items.Count; i++)
                {
                    if (DecorateVanillaUsedIndices.Contains(i))
                    {
                        continue;
                    }

                    object row = items[i];
                    if (!CollectedDataRowMatchesEvent(row, evt))
                    {
                        continue;
                    }

                    DecorateVanillaUsedIndices.Add(i);
                    var color = GetColorForKind(evt.Kind);
                    ApplyColorToEntryAt(collectedList, items, i, color);
                    StripInventoryParentheticalOnEntry(GetItemGameObjectFromDataRow(row));
                    PendingColors.Add(new PendingColor { DataRef = row, Color = color, AddedAt = Time.time });
                    if (ModConfig.Instance.DebugMode)
                    {
                        try { UnityEngine.Debug.Log($"[AutoReadMod] Visualization: styled vanilla row i={i} {evt.ItemClassName}"); } catch { }
                    }
                    return;
                }
            }
            catch
            {
                /* non-critical */
            }
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
            {
                return null;
            }

            var t = instance.GetType();
            var p = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null)
            {
                return p.GetValue(instance, null);
            }

            var f = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return f?.GetValue(instance);
        }

        private static object GetItemGameObjectFromDataRow(object dataRow)
        {
            if (dataRow == null)
            {
                return null;
            }

            foreach (var name in new[] { "Item", "item" })
            {
                var go = GetMemberValue(dataRow, name);
                if (go != null)
                {
                    return go;
                }
            }

            return null;
        }

        private static bool CollectedDataRowMatchesEvent(object dataRow, AutoReadVisualizationEvent evt)
        {
            foreach (var name in new[] { "ItemStack", "itemStack" })
            {
                var stackObj = GetMemberValue(dataRow, name);
                if (!(stackObj is ItemStack stack) || stack == null)
                {
                    continue;
                }

                if (stack.IsEmpty())
                {
                    continue;
                }

                var iv = GameReflection.GetItemValue(stack);
                var ic = GameReflection.GetItemClass(iv);
                string className = GameReflection.GetItemClassName(ic);
                if (!string.IsNullOrEmpty(className)
                    && string.Equals(className, evt.ItemClassName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(evt.IconName))
            {
                var icon = GetMemberValue(dataRow, "uiAtlasIcon") as string;
                if (!string.IsNullOrEmpty(icon)
                    && string.Equals(icon, evt.IconName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ReapplyPendingColors(object collectedItemList)
        {
            if (collectedItemList == null) return;
            var items = GetItemsList(collectedItemList);
            if (items == null) return;
            float now = Time.time;
            for (int i = 0; i < PendingColors.Count; i++)
            {
                var p = PendingColors[i];
                if (now - p.AddedAt > PendingColorWindowSeconds) continue;
                int idx = IndexOfData(items, p.DataRef);
                if (idx < 0) continue;
                ApplyColorToEntryAt(collectedItemList, items, idx, p.Color);
            }
            int n = 0;
            for (int i = PendingBundleColors.Count - 1; i >= 0; i--)
            {
                if (now - PendingBundleColors[i].AddedAt <= PendingColorWindowSeconds) n++;
                else break;
            }
            if (n > 0)
            {
                int maxRows = System.Math.Min(n, items.Count);
                maxRows = System.Math.Min(maxRows, PendingBundleColors.Count);
                for (int i = 0; i < maxRows; i++)
                {
                    int colorIdx = PendingBundleColors.Count - 1 - i;
                    if (colorIdx < 0 || colorIdx >= PendingBundleColors.Count)
                    {
                        break;
                    }

                    ApplyColorToEntryAt(collectedItemList, items, i, PendingBundleColors[colorIdx].Color);
                }
            }
        }

        private static void PrunePendingColors()
        {
            float now = Time.time;
            for (int i = PendingColors.Count - 1; i >= 0; i--)
            {
                if (now - PendingColors[i].AddedAt > PendingColorWindowSeconds)
                    PendingColors.RemoveAt(i);
            }
            for (int i = 0; i < PendingBundleColors.Count; )
            {
                if (now - PendingBundleColors[i].AddedAt > PendingColorWindowSeconds)
                    PendingBundleColors.RemoveAt(i);
                else
                    i++;
            }
        }

        private static System.Collections.IList GetItemsList(object collectedItemList)
        {
            if (collectedItemList == null) return null;
            try
            {
                var listType = collectedItemList.GetType();
                foreach (var name in new[] { "items", "Items" })
                {
                    var prop = listType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop == null) continue;
                    var val = prop.GetValue(collectedItemList);
                    if (val is System.Collections.IList list && list.Count > 0) return list;
                }
                foreach (var f in listType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.FieldType == null || !typeof(System.Collections.IList).IsAssignableFrom(f.FieldType)) continue;
                    var val = f.GetValue(collectedItemList);
                    if (val is System.Collections.IList list && list.Count > 0) return list;
                }
            }
            catch { }
            return null;
        }

        private static int IndexOfData(System.Collections.IList items, object dataRef)
        {
            if (items == null || dataRef == null) return -1;
            for (int i = 0; i < items.Count; i++)
                if (ReferenceEquals(items[i], dataRef)) return i;
            return -1;
        }

        private static void ApplyColorToEntryAt(object collectedItemList, System.Collections.IList items, int index, UnityEngine.Color color)
        {
            if (index < 0 || index >= items.Count) return;
            var entry = items[index];
            if (entry == null) return;
            object itemGo = null;
            var entryType = entry.GetType();
            foreach (var name in new[] { "Item", "item" })
            {
                var f = entryType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) { itemGo = f.GetValue(entry); if (itemGo != null) break; }
                var p = entryType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null) { itemGo = p.GetValue(entry, null); if (itemGo != null) break; }
            }
            if (itemGo != null)
            {
                SetNotificationBackgroundColor(itemGo, color);
                SetLabelColorOnEntry(itemGo, color);
                StripInventoryParentheticalOnEntry(itemGo);
            }
        }

        private static bool TryAddItemStackWithValue(object collectedItemList, ItemValue itemValue, int count)
        {
            try
            {
                if (itemValue == null)
                {
                    return false;
                }

                var stack = new ItemStack(itemValue, count);

                if (_addItemStackMethod == null)
                {
                    _addItemStackMethod = collectedItemList.GetType().GetMethod(
                        "AddItemStack",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(ItemStack), typeof(bool) },
                        null);
                }

                if (_addItemStackMethod != null)
                {
                    _addItemStackMethod.Invoke(collectedItemList, new object[] { stack, false });
                    return true;
                }
            }
            catch
            {
                // Fall back to name lookup or icon notification
            }

            return false;
        }

        private static bool TryAddItemStack(object collectedItemList, string itemClassName, int count)
        {
            try
            {
                var itemValue = GameReflection.GetItemValueByClassName(itemClassName);
                if (itemValue == null)
                {
                    return false;
                }

                var stack = new ItemStack(itemValue, count);

                if (_addItemStackMethod == null)
                {
                    _addItemStackMethod = collectedItemList.GetType().GetMethod(
                        "AddItemStack",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(ItemStack), typeof(bool) },
                        null);
                }

                if (_addItemStackMethod != null)
                {
                    _addItemStackMethod.Invoke(collectedItemList, new object[] { stack, false });
                    return true;
                }
            }
            catch
            {
                // Fall back to icon notification
            }

            return false;
        }

        private static bool TryAddIconNotification(object collectedItemList, string iconName, int count)
        {
            try
            {
                if (_addIconNotificationMethod == null)
                {
                    _addIconNotificationMethod = collectedItemList.GetType().GetMethod(
                        "AddIconNotification",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(string), typeof(int), typeof(bool) },
                        null);
                }

                if (_addIconNotificationMethod != null)
                {
                    _addIconNotificationMethod.Invoke(collectedItemList, new object[] { iconName, count, false });
                    return true;
                }
            }
            catch
            {
                // Ignore; visualization is non-critical
            }

            return false;
        }

        /// <summary>
        /// Removes the "(0)" (inventory total) from the newly added entry's label(s); uses source to pick prepend vs append.
        /// </summary>
        private static void TryClearLastEntryCountText(object collectedItemList, AutoReadVisualizationSource source)
        {
            try
            {
                object entry = GetNewlyAddedEntryData(collectedItemList, source);
                if (entry == null) return;

                object itemGo = null;
                var entryType = entry.GetType();
                foreach (var name in new[] { "Item", "item" })
                {
                    var f = entryType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null) { itemGo = f.GetValue(entry); if (itemGo != null) break; }
                    var p = entryType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null) { itemGo = p.GetValue(entry, null); if (itemGo != null) break; }
                }
                if (itemGo == null) return;

                StripInventoryParentheticalOnEntry(itemGo);
            }
            catch
            {
                // Non-critical
            }
        }

        private static float _lastCountTextLogTime = -999f;
        private const float CountTextLogIntervalSeconds = 5f;

        private static void StripTrailingInventoryParenthetical(object component)
        {
            if (component == null) return;
            var t = component.GetType();
            var textProp = t.GetProperty("text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (textProp == null || textProp.PropertyType != typeof(string)) return;
            try
            {
                var current = textProp.GetValue(component, null) as string;
                if (string.IsNullOrEmpty(current)) return;

                string result = current;
                if (System.Text.RegularExpressions.Regex.IsMatch(current, @"^\s*\(\d+\)\s*$"))
                    result = "";
                else
                    result = System.Text.RegularExpressions.Regex.Replace(current, @"\s*\(\d+\)\s*$", "");

                if (result != current)
                {
                    textProp.SetValue(component, result, null);
                    if (ModConfig.Instance.DebugMode && (UnityEngine.Time.time - _lastCountTextLogTime) >= CountTextLogIntervalSeconds)
                    {
                        _lastCountTextLogTime = UnityEngine.Time.time;
                        try { UnityEngine.Debug.Log($"[AutoReadMod] Count text strip: before '{current}' after '{result}'"); } catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Re-applies the "(0)" strip on all UILabels under the entry (e.g. when game formatter overwrites text).
        /// </summary>
        private static void StripInventoryParentheticalOnEntry(object entryGameObject)
        {
            if (entryGameObject == null) return;
            try
            {
                var goType = entryGameObject.GetType();
                var getComponentMethod = goType.GetMethod("GetComponent", new[] { typeof(string) });
                if (getComponentMethod != null)
                {
                    var labelComponent = getComponentMethod.Invoke(entryGameObject, new object[] { "UILabel" });
                    if (labelComponent != null)
                        StripTrailingInventoryParenthetical(labelComponent);
                }
                var transformProp = goType.GetProperty("transform", BindingFlags.Instance | BindingFlags.Public);
                if (transformProp == null) return;
                var transform = transformProp.GetValue(entryGameObject, null);
                if (transform == null) return;
                var transformType = transform.GetType();
                var getChildMethod = transformType.GetMethod("GetChild", new[] { typeof(int) });
                var childCountProp = transformType.GetProperty("childCount", BindingFlags.Instance | BindingFlags.Public);
                if (getChildMethod == null || childCountProp == null) return;
                int childCount = (int)childCountProp.GetValue(transform, null);
                for (int i = 0; i < childCount; i++)
                {
                    var child = getChildMethod.Invoke(transform, new object[] { i });
                    if (child != null)
                        StripInventoryParentheticalOnEntry(child);
                }
            }
            catch { }
        }

        /// <summary>
        /// Sets UILabel.color on all labels under the entry GameObject so notification text shows type-based color.
        /// </summary>
        private static void SetLabelColorOnEntry(object entryGameObject, UnityEngine.Color color)
        {
            if (entryGameObject == null) return;
            try
            {
                var goType = entryGameObject.GetType();
                var getComponentMethod = goType.GetMethod("GetComponent", new[] { typeof(string) });
                if (getComponentMethod != null)
                {
                    var labelComponent = getComponentMethod.Invoke(entryGameObject, new object[] { "UILabel" });
                    if (labelComponent != null)
                        TrySetLabelColor(labelComponent, color);
                }

                var transformProp = goType.GetProperty("transform", BindingFlags.Instance | BindingFlags.Public);
                if (transformProp == null) return;
                var transform = transformProp.GetValue(entryGameObject, null);
                if (transform == null) return;
                var transformType = transform.GetType();
                var getChildMethod = transformType.GetMethod("GetChild", new[] { typeof(int) });
                var childCountProp = transformType.GetProperty("childCount", BindingFlags.Instance | BindingFlags.Public);
                if (getChildMethod == null || childCountProp == null) return;
                int childCount = (int)childCountProp.GetValue(transform, null);
                for (int i = 0; i < childCount; i++)
                {
                    var child = getChildMethod.Invoke(transform, new object[] { i });
                    if (child != null)
                        SetLabelColorOnEntry(child, color);
                }
            }
            catch { }
        }

        private static void TrySetLabelColor(object labelComponent, UnityEngine.Color color)
        {
            if (labelComponent == null) return;
            try
            {
                var t = labelComponent.GetType();
                foreach (var name in new[] { "color", "mColor" })
                {
                    var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.PropertyType == typeof(UnityEngine.Color))
                    {
                        prop.SetValue(labelComponent, color, null);
                        return;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Sets the notification row background color (first UISprite), not the item icon, so the tint is more noticeable.
        /// </summary>
        private static bool TrySetLastEntryColor(object collectedItemList, object lastEntry, UnityEngine.Color color, AutoReader.ReadableItemKind kind)
        {
            if (kind == AutoReader.ReadableItemKind.None || lastEntry == null)
                return false;
            try
            {
                object itemGo = null;
                var entryType = lastEntry.GetType();
                foreach (var name in new[] { "Item", "item", "View", "view", "GameObject" })
                {
                    var f = entryType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null) { var val = f.GetValue(lastEntry); if (val != null) { itemGo = val; break; } }
                    var p = entryType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null) { var val = p.GetValue(lastEntry, null); if (val != null) { itemGo = val; break; } }
                }
                if (itemGo == null) return false;

                bool ok = SetNotificationBackgroundColor(itemGo, color);
                SetLabelColorOnEntry(itemGo, color);
                return ok;
            }
            catch { return false; }
        }

        /// <summary>
        /// Sets color on the largest UISprite (background) and tints the smallest UISprite (icon) by kind so type is visible.
        /// The game often only respects alpha on the background, so we also tint the icon for hue.
        /// </summary>
        private static bool SetNotificationBackgroundColor(object go, UnityEngine.Color color)
        {
            if (go == null) return false;
            object largestSprite = null;
            object smallestSprite = null;
            float largestArea = 0f;
            float smallestArea = float.MaxValue;
            CollectLargestAndSmallestUISprite(go, ref largestSprite, ref largestArea, ref smallestSprite, ref smallestArea);
            bool ok = false;
            if (largestSprite != null)
                ok = TrySetColorOnSprite(largestSprite, color);
            if (!ok)
                ok = TrySetColorOnSprite(go, color);
            if (smallestSprite != null && smallestSprite != largestSprite)
            {
                float r = color.r * 1.2f; if (r > 1f) r = 1f; else if (r < 0f) r = 0f;
                float g = color.g * 1.2f; if (g > 1f) g = 1f; else if (g < 0f) g = 0f;
                float b = color.b * 1.2f; if (b > 1f) b = 1f; else if (b < 0f) b = 0f;
                var iconTint = new UnityEngine.Color(r, g, b, 1f);
                TrySetColorOnSprite(smallestSprite, iconTint);
            }
            return ok;
        }

        private static void CollectLargestAndSmallestUISprite(object go, ref object largestSprite, ref float largestArea, ref object smallestSprite, ref float smallestArea)
        {
            if (go == null) return;
            float area = GetUISpriteArea(go);
            if (area > 0)
            {
                if (area > largestArea)
                {
                    largestArea = area;
                    largestSprite = go;
                }
                if (area < smallestArea)
                {
                    smallestArea = area;
                    smallestSprite = go;
                }
            }
            try
            {
                var goType = go.GetType();
                var transformProp = goType.GetProperty("transform", BindingFlags.Instance | BindingFlags.Public);
                if (transformProp == null) return;
                var transform = transformProp.GetValue(go, null);
                if (transform == null) return;
                var transformType = transform.GetType();
                var getChildMethod = transformType.GetMethod("GetChild", new[] { typeof(int) });
                if (getChildMethod == null) return;
                var childCountProp = transformType.GetProperty("childCount", BindingFlags.Instance | BindingFlags.Public);
                int childCount = childCountProp != null ? (int)childCountProp.GetValue(transform, null) : 0;
                for (int i = 0; i < childCount; i++)
                {
                    var child = getChildMethod.Invoke(transform, new object[] { i });
                    if (child != null) CollectLargestAndSmallestUISprite(child, ref largestSprite, ref largestArea, ref smallestSprite, ref smallestArea);
                }
            }
            catch { }
        }

        private static float GetUISpriteArea(object obj)
        {
            try
            {
                var objType = obj.GetType();
                var getComponentMethod = objType.GetMethod("GetComponent", new[] { typeof(string) });
                if (getComponentMethod == null) return 0f;
                var component = getComponentMethod.Invoke(obj, new object[] { "UISprite" });
                if (component == null) return 0f;
                var ct = component.GetType();
                float w = GetNumericMember(ct, component, "width");
                float h = GetNumericMember(ct, component, "height");
                if (w > 0 && h > 0) return w * h;
                return 0f;
            }
            catch { }
            return 0f;
        }

        private static float GetNumericMember(System.Type type, object instance, string name)
        {
            if (type == null || instance == null) return 0f;
            var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                var v = f.GetValue(instance);
                if (v is int i) return i;
                if (v is float x) return x;
            }
            var p = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.GetIndexParameters().Length == 0)
            {
                var v = p.GetValue(instance, null);
                if (v is int i) return i;
                if (v is float x) return x;
            }
            return 0f;
        }

        private static bool TrySetColorOnSprite(object obj, UnityEngine.Color color)
        {
            if (obj == null) return false;
            try
            {
                var objType = obj.GetType();
                var getComponentMethod = objType.GetMethod("GetComponent", new[] { typeof(string) });
                if (getComponentMethod == null) return false;

                var component = getComponentMethod.Invoke(obj, new object[] { "UISprite" });
                if (component == null) return false;

                var ct = component.GetType();
                var colorProp = ct.GetProperty("color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (colorProp == null) return false;

                colorProp.SetValue(component, color, null);

                var defaultColorProp = ct.GetProperty("defaultColor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (defaultColorProp != null)
                    defaultColorProp.SetValue(component, color, null);

                return true;
            }
            catch { }
            return false;
        }

        private static UnityEngine.Color GetColorForKind(AutoReader.ReadableItemKind kind)
        {
            var c = ModConfig.Instance;
            switch (kind)
            {
                case AutoReader.ReadableItemKind.Book: return c.VisualizationColorBook;
                case AutoReader.ReadableItemKind.Magazine: return c.VisualizationColorMagazine;
                case AutoReader.ReadableItemKind.Recipe: return c.VisualizationColorRecipe;
                case AutoReader.ReadableItemKind.Schematic: return c.VisualizationColorSchematic;
                default: return new Color(0.9f, 0.9f, 0.9f, 1f);
            }
        }
    }

    /// <summary>
    /// Per-frame update: runs deferred container processing and drains the visualization queue.
    /// Applied manually in ModApi so we only patch when a valid target method is found (avoids "method null" failure).
    /// </summary>
    internal static class XUiUpdatePatch
    {
        internal static MethodBase FindUpdateTarget()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var empty = System.Array.Empty<Type>();
            var floatArg = new[] { typeof(float) };

            var xuiType = typeof(XUi);
            var m = xuiType.GetMethod("Update", flags, null, empty, null);
            if (m != null) return m;
            m = xuiType.GetMethod("Update", flags, null, floatArg, null);
            if (m != null) return m;
            m = xuiType.GetMethod("OnUpdateDeltaTime", flags, null, empty, null);
            if (m != null) return m;
            m = xuiType.GetMethod("OnUpdateDeltaTime", flags, null, floatArg, null);
            if (m != null) return m;

            foreach (var method in xuiType.GetMethods(flags))
            {
                if (method.Name != null && method.Name.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0
                    && method.GetParameters().Length <= 1)
                    return method;
            }

            var gmType = typeof(GameManager);
            m = gmType.GetMethod("Update", flags, null, empty, null);
            if (m != null) return m;
            m = gmType.GetMethod("Update", flags, null, floatArg, null);
            if (m != null) return m;

            foreach (var method in gmType.GetMethods(flags))
            {
                if (method.Name != null && method.Name.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0
                    && method.GetParameters().Length <= 1)
                    return method;
            }

            return null;
        }

        public static void Postfix(object __instance)
        {
            object xui = __instance is XUi xu ? xu : GameReflection.GetLocalPlayerXUi();
            DeferredContainerTick.TickIfNewUnityFrame();
            AutoReadVisualizationOverlay.OnUpdate(xui);
            ReadableEffectPopupQueue.OnUpdate();
        }
    }
}
