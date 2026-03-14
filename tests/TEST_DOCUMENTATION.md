# Unit Test Documentation

**Last Updated:** 2026-03-11  
**Test Framework:** xUnit  
**Build System:** Bazel

---

## Test Coverage Overview

### AutoReaderTests.cs
**Purpose:** Test the main container processing logic and null safety

**Test Cases:**
- `ProcessContainer_WhenModDisabled_ReturnsEarly` - Verifies mod respects Enabled config
- `ProcessContainer_WithNullPlayer_DoesNotThrow` - Null safety for player parameter
- `ProcessContainer_WithNullContainer_DoesNotThrow` - Null safety for container parameter
- `ProcessContainer_WithEmptyContainer_DoesNotThrow` - Handles empty containers gracefully
- `ProcessContainer_WithNullItemsArray_DoesNotThrow` - Handles null items array
- `ProcessContainer_WithEmptyItemStacks_DoesNotThrow` - Handles empty item stacks

**Coverage:**
- ✅ Null safety
- ✅ Configuration respect
- ✅ Empty container handling
- ⚠️ Limited: Cannot fully test item reading without game DLLs

---

### ModConfigTests.cs
**Purpose:** Test configuration loading, defaults, and property management

**Test Cases:**
- `Instance_ReturnsNonNull` - Singleton instance is never null
- `DefaultConfig_EnabledIsTrue` - Default enabled state
- `DefaultConfig_ReadBooksIsTrue` - Default book reading enabled
- `DefaultConfig_ReadMagazinesIsTrue` - Default magazine reading enabled
- `DefaultConfig_ReadSchematicsIsTrue` - Default schematic reading enabled
- `DefaultConfig_DebugModeIsFalse` - Default debug mode disabled
- `Reload_RefreshesInstance` - Config can be reloaded
- `ModConfig_CanSetProperties` - All properties are settable
- `ModConfig_DefaultConstructor_SetsDefaults` - Constructor sets correct defaults

**Coverage:**
- ✅ Default values
- ✅ Property setters
- ✅ Singleton pattern
- ✅ Reload functionality
- ⚠️ Limited: Cannot test XML parsing without file system access

---

### ItemIdentificationTests.cs
**Purpose:** Test item name-based identification logic

**Test Cases:**
- `IsReadableItem_WithBookNames_ReturnsTrue` - Books identified by "book" prefix
- `IsReadableItem_WithMagazineNames_ReturnsTrue` - Magazines identified by "magazine" substring
- `IsReadableItem_WithSchematicNames_ReturnsTrue` - Schematics identified by "schematic" suffix
- `IsReadableItem_WithRecipeNames_ReturnsTrue` - Recipes identified by "recipe" suffix
- `IsReadableItem_WithBundleNames_ReturnsFalse` - Bundles excluded from reading
- `IsReadableItem_WithNonReadableNames_ReturnsFalse` - Non-readable items rejected
- `IsReadableItem_WithNullOrEmptyNames_ReturnsFalse` - Null/empty names handled
- `ItemIdentification_IsCaseInsensitive` - Case-insensitive matching
- `ItemIdentification_ExcludesBundles` - Bundle exclusion logic

**Coverage:**
- ✅ Book identification (starts with "book")
- ✅ Magazine identification (contains "magazine")
- ✅ Schematic identification (ends with "schematic")
- ✅ Recipe identification (ends with "recipe")
- ✅ Bundle exclusion (contains "bundle")
- ✅ Case insensitivity
- ✅ Null/empty handling

**Item Name Patterns:**
```
Books:      book*           (e.g., bookFiremansAlmanac)
Magazines:  *magazine*      (e.g., magazineGuns, gunMagazine)
Schematics: *schematic      (e.g., modGunBarrelExtenderSchematic)
Recipes:    *recipe         (e.g., foodCanShamRecipe)
Bundles:    *bundle*        (EXCLUDED - containers, not readable)
```

---

### ConfigurationFilteringTests.cs
**Purpose:** Test configuration-based item filtering logic

**Test Cases:**
- `FilterReadableItems_WhenBooksDisabled_SkipsBooks` - Books skipped when disabled
- `FilterReadableItems_WhenMagazinesDisabled_SkipsMagazines` - Magazines skipped when disabled
- `FilterReadableItems_WhenSchematicsDisabled_SkipsSchematics` - Schematics skipped when disabled
- `FilterReadableItems_WhenSchematicsDisabled_SkipsRecipes` - Recipes skipped when disabled
- `FilterReadableItems_WhenAllEnabled_ReadsAllTypes` - All types read when enabled
- `FilterReadableItems_WhenAllDisabled_ReadsNothing` - Nothing read when all disabled
- `FilterReadableItems_BooksAndMagazines_ShareSameConfigLogic` - Books/magazines independent
- `FilterReadableItems_SchematicsAndRecipes_ShareSameConfigSetting` - Schematics/recipes share setting

**Coverage:**
- ✅ ReadBooks configuration
- ✅ ReadMagazines configuration
- ✅ ReadSchematics configuration (covers recipes too)
- ✅ All enabled scenario
- ✅ All disabled scenario
- ✅ Independent control of books vs magazines
- ✅ Shared control of schematics and recipes

