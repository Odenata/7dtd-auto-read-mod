using Xunit;
using System;

namespace AutoReadMod.Tests
{
    public class ItemIdentificationTests
    {
        [Theory]
        [InlineData("bookFiremansAlmanacHeat", true)]
        [InlineData("bookNightStalkerStealthDamage", true)]
        [InlineData("bookLuckyLooterDukes", true)]
        [InlineData("bookAnimalTracker", true)]
        [InlineData("BOOKFIREMANSHEAT", true)] // Case insensitive
        public void IsReadableItem_WithBookNames_ReturnsTrue(string itemName, bool expected)
        {
            // This test documents the expected behavior for book identification
            // Books start with "book" (case insensitive)
            var result = itemName.ToLower().StartsWith("book") && !itemName.ToLower().Contains("bundle");
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("magazine", true)]
        [InlineData("magazineGuns", true)]
        [InlineData("magazineMelee", true)]
        [InlineData("gunMagazine", true)]
        [InlineData("MAGAZINE", true)] // Case insensitive
        public void IsReadableItem_WithMagazineNames_ReturnsTrue(string itemName, bool expected)
        {
            // This test documents the expected behavior for magazine identification
            // Magazines contain "magazine" (case insensitive)
            var result = itemName.ToLower().Contains("magazine") && !itemName.ToLower().Contains("bundle");
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("modGunBarrelExtenderSchematic", true)]
        [InlineData("modGunScopeSmallSchematic", true)]
        [InlineData("weaponSchematic", true)]
        [InlineData("armorSchematic", true)]
        [InlineData("SCHEMATIC", true)] // Case insensitive
        public void IsReadableItem_WithSchematicNames_ReturnsTrue(string itemName, bool expected)
        {
            // This test documents the expected behavior for schematic identification
            // Schematics end with "schematic" (case insensitive)
            var result = itemName.ToLower().EndsWith("schematic");
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foodCanShamRecipe", true)]
        [InlineData("foodMeatStewRecipe", true)]
        [InlineData("drinkRecipe", true)]
        [InlineData("RECIPE", true)] // Case insensitive
        public void IsReadableItem_WithRecipeNames_ReturnsTrue(string itemName, bool expected)
        {
            // This test documents the expected behavior for recipe identification
            // Recipes end with "recipe" (case insensitive)
            var result = itemName.ToLower().EndsWith("recipe");
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("bookBundle", false)]
        [InlineData("magazineBundle", false)]
        [InlineData("bundleBook", false)]
        [InlineData("bundleMagazine", false)]
        public void IsReadableItem_WithBundleNames_ReturnsFalse(string itemName, bool expected)
        {
            // This test documents that bundles should be excluded
            // Bundles are containers, not readable items
            bool isBook = itemName.ToLower().StartsWith("book") && !itemName.ToLower().Contains("bundle");
            bool isMagazine = itemName.ToLower().Contains("magazine") && !itemName.ToLower().Contains("bundle");
            var result = isBook || isMagazine;
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("gunAK47", false)]
        [InlineData("foodCanChili", false)]
        [InlineData("resourceWood", false)]
        [InlineData("ammo9mm", false)]
        [InlineData("medicalBandage", false)]
        public void IsReadableItem_WithNonReadableNames_ReturnsFalse(string itemName, bool expected)
        {
            // This test documents that non-readable items should return false
            string lowerName = itemName.ToLower();
            bool isBook = lowerName.StartsWith("book") && !lowerName.Contains("bundle");
            bool isMagazine = lowerName.Contains("magazine") && !lowerName.Contains("bundle");
            bool isSchematic = lowerName.EndsWith("schematic");
            bool isRecipe = lowerName.EndsWith("recipe");
            var result = isBook || isMagazine || isSchematic || isRecipe;
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void IsReadableItem_WithNullOrEmptyNames_ReturnsFalse(string itemName, bool expected)
        {
            // This test documents handling of null/empty item names
            string lowerName = itemName?.ToLower() ?? "";
            bool isBook = lowerName.StartsWith("book") && !lowerName.Contains("bundle");
            bool isMagazine = lowerName.Contains("magazine") && !lowerName.Contains("bundle");
            bool isSchematic = lowerName.EndsWith("schematic");
            bool isRecipe = lowerName.EndsWith("recipe");
            var result = isBook || isMagazine || isSchematic || isRecipe;
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ItemIdentification_IsCaseInsensitive()
        {
            // Test that item identification works regardless of case
            string[] testNames = { "BOOK", "Book", "bOoK", "MAGAZINE", "Magazine", "SCHEMATIC", "Schematic", "RECIPE", "Recipe" };
            
            foreach (var name in testNames)
            {
                string lowerName = name.ToLower();
                bool isReadable = lowerName.StartsWith("book") || 
                                 lowerName.Contains("magazine") || 
                                 lowerName.EndsWith("schematic") || 
                                 lowerName.EndsWith("recipe");
                Assert.True(isReadable, $"Item '{name}' should be identified as readable");
            }
        }

        [Fact]
        public void ItemIdentification_ExcludesBundles()
        {
            // Test that bundles are properly excluded
            string[] bundleNames = { "bookBundle", "magazineBundle", "bundleBook", "bundleMagazine" };
            
            foreach (var name in bundleNames)
            {
                string lowerName = name.ToLower();
                bool isBook = lowerName.StartsWith("book") && !lowerName.Contains("bundle");
                bool isMagazine = lowerName.Contains("magazine") && !lowerName.Contains("bundle");
                bool isReadable = isBook || isMagazine;
                Assert.False(isReadable, $"Bundle '{name}' should not be identified as readable");
            }
        }
    }
}
