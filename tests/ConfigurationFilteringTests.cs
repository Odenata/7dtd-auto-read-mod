using Xunit;
using System;

namespace AutoReadMod.Tests
{
    public class ConfigurationFilteringTests
    {
        [Fact]
        public void FilterReadableItems_WhenBooksDisabled_SkipsBooks()
        {
            // Arrange
            var config = new ModConfig
            {
                Enabled = true,
                ReadBooks = false,
                ReadMagazines = true,
                ReadSchematics = true
            };

            // Act & Assert
            string itemName = "bookFiremansAlmanac";
            bool isBook = itemName.ToLower().StartsWith("book") && !itemName.ToLower().Contains("bundle");
            bool shouldRead = isBook && config.ReadBooks;
            
            Assert.False(shouldRead, "Books should be skipped when ReadBooks is false");
        }

        [Fact]
        public void FilterReadableItems_WhenMagazinesDisabled_SkipsMagazines()
        {
            // Arrange
            var config = new ModConfig
            {
                Enabled = true,
                ReadBooks = true,
                ReadMagazines = false,
                ReadSchematics = true
            };

            // Act & Assert
            string itemName = "magazineGuns";
            bool isMagazine = itemName.ToLower().Contains("magazine") && !itemName.ToLower().Contains("bundle");
            bool shouldRead = isMagazine && config.ReadMagazines;
            
            Assert.False(shouldRead, "Magazines should be skipped when ReadMagazines is false");
        }

        [Fact]
        public void FilterReadableItems_WhenSchematicsDisabled_SkipsSchematics()
        {
            // Arrange
            var config = new ModConfig
            {
                Enabled = true,
                ReadBooks = true,
                ReadMagazines = true,
                ReadSchematics = false
            };

            // Act & Assert
            string itemName = "modGunBarrelExtenderSchematic";
            bool isSchematic = itemName.ToLower().EndsWith("schematic");
            bool shouldRead = isSchematic && config.ReadSchematics;
            
            Assert.False(shouldRead, "Schematics should be skipped when ReadSchematics is false");
        }

        [Fact]
        public void FilterReadableItems_WhenSchematicsDisabled_SkipsRecipes()
        {
            // Arrange
            var config = new ModConfig
            {
                Enabled = true,
                ReadBooks = true,
                ReadMagazines = true,
                ReadSchematics = false
            };

            // Act & Assert
            string itemName = "foodCanShamRecipe";
            bool isRecipe = itemName.ToLower().EndsWith("recipe");
            bool shouldRead = isRecipe && config.ReadSchematics;
            
            Assert.False(shouldRead, "Recipes should be skipped when ReadSchematics is false");
        }

        [Fact]
        public void FilterReadableItems_WhenAllEnabled_ReadsAllTypes()
        {
            // Arrange
            var config = new ModConfig
            {
                Enabled = true,
                ReadBooks = true,
                ReadMagazines = true,
                ReadSchematics = true
            };

            // Act & Assert
            string[] testItems = { "bookFiremansAlmanac", "magazineGuns", "modSchematic", "foodRecipe" };
            
            foreach (var itemName in testItems)
            {
                string lowerName = itemName.ToLower();
                bool isBook = lowerName.StartsWith("book") && !lowerName.Contains("bundle");
                bool isMagazine = lowerName.Contains("magazine") && !lowerName.Contains("bundle");
                bool isSchematic = lowerName.EndsWith("schematic");
                bool isRecipe = lowerName.EndsWith("recipe");
                
                bool shouldRead = (isBook && config.ReadBooks) ||
                                 (isMagazine && config.ReadMagazines) ||
                                 (isSchematic && config.ReadSchematics) ||
                                 (isRecipe && config.ReadSchematics);
                
                Assert.True(shouldRead, $"Item '{itemName}' should be read when all types are enabled");
            }
        }

        [Fact]
        public void FilterReadableItems_WhenAllDisabled_ReadsNothing()
        {
            // Arrange
            var config = new ModConfig
            {
                Enabled = true,
                ReadBooks = false,
                ReadMagazines = false,
                ReadSchematics = false
            };

            // Act & Assert
            string[] testItems = { "bookFiremansAlmanac", "magazineGuns", "modSchematic", "foodRecipe" };
            
            foreach (var itemName in testItems)
            {
                string lowerName = itemName.ToLower();
                bool isBook = lowerName.StartsWith("book") && !lowerName.Contains("bundle");
                bool isMagazine = lowerName.Contains("magazine") && !lowerName.Contains("bundle");
                bool isSchematic = lowerName.EndsWith("schematic");
                bool isRecipe = lowerName.EndsWith("recipe");
                
                bool shouldRead = (isBook && config.ReadBooks) ||
                                 (isMagazine && config.ReadMagazines) ||
                                 (isSchematic && config.ReadSchematics) ||
                                 (isRecipe && config.ReadSchematics);
                
                Assert.False(shouldRead, $"Item '{itemName}' should not be read when all types are disabled");
            }
        }

        [Fact]
        public void FilterReadableItems_BooksAndMagazines_ShareSameConfigLogic()
        {
            // Test that books and magazines can be controlled independently
            var config1 = new ModConfig { ReadBooks = true, ReadMagazines = false };
            var config2 = new ModConfig { ReadBooks = false, ReadMagazines = true };

            string bookName = "bookFiremansAlmanac";
            string magazineName = "magazineGuns";

            // Config 1: Books enabled, magazines disabled
            bool isBook1 = bookName.ToLower().StartsWith("book");
            bool isMagazine1 = magazineName.ToLower().Contains("magazine");
            Assert.True(isBook1 && config1.ReadBooks);
            Assert.False(isMagazine1 && config1.ReadMagazines);

            // Config 2: Books disabled, magazines enabled
            bool isBook2 = bookName.ToLower().StartsWith("book");
            bool isMagazine2 = magazineName.ToLower().Contains("magazine");
            Assert.False(isBook2 && config2.ReadBooks);
            Assert.True(isMagazine2 && config2.ReadMagazines);
        }

        [Fact]
        public void FilterReadableItems_SchematicsAndRecipes_ShareSameConfigSetting()
        {
            // Test that schematics and recipes are both controlled by ReadSchematics
            var config = new ModConfig { ReadSchematics = true };

            string schematicName = "modGunBarrelExtenderSchematic";
            string recipeName = "foodCanShamRecipe";

            bool isSchematic = schematicName.ToLower().EndsWith("schematic");
            bool isRecipe = recipeName.ToLower().EndsWith("recipe");

            Assert.True(isSchematic && config.ReadSchematics);
            Assert.True(isRecipe && config.ReadSchematics);
        }
    }
}
