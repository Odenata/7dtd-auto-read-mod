using System;
using System.Collections.Generic;
using System.Reflection;

namespace AutoReadMod
{
    /// <summary>
    /// Builds player-facing effect tooltip text for newly unlocked books / Complete / craft unlocks.
    /// Localization is resolved via <see cref="TryLocalize"/> (injectable for hermetic tests).
    /// </summary>
    internal static class ReadableEffectMessageBuilder
    {
        internal const string YouCanNowCraftPrefix = "You can now craft ";

        /// <summary>Optional test override: key → localized string (null/missing → fall through to game Localization).</summary>
        internal static Func<string, string> LocalizationLookupForTest { get; set; }

        internal static void ClearLocalizationLookupForTests()
        {
            LocalizationLookupForTest = null;
        }

        internal static string BuildBookEffectText(ItemClass itemClass)
        {
            if (itemClass == null)
                return null;

            var descKey = TryGetItemDescriptionKey(itemClass);
            var fromKey = PreferEffectDesc(descKey);
            if (!string.IsNullOrWhiteSpace(fromKey))
                return fromKey.Trim();

            var unlocks = TryGetItemUnlocks(itemClass);
            if (!string.IsNullOrWhiteSpace(unlocks))
            {
                var fromUnlocks = PreferEffectDesc(unlocks.Trim() + "Desc");
                if (!string.IsNullOrWhiteSpace(fromUnlocks))
                    return fromUnlocks.Trim();
            }

            var longDesc = TryLocalize(descKey);
            return ExtractEffectHeuristic(longDesc);
        }

        internal static string BuildCompleteEffectText(string completeProgressionName)
        {
            if (string.IsNullOrWhiteSpace(completeProgressionName))
                return null;

            var name = completeProgressionName.Trim();
            var fromDesc = PreferEffectDesc(name + "Desc");
            if (!string.IsNullOrWhiteSpace(fromDesc))
                return fromDesc.Trim();

            var fromLong = PreferEffectDesc(name + "LongDesc");
            if (!string.IsNullOrWhiteSpace(fromLong))
                return fromLong.Trim();

            return ExtractEffectHeuristic(TryLocalize(name + "LongDesc"));
        }

        internal static string BuildCraftUnlockText(ItemClass itemClass)
        {
            if (itemClass == null)
                return null;

            var craftName = ResolveCraftDisplayName(itemClass);
            if (string.IsNullOrWhiteSpace(craftName))
                return null;

            return YouCanNowCraftPrefix + craftName.Trim();
        }

        /// <summary>
        /// Prefer short *Desc over *LongDesc. If key ends with LongDesc, try Desc twin first.
        /// </summary>
        internal static string PreferEffectDesc(string descriptionOrDescKey)
        {
            if (string.IsNullOrWhiteSpace(descriptionOrDescKey))
                return null;

            var key = descriptionOrDescKey.Trim();
            if (key.EndsWith("LongDesc", StringComparison.OrdinalIgnoreCase))
            {
                var shortKey = key.Substring(0, key.Length - "LongDesc".Length) + "Desc";
                var shortText = TryLocalize(shortKey);
                if (!string.IsNullOrWhiteSpace(shortText) &&
                    !string.Equals(shortText, shortKey, StringComparison.Ordinal))
                {
                    return shortText.Trim();
                }
            }

            if (key.EndsWith("Desc", StringComparison.OrdinalIgnoreCase) &&
                !key.EndsWith("LongDesc", StringComparison.OrdinalIgnoreCase))
            {
                var descText = TryLocalize(key);
                if (!string.IsNullOrWhiteSpace(descText) &&
                    !string.Equals(descText, key, StringComparison.Ordinal))
                {
                    return descText.Trim();
                }
            }

            var text = TryLocalize(key);
            if (string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal))
                return null;

            if (key.EndsWith("LongDesc", StringComparison.OrdinalIgnoreCase))
                return ExtractEffectHeuristic(text) ?? text.Trim();

            return text.Trim();
        }

        /// <summary>
        /// Fallback when only LongDesc-style "Title: flavor. Effect." text is available.
        /// Takes text after the last ": " when present; otherwise returns the full string.
        /// </summary>
        internal static string ExtractEffectHeuristic(string longDesc)
        {
            if (string.IsNullOrWhiteSpace(longDesc))
                return null;

            var text = longDesc.Trim();
            var colon = text.LastIndexOf(": ", StringComparison.Ordinal);
            if (colon >= 0 && colon + 2 < text.Length)
            {
                var after = text.Substring(colon + 2).Trim();
                // Prefer the last sentence when flavor + effect share the same clause after the colon.
                var lastPeriod = after.LastIndexOf(". ", StringComparison.Ordinal);
                if (lastPeriod >= 0 && lastPeriod + 2 < after.Length)
                {
                    var lastSentence = after.Substring(lastPeriod + 2).Trim();
                    if (!string.IsNullOrWhiteSpace(lastSentence))
                        return lastSentence;
                }

                if (!string.IsNullOrWhiteSpace(after))
                    return after;
            }

            return text;
        }

