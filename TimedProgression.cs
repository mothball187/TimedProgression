using Rust;
using Facepunch.Extend;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.ComponentModel;
using ProtoBuf;
using Random = System.Random;
using Oxide.Core.Libraries.Covalence;


namespace Oxide.Plugins
{
    [Info("Timed Progression", "mothball187", "0.0.3")]
    [Description("Restricts crafting and looting of items based on configurable tiers, unlocked over configurable time periods")]
    class TimedProgression : CovalencePlugin
    {
        [PluginReference] private Plugin DiscordCore, GUIAnnouncements;

        #region Fields

        private DynamicConfigFile timeData;
        private DynamicConfigFile items;
        Random rnd = new Random();
        private PluginConfig config;
        private DateTime wipeStart;

        #endregion Fields

        #region Commands

        [Command("checkphase")]
        private void CheckPhase(IPlayer player, string command, string[] args)
        {
            int cp = (int)timeData["currentPhase"];
            player.Message($"Current phase: {cp}");
            if(cp - 1 < config.thresholds.Count)
            {
                DateTime wt = DateTime.Parse((string)timeData["wipeTime"]);
                TimeSpan elapsed = DateTime.Now - wt;
                string timeLeft = FormatTimeSpan(config.thresholds[cp - 1] - (long)elapsed.TotalSeconds);
                player.Message($"Time left in this phase: {timeLeft}");
            }
            
            
        }

        [Command("listitems")]
        private void ListItemsCmd(IPlayer player, string command, string[] args)
        {
            ListItems(player=player);
        }

        [Command("timedprogression.setthreshold")]
        private void SetThreshold(IPlayer player, string command, string[] args)
        {
            if(player.IsAdmin)
            {
                try{
                    int phase = Int32.Parse(args[0]);
                    int seconds = Int32.Parse(args[1]);
                    config.thresholds[phase - 1] = seconds;
                    SaveConfig();
                    player.Message($"Threshold {phase} set to {seconds}");
                }
                catch{
                    player.Message("Error handling setthreshold command");
                }
            }
            else
            {
                player.Message("This command is restricted to admins only");
            }
        }

        [Command("timedprogression.setphase")]
        private void SetPhase(IPlayer player, string command, string[] args)
        {
            if(player.IsAdmin)
            {
                try{
                    int phase = Int32.Parse(args[0]);
                    timeData["currentPhase"] = phase;
                    timeData.Save();
                    //RefreshLoot();
                    //CheckAllLoot();
                    RefreshVendingMachines();
                    player.Message($"Phase set to {phase}");
                    NotifyPhaseChange();
                }
                catch{
                    player.Message("Error handling setphase command");
                }
            }
            else
            {
                player.Message("This command is restricted to admins only");
            }
        }


        [Command("timedprogression.setwipetime")]
        private void SetWipeTime(IPlayer player, string command, string[] args)
        {
            if(player.IsAdmin)
            {
                try{
                    string dateString = args[0];
                    DateTime dt;
                    if(DateTime.TryParse(dateString, out dt))
                    {
                        player.Message($"Wipe time set to {dt}");
                        timeData["wipeTime"] = dateString;
                        timeData.Save();
                    }
                    else
                        player.Message($"Failed to parse supplied date string {dateString}");
                }
                catch{
                    player.Message("Error handling setwipetime command");
                }
            }
            else
            {
                player.Message("This command is restricted to admins only");
            }
        }



        #endregion Commands

        #region Configuration

        class PluginConfig
        {
            public List<long> thresholds;
        }

        protected void LoadDefaultItemsConfig()
        {
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

            items.Save();
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
            config.thresholds = new List<long>();
            config.thresholds.Add(60 * 60 * 48); // 2 days
            config.thresholds.Add(60 * 60 * 96); // 4 days
            SaveConfig();
        }

        private void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Oxide Hooks

        /*
        private void OnEntityKill(BaseNetworkable entity)
        {
            BaseEntity baseEnt = entity as BaseEntity;
            if (baseEnt == null) return;
            if (entity.GetComponent<LootContainer>())
            {
                UpdateContainer(entity.GetComponent<LootContainer>());
            }
            else if (entity.GetComponent<StorageContainer>())
            {
                UpdateContainer(entity.GetComponent<StorageContainer>());
            }
        }
        */