**Configuration Matrix:**
```
ReadBooks    ReadMagazines    ReadSchematics    Result
---------    -------------    --------------    ------
true         true             true              All items read
false        false            false             No items read
true         false            false             Only books read
false        true             false             Only magazines read
false        false            true              Only schematics/recipes read
```

---

### DebugLoggingTests.cs
**Purpose:** Test debug logging functionality and DebugMode configuration

**Test Cases:**
- `DebugLog_WhenDebugModeDisabled_DoesNotLog` - Debug logs suppressed when disabled
- `DebugLog_WhenDebugModeEnabled_Logs` - Debug logs shown when enabled
- `SafeLog_AlwaysLogs_RegardlessOfDebugMode` - User messages always shown
- `DebugMode_DefaultValue_IsFalse` - Debug mode defaults to off
- `DebugMode_CanBeEnabled` - Debug mode can be enabled
- `DebugMode_CanBeDisabled` - Debug mode can be disabled
- `DebugMode_CanBeToggled` - Debug mode can be toggled
- `DebugMode_WorksWithBothValues` - Both true/false work correctly

**Coverage:**
- ✅ DebugMode default value
- ✅ DebugMode property setter
- ✅ DebugLog behavior when enabled
- ✅ DebugLog behavior when disabled
- ✅ SafeLog always logs
- ✅ Toggle functionality

**Logging Strategy:**
```
SafeLog()   - Always logs (user-facing messages)
            - Example: "Auto-Read: reading 3x Book of Firearms"
            
DebugLog()  - Only logs when DebugMode=true (developer messages)
            - Example: "[DEBUG] Found ItemActionEat, will consume 3 items"
            - Example: "[DEBUG] Successfully consumed item 1/3"
```

---

## Running Tests

### Run All Tests
```bash
bazel test //tests:all
```

### Run Specific Test Suite
```bash
bazel test //tests:AutoReaderTests
bazel test //tests:ModConfigTests
bazel test //tests:ItemIdentificationTests
bazel test //tests:ConfigurationFilteringTests
bazel test //tests:DebugLoggingTests
```

### Run with Verbose Output
```bash
bazel test //tests:all --test_output=all
```

---

## Known Workaround

The `ItemActionEat.consume()` call in `AutoReader.ReadItem` is wrapped in a try-catch that increments `successfulConsumes` even on exception ("effects might have been applied before the error"). This masks partial failures. See [docs/DEVELOPMENT_STATUS.md](../docs/DEVELOPMENT_STATUS.md) for details and exploration of healthier alternatives.

---

## Test Limitations

### Current Limitations

1. **Game DLL Dependencies**
   - Cannot fully test item reading logic without game DLLs
   - Mock objects have limited functionality
   - ItemActionEat.consume() cannot be tested in unit tests

2. **File System Access**
   - XML config parsing not tested (requires file system)
   - Config file creation not tested
   - Default config generation not tested

3. **Unity Engine Dependencies**
   - SafeLog uses UnityEngine.Debug.Log (not available in tests)
   - Tests rely on try-catch to suppress Unity errors

### Integration Test Coverage

The following scenarios require integration tests with the actual game:

- ✅ Item consumption and effect application (tested manually)
- ✅ Item removal from containers (tested manually)
- ✅ Stacked item handling (tested manually)
- ✅ Player buff application (tested manually)
- ✅ Recipe/schematic learning (tested manually)
- ⚠️ XML config loading (not yet tested)
- ⚠️ Config file creation (not yet tested)

---

## Test Strategy

### Unit Tests (Current)
- Test business logic without game dependencies
- Test configuration management
- Test item identification patterns
- Test filtering logic
- Test null safety and edge cases

### Integration Tests (Manual)
- Test with actual game DLLs
- Test in-game functionality
- Test XML config loading
- Test player interaction
- Test container interaction

### Future Improvements
1. Add mock framework for game DLLs
2. Add file system mocking for config tests
3. Add integration test automation
4. Add performance tests
5. Add stress tests (large containers, many items)

---

## Test Maintenance

### When Adding New Features
1. Add unit tests for business logic
2. Add integration tests for game interaction
3. Update this documentation
4. Update test coverage matrix

### When Fixing Bugs
1. Add regression test for the bug
2. Verify fix with unit test
3. Verify fix with integration test
4. Document the bug and fix

### When Refactoring
1. Ensure all existing tests pass
2. Add tests for new code paths
3. Remove obsolete tests
4. Update test documentation

---

## Code Coverage Goals

### Current Coverage (Estimated)
- ModConfig: ~70% (limited by file system dependencies)
- AutoReader: ~40% (limited by game DLL dependencies)
- Item Identification: ~95% (well-tested logic)
- Configuration Filtering: ~95% (well-tested logic)
- Debug Logging: ~90% (well-tested logic)

### Target Coverage
- Business Logic: 90%+
- Configuration: 80%+
- Integration Points: 60%+ (limited by external dependencies)

---

## Related Documentation

- [README.md](../README.md) - Project overview and build instructions
- [docs/PROJECT_OVERVIEW.md](../docs/PROJECT_OVERVIEW.md) - Project context and goals
- [docs/DESIGN.md](../docs/DESIGN.md) - Design notes
- [docs/DEVELOPMENT_STATUS.md](../docs/DEVELOPMENT_STATUS.md) - Current progress and known workarounds
