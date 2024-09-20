# ErenshorREL
Erenshor Random Epic Loot Mod based on BepInEx

## Features: Get similar level random loot from enemies!

`This mod uses a file watcher. If the configuration file is not changed with BepInEx Configuration manager, but changed in the file directly, upon file save, it will automatically apply the settings.`
---

To change the mod's settings, edit the file `BepInEx/config/Brumdail.ErenshorREL.cfg` (created after running the game once with this mod) using a text editor.

You can adjust the config values in-game using the [Configuration Manager]https://github.com/BepInEx/BepInEx.ConfigurationManager/releases).


## How it works:
- Get similar level random loot from enemies!
- Configure the chance that common and rare NPCs can drop extra random loot!
- Loot is chosen from an item level range based on the defeated enemy's level.

## How to Install (Beta or Full Erenshor License REQUIRED):

### Recommended:
Click the Install with Mod Manager button

### Manual:
1. Install BepInEx - https://thunderstore.io/c/erenshor/p/BepInEx/BepInExPack/.
2. Download manually, extract the Brumdail-ErenshorREL folder and move into the Erenshor\BepInEx\plugins folder.

## Technical Details
Adds Postfix commands to ItemDatabase.Start() to create lists of items per level
Adds Postfix commands to Character.DoDeath() to insert new random loot


