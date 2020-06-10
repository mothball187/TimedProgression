# TimedProgression

`TimedProgression` is a uMod plugin for Rust servers that restricts configured weapons, armor, and items and unlocks them over configured intervals of time called "phases". 

This effectively time gates progression on a Rust server, which is a desirable effect for players who only have a limited amount of time to play in a day but want fair competition. 

This plugin helps create a level playing field for players even if some players have more time to play than others, and extends the lifetime of each tier of combat. 

The [WorkingMan Plugin](https://github.com/pilate/WorkingMan), which restricts the amount of time players can play on your server, makes a great companion to this plugin.


# Example Setup

The default configuration for this plugin only allows tier 0 and tier 1 weapons and armor for the first two days of a server wipe, called "phase 1". 

This means a player will never find tier 2 or tier 3 weapons or armor in crates, even elite crates. The player will also not be able to craft a tier 2 workbench or any tier 2 items, nor will they be able to purchase end tier weapons from the bandit camp.

If a player opens loot that would have contained a tier 2 or tier 3 item, it is replaced with an item of the highest tier available for the current phase, of the same category. For example, if you were to loot a thompson in phase 1, you might instead loot a revolver or a double barrel shotgun.

After two days, phase 2 begins which unlocks tier 2 weapons and armor. All loot is refreshed and from now on, loot can contain tier 2 items such as road plate armor, custom smgs, etc. The player can also now craft the tier 2 workbench and begin researching and crafting tier 2 items.

After two more days, phase 3 begins which unlocks all restricted items. At this time, you can now craft the tier 3 workbench, research and craft tier 3 items, and purchase weapons from the bandit camp as well.

# Configuration

The configuration for this plugin is divided into the configuration file, which is fairly simple, and a data file named `items.json`.

The configuration file contains the following settings:

`thresholds` - An array of integers representing the number of minutes from the start of wipe before the next phase begins. The default values are `2880` and `5760`, which are 2 days, and 4 days, respectively. This means phase 2 begins after two days from wipe, and phase 3 after 4 days from wipe. You can configure more than two thresholds to add more phases to your wipe period.

`botChannel` - The name of your Discord channel for the bot to listen and chat in, defaults to "bots" (requires [Discord Core](https://umod.org/plugins/discord-core)).

The `items.json` file contains the more complex details of which items are restricted and their unlock phase. Below is the default configuration.
```
items["Weapon", "pistol.revolver"] = 1;
items["Weapon", "shotgun.double"] = 1;
items["Weapon", "pistol.m92"] = 3;
items["Weapon", "pistol.python"] = 2;
items["Weapon", "pistol.semiauto"] = 2;
items["Weapon", "rifle.ak"] = 3;
items["Weapon", "rifle.bolt"] = 3;
items["Weapon", "rifle.l96"] = 3;
items["Weapon", "rifle.lr300"] = 3;
items["Weapon", "rifle.m39"] = 3;
items["Weapon", "rifle.semiauto"] = 2;
items["Weapon", "shotgun.pump"] = 2;
items["Weapon", "shotgun.spas12"] = 3;
items["Weapon", "smg.2"] = 2;
items["Weapon", "smg.mp5"] = 3;
items["Weapon", "smg.thompson"] = 2;
items["Weapon", "lmg.m249"] = 3;
items["Weapon", "rocket.launcher"] = 2;
items["Weapon", "multiplegrenadelauncher"] = 2;

items["Attire", "wood.armor.jacket"] = 1;
items["Attire", "wood.armor.pants"] = 1;
items["Attire", "wood.armor.helmet"] = 1;
items["Attire", "roadsign.gloves"] = 2;
items["Attire", "roadsign.jacket"] = 2;
items["Attire", "coffeecan.helmet"] = 2;
items["Attire", "roadsign.kilt"] = 2;
items["Attire", "metal.facemask"] = 3;
items["Attire", "metal.plate.torso"] = 3;

items["Items", "workbench2"] = 2;
items["Items", "workbench3"] = 3;

items["Tool", "explosive.satchel"] = 1;
items["Tool", "explosive.timed"] = 3;

items["HeavyAmmo", "ammo.rocket.hv"] = 1;
items["HeavyAmmo", "ammo.rocket.basic"] = 3;
items["HeavyAmmo", "ammo.grenadelauncher.he"] = 3;
```

For example, the thompson (`smg.thompson`) is unlocked in phase 2, which is reached after two days with the default configuration. Be sure to include phase 1 items for each category, so the plugin will know which items can be used to replace restricted items during phase 1. For a complete list of item names, see the [uMod Rust Definitions page](https://umod.org/documentation/games/rust/definitions).

**Note:** A custom category not part of Rust's regular categories is added by this plugin named `HeavyAmmo` to account for rockets and grenades. This is because rockets and grenades do not come in the same quantities as other ammo types. If you accidentally put a rocket or grenade item under the `Ammunition` category in your `items.json` file, it will be moved to the `HeavyAmmo` category for you automatically.

# Player Commands

`/checkphase` - Informs the player of the current phase and time until next phase begins.

`/listitems` - Shows which items will unlock next phase.

# Console Commands

In order to use these console commands, the user must have the Oxide permission `timedprogression.configure`.

`timedprogression.setthreshold <phase num> <threshold minutes>` - Set the threshold for phase `<phase num>` to `<minutes>`.

`timedprogression.setphase <phase num>` - Set the current phase to `<phase num>`.

`timedprogression.setwipetime <date/time string>` - Set the time for the server's scheduled wipe time. `<date/time string>` is any valid date string format (3/17/2020 13:00, for example).


# Considerations

When a new map is discovered during server intitialization, the current time is saved as the wipe start time. Every minute it calculates the difference between the current time and the wipe start time to determine how many minutes have elapsed for the current wipe period. If, for any reason, you wish to change the wipe start time, you can use `timedprogression.setwipetime` command.

# Optional Plugins
[GUIAnnouncements](https://umod.org/plugins/gui-announcements) - Will be used to warn the player when they try to craft a locked item and to announce to players when the phase changes.

[Discord Core](https://umod.org/plugins/discord-core) - Can integrate with your Discord Bot to announce phase changes and support the `/listitems` command in your Discord server.

# Server Owners

Are you using, or planning to use, this plugin? Please let us know! We are interested in hearing about servers using our plugins and may even advertise them here if you are interested.

# Servers 

Below are a list of servers using our plugin.

**Working Man's Rust** - (retired)

**Niflheim - Road to Hel [PVE][Zombies][Raidable Bases][Events]** - PVE with PVP only at raidable bases - 45.35.205.133:28045 - discord[.]gg/vukMc72 