        private void OnNewSave(string filename)
        {
            timeData["wipeTime"] = DateTime.Now.ToString();
            timeData["currentPhase"] = 1;
            timeData.Save();
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            UpdateContainer(entity);
        }

        /*
        private object OnLootSpawn(LootContainer container)
        {
            return UpdateContainer(container);
        }
        */
        

        private void NotifyPlayer(ItemDefinition itemdef, BasePlayer player)
        {
            string msg = $"{itemdef.displayName.english} is currently locked!";
            player.ChatMessage(msg);
            if(GUIAnnouncements != null)
                GUIAnnouncements?.Call("CreateAnnouncement", msg, "Purple", "Yellow", player);
        }

        private bool CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            if(!CanHaveItem(bp.targetItem))
            {
                NotifyPlayer(bp.targetItem, itemCrafter.GetComponent<BasePlayer>());
                return false;
            }

            return true;
        }

        private object OnVendingTransaction(VendingMachine machine, BasePlayer buyer, int sellOrderId, int numberOfTransactions)
        {
            ProtoBuf.VendingMachine.SellOrder sellOrder = machine.sellOrders.sellOrders[sellOrderId];
            List<global::Item> list = machine.inventory.FindItemsByItemID(sellOrder.itemToSellID);
            if (list != null && list.Count > 0)
            {
                if(!CanHaveItem(list[0].info))
                {
                    if(buyer != null)
                        NotifyPlayer(list[0].info, buyer);

                    return false;
                }
            }

            return null;
        }

        private void Unload()
        {
            return;
        }

        private void OnServerInitialized()
        {
            if(DiscordCore != null)
                OnDiscordCoreReady();
        }

        #endregion Oxide Hooks

        private void ListItems(IPlayer player=null, string channelId=null)
        {
            DateTime wt = DateTime.Parse((string)timeData["wipeTime"]);
            int cp = (int)timeData["currentPhase"];
            Dictionary<int, string> phaseItems = new Dictionary<int, string>();
            foreach (ItemCategory category in (ItemCategory[]) Enum.GetValues(typeof(ItemCategory)))
            {
                if(items[category.ToString("f")] == null)
                    continue;

                Dictionary<string, object> catItems = items[category.ToString("f")] as Dictionary<string, object>;
                foreach(string name in catItems.Keys)
                {
                    int phase = (int)catItems[name];
                    if(phase < 2)
                        continue;

                    ItemDefinition itemdef = ItemManager.FindItemDefinition(name);
                    string fullname = itemdef.displayName.english;
                    string phaseString = "";
                    if(phaseItems.TryGetValue(phase, out phaseString))
                    {
                        phaseString += $", {fullname}";
                        phaseItems[phase] = phaseString;
                    }
                    else
                        phaseItems[phase] = $"{fullname}";
                }
            }

            if(items["HeavyAmmo"] != null)
            {
                 Dictionary<string, object> catItems = items["HeavyAmmo"] as Dictionary<string, object>;
                foreach(string name in catItems.Keys)
                {
                    int phase = (int)catItems[name];
                    if(phase < 2)
                        continue;

                    ItemDefinition itemdef = ItemManager.FindItemDefinition(name);
                    string fullname = itemdef.displayName.english;
                    string phaseString = "";
                    if(phaseItems.TryGetValue(phase, out phaseString))
                    {
                        phaseString += $", {fullname}";
                        phaseItems[phase] = phaseString;
                    }
                    else
                        phaseItems[phase] = $"{fullname}";
                }
            } 

            bool messageSent = false;
            foreach(int phase in phaseItems.Keys)
            {
                if(cp < phase)
                {
                    TimeSpan elapsed = DateTime.Now - wt;
                    string timeLeft = FormatTimeSpan(config.thresholds[phase - 2] - (long)elapsed.TotalSeconds);
                    if(player != null)
                        player.Message($"{phaseItems[phase]} unlocks in phase {phase}, in {timeLeft}");
                    else if(DiscordCore != null && channelId != null)
                        SendMessage(channelId, $"{phaseItems[phase]} unlocks in phase {phase}, in {timeLeft}");
                    messageSent = true;
                }
            }

            if(!messageSent)
            {
                if(player != null)
                    player.Message("All items unlocked!");
                else if(DiscordCore != null && channelId != null)
                    SendMessage(channelId, "All items unlocked!");
            }
        }

        private void SendMessage(string channelId, string message)
        {
            DiscordCore.Call("SendMessageToChannel", channelId, $"{message}");
        }

