# Testing Checklist

Use this checklist to validate the mod is working correctly.

## Pre-Testing Setup

- [ ] .NET SDK 8.0+ installed (`dotnet --version`)
- [ ] 7 Days to Die installed at default location
- [ ] No conflicting mods installed

## Build Validation

- [ ] Run `.\tools\build.ps1` - completes without errors
- [ ] DLL created at `src\bin\Release\net6.0\AutoReadMod.dll`
- [ ] Build script shows "Build successful!"
- [ ] Assembly validation shows ModApi class

## Unit Test Validation

- [ ] Run `.\tools\test.ps1` - all tests pass
- [ ] ModApiTests pass
- [ ] AutoReaderTests pass
- [ ] BuildToolTests pass
- [ ] DeployToolTests pass
- [ ] PackageToolTests pass

## Deployment Validation

- [ ] Run `.\tools\deploy.ps1` - completes without errors
- [ ] Mod directory created: `7 Days To Die\Mods\AutoReadMod\`
- [ ] DLL copied to mod directory
- [ ] ModInfo.xml copied to mod directory

## In-Game Testing

### Mod Loading
- [ ] Launch 7 Days to Die
- [ ] Check console log (F1) for "Loading Auto-Read Mod"
- [ ] Check console log for "Auto-Read Mod loaded successfully"
- [ ] No error messages in console

### Functionality Testing

#### Test 1: Book Auto-Reading
- [ ] Start or load a game
- [ ] Find a container with a book (e.g., bookshelf, desk)
- [ ] Open the container
- [ ] Book should disappear from container
- [ ] Check player progression - book should be read
- [ ] Console shows "Auto-Read: [player] read book: [name]"

#### Test 2: Magazine Auto-Reading
- [ ] Find a container with a magazine
- [ ] Open the container
- [ ] Magazine should disappear
- [ ] Check player progression - magazine should be read
- [ ] Console shows auto-read message

#### Test 3: Schematic Auto-Reading
- [ ] Find a container with a schematic/recipe
- [ ] Open the container
- [ ] Schematic should disappear
- [ ] Check crafting menu - recipe should be unlocked
- [ ] Console shows "Auto-Read: [player] learned recipe: [name]"

#### Test 4: Multiple Items
- [ ] Find container with multiple readable items
- [ ] Open the container
- [ ] All readable items should be auto-read
- [ ] Multiple console messages appear

#### Test 5: Mixed Container
- [ ] Find container with readable and non-readable items
- [ ] Open the container
- [ ] Only readable items disappear
- [ ] Non-readable items remain in container

#### Test 6: Empty Container
- [ ] Open an empty container
- [ ] No errors occur
- [ ] Game continues normally

#### Test 7: Already-Read Items
- [ ] Find a book you've already read
- [ ] Open container with that book
- [ ] Book should still be auto-read (removed)
- [ ] No progression change (already known)

#### Test 8: Configuration - Disable Mod
- [ ] Edit Config.xml, set `<Enabled>false</Enabled>`
- [ ] Restart game
- [ ] Open container with readable items
- [ ] Items should NOT be auto-read
- [ ] Items remain in container

#### Test 9: Configuration - Disable Books Only
- [ ] Edit Config.xml, set `<Books>false</Books>`
- [ ] Restart game
- [ ] Open container with books and magazines
- [ ] Books should remain, magazines should be auto-read

#### Test 10: Configuration - Enable All
- [ ] Edit Config.xml, set all to `true`
- [ ] Restart game
- [ ] All readable items should be auto-read

### Multiplayer Testing (if applicable)
- [ ] Test in multiplayer server
- [ ] Each player auto-reads their own items
- [ ] Items cannot be shared between players
- [ ] No synchronization issues

## Package Validation

- [ ] Run `.\tools\package.ps1` - completes without errors
- [ ] ZIP file created in `dist\` directory
- [ ] ZIP filename includes version number
- [ ] ZIP contains: DLL, ModInfo.xml, README.md, COMPATIBILITY.md
- [ ] Extract ZIP and verify structure

## Regression Testing

After any code changes:
- [ ] Re-run all unit tests
- [ ] Re-build and re-deploy
- [ ] Re-test core functionality in-game
- [ ] Check for new errors in console

## Performance Testing

- [ ] Open container with 50+ items - no lag
- [ ] Open multiple containers rapidly - no crashes
- [ ] Play for 30+ minutes - no memory leaks
- [ ] Check game performance (FPS) - no degradation

## Compatibility Testing

- [ ] Test with game version A21.x
- [ ] Update COMPATIBILITY.md with test results
- [ ] Note any issues or limitations

## Known Issues to Watch For

- [ ] Null reference exceptions in console
- [ ] Items not being removed
- [ ] Progression not updating
- [ ] Mod not loading at all
- [ ] Conflicts with other mods

## Sign-Off

- [ ] All critical tests passed
- [ ] No blocking issues found
- [ ] Documentation updated
- [ ] Ready for distribution

---

## Test Results Template

```
Test Date: ___________
Game Version: ___________
Mod Version: ___________
Tester: ___________

Build: ☐ Pass ☐ Fail
Unit Tests: ☐ Pass ☐ Fail
Deployment: ☐ Pass ☐ Fail
In-Game Loading: ☐ Pass ☐ Fail
Book Reading: ☐ Pass ☐ Fail
Magazine Reading: ☐ Pass ☐ Fail
Schematic Reading: ☐ Pass ☐ Fail
Multiple Items: ☐ Pass ☐ Fail
Mixed Container: ☐ Pass ☐ Fail
Empty Container: ☐ Pass ☐ Fail
Performance: ☐ Pass ☐ Fail

Notes:
_________________________________
_________________________________
_________________________________
```
