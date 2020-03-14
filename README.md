# TimedProgression

`TimedProgression` is a uMod plugin for Rust servers that restricts configured weapons, armor, and items and unlocks them over configured intervals of time called "phases". 

This effectively time gates progression on a Rust server, which is a desirable effect for players who only have a limited amount of time to play in a day but want fair competition. 

This plugin helps create a level playing field for players even if some players have more time to play than others, and extends the lifetime of each tier of combat. 

The [WorkingMan Plugin](https://github.com/pilate/WorkingMan), which restricts the amount of time players can play on your server, makes a great companion to this plugin.


# Example Setup

The default configuration for this plugin only allows tier 0 and tier 1 weapons and armor for the first two days of a server wipe, called "phase 0". 

This means a player will never find tier 2 or tier 3 weapons or armor in crates, even elite crates. The player will also not be able to craft a tier 2 workbench or any tier 2 items, nor will they be able to purchase end tier weapons from the bandit camp.

If a player opens loot that would have contained a tier 2 or tier 3 item, it is replaced with an item of the highest tier available for the current phase, of the same category. For example, if you were to loot a thompson in phase 0, you might instead loot a revolver or a double barrel shotgun.

After two days, phase 1 begins which unlocks tier 2 weapons and armor. All loot is refreshed and from now on, loot can contain tier 2 items such as road plate armor, custom smgs, etc. The player can also now craft the tier 2 workbench and begin researching and crafting tier 2 items.

After two more days, phase 2 begins which unlocks all restricted items. At this time, you can now craft the tier 3 workbench, research and craft tier 3 items, and purchase weapons from the bandit camp as well.

# Configuration

The configuration for this plugin is divided into the configuration file, which is fairly simple, and a data file named "items.json".

The configuration file contains the following settings:
`thresholds` - An array of integers representing the number of seconds from the start of wipe before the next phase begins. The default values are `172800` and `345600`, which are 2 days, and 4 days, respectively. This means phase 1 begins after two days from wipe, and phase 2 after 4 days from wipe. You can configure more than two thresholds to add more phases to your wipe period.

`wipeStartDay` - The 0 based index of the day of the week your wipe period begins. This value defaults to 4 (Thursday).

`wipeStartHour` - The hour of the day your wipe period begins on your configured wipe day of the week. This value defaults to 13 (1pm).

The `items.json` file contains the more complex details of which items are restricted and their unlock phase. Below is the default configuration.
```
items["Weapon", "pistol.revolver"] = 0;
items["Weapon", "shotgun.double"] = 0;
items["Weapon", "pistol.m92"] = 2;
items["Weapon", "pistol.python"] = 1;
items["Weapon", "pistol.semiauto"] = 1;
items["Weapon", "rifle.ak"] = 2;
items["Weapon", "rifle.bolt"] = 2;
items["Weapon", "rifle.l96"] = 2;
items["Weapon", "rifle.lr300"] = 2;
items["Weapon", "rifle.m39"] = 2;
items["Weapon", "rifle.semiauto"] = 1;
items["Weapon", "shotgun.pump"] = 1;
items["Weapon", "shotgun.spas12"] = 2;
items["Weapon", "smg.2"] = 1;
items["Weapon", "smg.mp5"] = 2;
items["Weapon", "smg.thompson"] = 1;
items["Weapon", "lmg.m249"] = 2;
items["Weapon", "rocket.launcher"] = 2;
items["Weapon", "multiplegrenadelauncher"] = 2;

items["Attire", "wood.armor.jacket"] = 0;
items["Attire", "wood.armor.pants"] = 0;
items["Attire", "wood.armor.helmet"] = 0;
items["Attire", "roadsign.gloves"] = 1;
items["Attire", "roadsign.jacket"] = 1;
items["Attire", "coffeecan.helmet"] = 1;
items["Attire", "roadsign.kilt"] = 1;
items["Attire", "metal.facemask"] = 2;
items["Attire", "metal.plate.torso"] = 2;

items["Items", "workbench2"] = 1;
items["Items", "workbench3"] = 2;

items["Tool", "explosive.satchel"] = 0;
items["Tool", "explosive.timed"] = 2;
```

For example, the thompson (`smg.thompson`) is unlocked in phase 1, which is reached after two days with the default configuration. Be sure to include phase 0 items for each category, so the plugin will know which items can be used to replace restricted items during phase 0. For a complete list of item names, see the [uMod Rust Definitions page](https://umod.org/documentation/games/rust/definitions).

# Commands

The only command players can run is the chat command `/checkphase`. This informs the player of the current phase. 

There are several admin commands:
`timedprogression.setthreshold <phase num> <threshold seconds>` - Set the threshold for phase `<phase num>` to `<seconds>`.

`timedprogression.setphase <phase num>` - Set the current phase to `<phase num>`.

`timedprogression.setwipetime <day of week> <hour of day>` - Set the day and hour for the server's scheduled wipe time. `<day of week>` is the number of the zero-indexed day of the week, Sunday being `0`.

`timedprogression.newwipe` - Run this command immediately after a new wipe. It resets the phase and timer for the new wipe period.

`timedprogression.addweeks <number of weeks>` - This command is for servers with wipe schedules that last more than a week. Run this command after you have restarted the server or reloaded the plugin and are further than a week into your wipe period.

# Considerations

`TimedProgression` uses your server's configured timezone for determining the daily and weekly cycle start times. Be sure your server's timezone is configured how you desire. If you update your server's timezone, you can reload the plugin to have it sync with your new timezone as well.

When the plugin initializes, it calculates the wipe start time by taking the time of the start of the current day and subtracting days from it until it reaches your configured wipe start day, adding your configured start hour to it. Every second it calculates the difference between the current time and the wipe start time to determine how many seconds have elapsed for the current wipe period.

**Important** - if your wipe period is longer than a week and you have to restart your server after your first week has passed, you will need to run the week `timedprogression.addweeks` command afterwards to catch the progression timer up.

**Important** - you must run the `timedprogression.newwipe` command shortly after starting your server for a fresh wipe in order to reset phase to `0`. Optionally, you could simply delete the `oxide/data/TimedProgression/timeData.json` file before starting your server. If you script your wipe, simply delete this file as part of your wipe routine to automate it.