        private object HandleListItems(IPlayer player, string channelId, string cmd, string[] args)
        {
            ListItems(player=null, channelId=channelId);
            return null;
        }

        private void OnDiscordCoreReady()
        {
            if (!(DiscordCore?.Call<bool>("IsReady") ?? false))
            {
                return;
            }

            DiscordCore.Call("RegisterCommand", "listitems", this, new Func<IPlayer, string, string, string[], object>(HandleListItems), "Show next items to unlock", null, true);
        }

        private string FormatTimeSpan(long seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds( seconds );
            string answer = string.Format("{0:D2}d:{1:D2}h:{2:D2}m:{3:D2}s",
                t.Days, 
                t.Hours, 
                t.Minutes, 
                t.Seconds);
            return answer;
        }

        private bool CanHaveItem(ItemDefinition itemdef)
        {
            string itemCategory;
            if(itemdef.category.ToString("f").Equals("Ammunition") && (itemdef.shortname.Contains("rocket") || itemdef.shortname.Contains("grenade")))
                itemCategory = "HeavyAmmo";
            else
                itemCategory = itemdef.category.ToString("f");

            //Puts($"{itemdef.shortname} is of the {itemCategory} category");
            if(items[itemCategory, itemdef.shortname] != null)
            {
                if((int)timeData["currentPhase"] < (int)items[itemCategory, itemdef.shortname])
                    return false;
            }
            return true;
        }

        private Item ReplaceItem(ItemDefinition itemdef, int amount)
        {
            List<string> itemPool = new List<string>();
            string cat;
            if(itemdef.category.ToString("f").Equals("Ammunition") && (itemdef.shortname.Contains("rocket") || itemdef.shortname.Contains("grenade")))
                cat = "HeavyAmmo";
            else
                cat = itemdef.category.ToString("f");

            Dictionary<string, object> catItems = items[cat] as Dictionary<string, object>;
            int currentPhase = (int)timeData["currentPhase"];
            while(currentPhase >= 1 && itemPool.Count == 0)
            {
                foreach(string name in catItems.Keys)
                {
                    int curItemPhase = (int)items[cat, name];
                    
                    if(curItemPhase == currentPhase)
                        itemPool.Add(name);
                }

                currentPhase--;
            }

            if(itemPool.Count == 0)
            {
                Puts($"Couldn't find replacement for {itemdef.shortname}");
                return null;
            }

            int r = rnd.Next(itemPool.Count);
            Item newItem = ItemManager.CreateByName(itemPool[r], amount);
            Puts($"Replacing {itemdef.shortname} with {newItem.info.shortname}");
            return newItem;
        }

        private object UpdateContainer(BaseEntity container)
        {
            bool updated = false;
            List<Item> itemsToRemove = new List<Item>();
            List<Item> itemsToAdd = new List<Item>();
            ItemContainer inventory = null;

            if (container is LootContainer)
            {
                //Puts($"Checking a LootContainer");
                inventory = (container as LootContainer).inventory;
                (container as LootContainer).minSecondsBetweenRefresh = -1;
                (container as LootContainer).maxSecondsBetweenRefresh = 0;
                (container as LootContainer).CancelInvoke("SpawnLoot");

            }
            else if(container is StorageContainer)
            {
                //Puts($"Checking a StorageContainer");
                inventory = (container as StorageContainer).inventory;
            }
            else if(container is DroppedItemContainer)
            {
                //Puts($"Checking a DroppedItemContainer");
                inventory = (container as DroppedItemContainer).inventory;
            }
            else if(container is NPCPlayerCorpse)
            {
                //Puts($"Checking a NPCPlayerCorpse");
                inventory = (container as NPCPlayerCorpse).containers[0];
            }
            else
            {
                Puts($"Unhandled type: {container.GetType()}");
                return null;
            }
            
            foreach(Item item in inventory.itemList)
            {
                if(!CanHaveItem(item.info))
                {
                    Item itemToAdd = ReplaceItem(item.info, item.amount);
                    if(itemToAdd != null)
                        itemsToAdd.Add(itemToAdd);

                    itemsToRemove.Add(item);
                    updated = true;
                }
            }

