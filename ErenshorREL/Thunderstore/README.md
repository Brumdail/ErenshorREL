# ErenshorREL
Erenshor Random Epic Loot Mod based on BepInEx.

## Features: Get similar level random loot from bosses, tweak loot tables and more!

`This mod uses a file watcher. If the configuration file is not changed with BepInEx Configuration manager, but changed in the file directly, upon file save, it will automatically apply the settings.`
---

To change the mod's settings, edit the file `BepInEx/config/Brumdail.ErenshorREL.cfg` (created after running the game once with this mod) using a text editor.

You can adjust the config values in-game using the [Configuration Manager]https://github.com/BepInEx/BepInEx.ConfigurationManager/releases).


## How it works:
- Get similar level random loot from bosses, tweak loot tables and more!
- By default, adds 1 random piece of loot when killing a rare NPC that drops an uncommon drop or better
- Loot is chosen from an item level range based on the defeated enemy's level
- Because this mod requires editing the LootTable of NPCs, additional tweaks are available to increase or decrease drop percentages


## How to Install:

### (Recommended for Beta Testers/full Erenshor License):
Click the Install with Mod Manager button

### Manual (Recommended for Demo Users):
1. Install BepInEx - https://thunderstore.io/c/erenshor/p/BepInEx/BepInExPack/.
2. Download manually, extract the Brumdail-ErenshorREL folder and move into the Erenshor\BepInEx\plugins folder.

## Technical Details
Adds Postfix commands to ItemDatabase.Start() to create lists of items per level
Adds Prefix commands to TypeText.CheckCommands() to include new commands