        internal static string StripSchematicOrRecipeSuffix(string localizedReadableName)
        {
            if (string.IsNullOrWhiteSpace(localizedReadableName))
                return null;

            var name = localizedReadableName.Trim();
            if (name.EndsWith(" Schematic", StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - " Schematic".Length).Trim();
            if (name.EndsWith(" Recipe", StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - " Recipe".Length).Trim();
            if (name.EndsWith("Schematic", StringComparison.OrdinalIgnoreCase) && name.Length > "Schematic".Length)
                return name.Substring(0, name.Length - "Schematic".Length).Trim();
            if (name.EndsWith("Recipe", StringComparison.OrdinalIgnoreCase) && name.Length > "Recipe".Length)
                return name.Substring(0, name.Length - "Recipe".Length).Trim();
            return name;
        }

        internal static string ResolveCraftDisplayName(ItemClass itemClass)
        {
            var unlocks = TryGetItemUnlocks(itemClass);
            if (!string.IsNullOrWhiteSpace(unlocks))
            {
                var unlockName = unlocks.Trim();
                var unlockedClass = TryResolveItemClassByName(unlockName);
                if (unlockedClass != null)
                {
                    try
                    {
                        var localized = unlockedClass.GetLocalizedItemName();
                        if (!string.IsNullOrWhiteSpace(localized))
                            return localized.Trim();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                var loc = TryLocalize(unlockName);
                if (!string.IsNullOrWhiteSpace(loc) && !string.Equals(loc, unlockName, StringComparison.Ordinal))
                    return loc.Trim();
            }

            try
            {
                return StripSchematicOrRecipeSuffix(itemClass.GetLocalizedItemName());
            }
            catch
            {
                return StripSchematicOrRecipeSuffix(GameReflection.GetItemClassName(itemClass));
            }
        }

        internal static string TryLocalize(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            var k = key.Trim();
            var test = LocalizationLookupForTest;
            if (test != null)
            {
                try
                {
                    var v = test(k);
                    if (!string.IsNullOrWhiteSpace(v))
                        return v;
                }
                catch
                {
                    // ignore
                }
            }

            try
            {
                var locType = FindLocalizationType();
                if (locType == null)
                    return null;

                var tryGet = locType.GetMethod(
                    "TryGet",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string).MakeByRefType() },
                    null);
                if (tryGet != null)
                {
                    var args = new object[] { k, null };
                    var ok = tryGet.Invoke(null, args) as bool?;
                    if (ok == true && args[1] is string s && !string.IsNullOrWhiteSpace(s))
                        return s;
                }

                var get = locType.GetMethod(
                    "Get",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(bool), typeof(string) },
                    null);
                if (get != null)
                {
                    var s = get.Invoke(null, new object[] { k, true, null }) as string;
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static Type FindLocalizationType()
        {
            try
            {
                return typeof(GameManager).Assembly.GetType("Localization")
                       ?? Type.GetType("Localization, Assembly-CSharp");
            }
            catch
            {
                return null;
            }
        }

        internal static string TryGetItemDescriptionKey(ItemClass itemClass)
        {
            if (itemClass == null)
                return null;

            try
            {
                var m = itemClass.GetType().GetMethod(
                    "GetItemDescriptionKey",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null && m.GetParameters().Length == 0)
                {
                    var v = m.Invoke(itemClass, null) as string;
                    if (!string.IsNullOrWhiteSpace(v))
                        return v.Trim();
                }
            }
            catch
            {
                // ignore
            }

            return GetStringMember(itemClass, "DescriptionKey", "PropDescriptionKey");
        }

        internal static string TryGetItemUnlocks(ItemClass itemClass)
        {
            return GetStringMember(itemClass, "Unlocks", "PropUnlocks");
        }

        private static string GetStringMember(object target, params string[] names)
        {
            if (target == null || names == null)
                return null;

            var t = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in names)
            {
                try
                {
                    var prop = t.GetProperty(name, flags);
                    if (prop != null && prop.PropertyType == typeof(string))
                    {
                        var v = prop.GetValue(target, null) as string;
                        if (!string.IsNullOrWhiteSpace(v))
                            return v.Trim();
                    }

                    var field = t.GetField(name, flags);
                    if (field != null && field.FieldType == typeof(string))
                    {
                        var v = field.GetValue(target) as string;
                        if (!string.IsNullOrWhiteSpace(v))
                            return v.Trim();
                    }
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }

        private static ItemClass TryResolveItemClassByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            try
            {
                var iv = GameReflection.GetItemValueByClassName(name.Trim());
                return GameReflection.GetItemClass(iv);
            }
            catch
            {
                return null;
            }
        }
    }
}