            if(updated)
            {
                foreach(Item item in itemsToRemove)
                {
                    item.RemoveFromContainer();
                    item.Remove(0f);
                }

                foreach(Item item in itemsToAdd)
                {
                    item.MoveToContainer(inventory, -1, false);
                }

                inventory.MarkDirty();
                container.SendNetworkUpdate();
                ItemManager.DoRemoves();
                return true;
            }
            return null;
        }

        private void CheckAllLoot()
        {
            var spawns = Resources.FindObjectsOfTypeAll<LootContainer>().Where(c => c.isActiveAndEnabled).ToList();

            var count = spawns.Count();
            var check = 0;
            Puts($"Checking {count} LootContainers");
            for (var i = 0; i < count; i++)
            {
                var box = spawns[i];
                check++;
                UpdateContainer(box);
            }

            Puts($"Checked {check} LootContainers");
        }

        private void RefreshLoot()
        {
            server.Command("del assets/bundled/prefabs/radtown/");
            timer.Once(5f, () =>
            {
                server.Command("spawn.fill_groups"); // this also respawns bad guys which is bad
            });
            
        }

        // in case the user puts rockets in the Ammunition category
        private void FixHeavyAmmoCategory()
        {
            if(items["Ammunition"] == null)
                return;

            Dictionary<string, object> ammoItems = items["Ammunition"] as Dictionary<string, object>;
            foreach(string shortname in ammoItems.Keys)
            {
                if(shortname.Contains("rocket"))
                {
                    items["HeavyAmmo", shortname] = items["Ammunition", shortname];
                }
            }
            items.Save();
        }

        private void Init()
        {
            TimeZoneInfo.ClearCachedData();
            config = Config.ReadObject<PluginConfig>();
            timeData = Interface.Oxide.DataFileSystem.GetFile("TimedProgression/timeData");
            items = Interface.Oxide.DataFileSystem.GetFile("TimedProgression/items");
            //items.Clear();
            if(items["Weapon"] == null)
                LoadDefaultItemsConfig();

            FixHeavyAmmoCategory();

            if(timeData["currentPhase"] == null)
                timeData["currentPhase"] = 1;

            if(timeData["wipeTime"] == null)
                timeData["wipeTime"] = DateTime.Now.ToString();

            //RefreshLoot();
            //CheckAllLoot();
            timer.Every(1f, UpdateLoop);
            timer.Every(10f, SaveLoop);

            RefreshVendingMachines();
            return;
        }

        private void UpdateLoop()
        {
            if((int)timeData["currentPhase"] - 1 == config.thresholds.Count)
                return;

            TimeSpan elapsed = DateTime.Now - DateTime.Parse((string)timeData["wipeTime"]);
            //Puts($"{elapsed} seconds since wipe");
            if(elapsed.TotalSeconds >= config.thresholds[(int)timeData["currentPhase"] - 1])
            {
                timeData["currentPhase"] = (int)timeData["currentPhase"] + 1;
                //RefreshLoot();
                NotifyPhaseChange();
                RefreshVendingMachines();
            }
        }

        private void SaveLoop()
        {
            timeData.Save();
        }

        private void NotifyPhaseChange()
        {
            string msg = $"ATTENTION: PHASE {(int)timeData["currentPhase"]} HAS BEGUN";
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage(msg);
                if(GUIAnnouncements != null)
                    GUIAnnouncements?.Call("CreateAnnouncement", msg, "Purple", "Yellow", player);
            }

            if(DiscordCore != null)
                SendMessage("bots", msg);
        }

        private void RefreshVendingMachines()
        {
            foreach(var entity in BaseNetworkable.serverEntities.ToList())
            {
                if (entity is NPCVendingMachine)
                {
                    MyInstallFromVendingOrders((entity as NPCVendingMachine));
                }
            }
        }

        private void MyInstallFromVendingOrders(NPCVendingMachine machine)
        {
            if(machine.vendingOrders == null)
                return;

            machine.ClearSellOrders();
            machine.inventory.Clear();
            ItemManager.DoRemoves();
            foreach(NPCVendingOrder.Entry entry in machine.vendingOrders.orders)
            {
                ItemDefinition itemDef = ItemManager.FindItemDefinition(entry.sellItem.itemid);
                if(CanHaveItem(itemDef))
                    machine.AddItemForSale(entry.sellItem.itemid, entry.sellItemAmount, entry.currencyItem.itemid, 
                                           entry.currencyAmount, machine.GetBPState(entry.sellItemAsBP, entry.currencyAsBP));
            }
        }

    }
}
