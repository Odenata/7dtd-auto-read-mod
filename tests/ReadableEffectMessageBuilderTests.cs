using System;
using System.Collections.Generic;
using Xunit;

namespace AutoReadMod.Tests
{
    public class ReadableEffectMessageBuilderTests : IDisposable
    {
        public ReadableEffectMessageBuilderTests()
        {
            ReadableEffectMessageBuilder.ClearLocalizationLookupForTests();
            ReadableEffectPopupQueue.ClearForTests();
        }

        public void Dispose()
        {
            ReadableEffectMessageBuilder.ClearLocalizationLookupForTests();
            ReadableEffectPopupQueue.ClearForTests();
        }

        [Fact]
        public void PreferEffectDesc_PrefersShortDescOverLongDesc()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["perkNightStalkerSlumberPartyLongDesc"] =
                    "Slumber Party: It's best to come late to the party. Do an extra 50% sneak damage to sleeping victims at night.",
                ["perkNightStalkerSlumberPartyDesc"] =
                    "Do an extra 50% sneak damage to sleeping victims at night."
            };
            ReadableEffectMessageBuilder.LocalizationLookupForTest = key =>
                map.TryGetValue(key, out var v) ? v : null;

            var text = ReadableEffectMessageBuilder.PreferEffectDesc("perkNightStalkerSlumberPartyLongDesc");
            Assert.Equal("Do an extra 50% sneak damage to sleeping victims at night.", text);
        }

        [Fact]
        public void ExtractEffectHeuristic_TakesLastSentenceAfterColon()
        {
            var longDesc =
                "Slumber Party: It's best to come late to the party. Do an extra 50% sneak damage to sleeping victims at night.";
            var text = ReadableEffectMessageBuilder.ExtractEffectHeuristic(longDesc);
            Assert.Equal("Do an extra 50% sneak damage to sleeping victims at night.", text);
        }

        [Fact]
        public void BuildCompleteEffectText_UsesDescKey()
        {
            ReadableEffectMessageBuilder.LocalizationLookupForTest = key =>
                key == "perkFiremansAlmanacCompleteDesc"
                    ? "Complete the Fireman's Almanac set for a bonus."
                    : null;

            var text = ReadableEffectMessageBuilder.BuildCompleteEffectText("perkFiremansAlmanacComplete");
            Assert.Equal("Complete the Fireman's Almanac set for a bonus.", text);
        }

        [Fact]
        public void BuildCraftUnlockText_StripsSchematicSuffix()
        {
            ReadableEffectMessageBuilder.LocalizationLookupForTest = _ => null;
            var item = new ItemClass { Name = "modGunScopeLargeSchematic" };
            // Without Unlocks/GetLocalizedItemName override beyond Name, craft name strips suffix from Name.
            // GetLocalizedItemName returns Name in mock.
            var text = ReadableEffectMessageBuilder.BuildCraftUnlockText(item);
            Assert.Equal("You can now craft modGunScopeLarge", text);
        }

        [Fact]
        public void BuildCraftUnlockText_StripsRecipeSuffixFromLocalizedName()
        {
            ReadableEffectMessageBuilder.LocalizationLookupForTest = _ => null;
            var item = new ItemClass { Name = "Super Corn (Seed) Recipe" };
            var text = ReadableEffectMessageBuilder.BuildCraftUnlockText(item);
            Assert.Equal("You can now craft Super Corn (Seed)", text);
        }

        [Fact]
        public void StripSchematicOrRecipeSuffix_HandlesVariants()
        {
            Assert.Equal("Scope 8x Mod", ReadableEffectMessageBuilder.StripSchematicOrRecipeSuffix("Scope 8x Mod Schematic"));
            Assert.Equal("Super Corn (seed)", ReadableEffectMessageBuilder.StripSchematicOrRecipeSuffix("Super Corn (seed) Recipe"));
        }

        [Fact]
        public void BuildBookEffectText_UsesDescriptionKeyDescTwin()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["perkNightStalkerSlumberPartyLongDesc"] =
                    "Slumber Party: It's best to come late to the party. Do an extra 50% sneak damage to sleeping victims at night.",
                ["perkNightStalkerSlumberPartyDesc"] =
                    "Do an extra 50% sneak damage to sleeping victims at night."
            };
            ReadableEffectMessageBuilder.LocalizationLookupForTest = key =>
                map.TryGetValue(key, out var v) ? v : null;

            var item = new ItemClassWithDescription
            {
                Name = "bookNightStalkerSlumberParty",
                DescriptionKey = "perkNightStalkerSlumberPartyLongDesc",
                Unlocks = "perkNightStalkerSlumberParty"
            };

            var text = ReadableEffectMessageBuilder.BuildBookEffectText(item);
            Assert.Equal("Do an extra 50% sneak damage to sleeping victims at night.", text);
        }

        [Fact]
        public void PopupQueue_EnqueuesMessages()
        {
            ReadableEffectPopupQueue.ClearForTests();
            ReadableEffectPopupQueue.Enqueue("first");
            ReadableEffectPopupQueue.Enqueue("second");
            Assert.Equal(2, ReadableEffectPopupQueue.PendingCountForTest);
        }

        [Fact]
        public void DefaultConfig_ShowReadEffectsIsTrue()
        {
            Assert.True(new ModConfig().ShowReadEffects);
        }

        [Fact]
        public void ApplyGlobalSettings_UpdatesShowReadEffects()
        {
            var config = new ModConfig();
            config.ApplyGlobalSettings(
                debugMode: false,
                visualizationEnabled: true,
                visualizationMaxVisible: 8,
                colorBook: config.VisualizationColorBook,
                colorMagazine: config.VisualizationColorMagazine,
                colorRecipe: config.VisualizationColorRecipe,
                colorSchematic: config.VisualizationColorSchematic,
                playReadSounds: true,
                playBookSetCompleteSound: true,
                showReadEffects: false);
            Assert.False(config.ShowReadEffects);
        }

        private sealed class ItemClassWithDescription : ItemClass
        {
            public string DescriptionKey { get; set; }
            public string Unlocks { get; set; }

            public string GetItemDescriptionKey() => DescriptionKey;
        }
    }
}
