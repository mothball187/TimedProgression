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
    [Info("Timed Progression", "mothball187", "0.0.1")]
    [Description("Restricts crafting and looting of items based on configurable tiers, unlocked over configurable time periods")]
    class TimedProgression : CovalencePlugin
    {
        [PluginReference]
        private Plugin GUIAnnouncements;

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
            player.Message($"Current phase: {(int)timeData["currentThreshold"]}");
        }

        [Command("timedprogression.setthreshold")]
        private void SetThreshold(IPlayer player, string command, string[] args)
        {
            if(player.IsAdmin)
            {
                try{
                    int idx = Int32.Parse(args[0]);
                    int seconds = Int32.Parse(args[1]);
                    config.thresholds[idx] = seconds;
                    SaveConfig();
                    player.Message($"Threshold {idx} set to {seconds}");
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
                    timeData["currentThreshold"] = phase;
                    RefreshLoot();
                    CheckAllLoot();
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
                    int day = Int32.Parse(args[0]);
                    config.wipeStartDay = day;
                    int hour = Int32.Parse(args[1]);
                    config.wipeStartHour = hour;
                    SaveConfig();
                    player.Message($"Wipe day set to day {day} and hour {hour}");
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

        [Command("timedprogression.newwipe")]
        private void NewWipe(IPlayer player, string command, string[] args)
        {
            if(player.IsAdmin)
            {
                try{
                    timeData["currentThreshold"] = 0;
                    SaveLoop();
                    ResetProgressionTimer();
                    RefreshLoot();
                    CheckAllLoot();
                    player.Message($"Reset phase and progression timer");
                    NotifyPhaseChange();
                }
                catch{
                    player.Message("Error handling newwipe command");
                }
            }
            else
            {
                player.Message("This command is restricted to admins only");
            }
        }

        [Command("timedprogression.addweeks")]
        private void AddWeeks(IPlayer player, string command, string[] args)
        {
            if(player.IsAdmin)
            {
                try{
                    int weeks = Int32.Parse(args[0]);
                    wipeStart.AddDays(-weeks * 7);
                    player.Message($"Added {weeks} week to the progression timer");
                }
                catch{
                    player.Message("Error handling addweeks command");
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
            public int wipeStartDay;
            public int wipeStartHour;
        }

        protected void LoadDefaultItemsConfig()
        {
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

            items.Save();
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
            config.thresholds = new List<long>();
            config.thresholds.Add(60 * 60 * 48); // 2 days
            config.thresholds.Add(60 * 60 * 96); // 4 days
            config.wipeStartDay = 4; // Thursday
            config.wipeStartHour = 13; // 1PM
            SaveConfig();
        }

        private void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Oxide Hooks

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

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            UpdateContainer(entity);
        }


        private object OnLootSpawn(LootContainer container)
        {
            return UpdateContainer(container);
        }

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

        #endregion Oxide Hooks

        private void ResetProgressionTimer()
        {
            wipeStart = DateTime.Today;
            while(wipeStart.DayOfWeek != (System.DayOfWeek)config.wipeStartDay) wipeStart = wipeStart.AddDays(-1);
            wipeStart = wipeStart.AddHours(config.wipeStartHour);
        }

        private bool CanHaveItem(ItemDefinition itemdef)
        {
            string itemCategory = itemdef.category.ToString("f");
            //Puts($"{itemdef.shortname} is of the {itemCategory} category");
            if(items[itemCategory, itemdef.shortname] != null)
            {
                if((int)timeData["currentThreshold"] < (int)items[itemCategory, itemdef.shortname])
                    return false;
            }
            return true;
        }

        private Item ReplaceItem(ItemDefinition itemdef)
        {
            List<string> itemPool = new List<string>();
            Dictionary<string, object> catItems = items[itemdef.category.ToString("f")] as Dictionary<string, object>;
            int currentThreshold = (int)timeData["currentThreshold"];
            while(currentThreshold >= 0 && itemPool.Count == 0)
            {
                foreach(string name in catItems.Keys)
                {
                    int thresholdIdx = (int)items[itemdef.category.ToString("f"), name];
                    
                    if(thresholdIdx == currentThreshold)
                        itemPool.Add(name);
                }

                currentThreshold--;
            }

            if(itemPool.Count == 0)
            {
                Puts($"Couldn't find replacement for {itemdef.shortname}");
                return null;
            }

            int r = rnd.Next(itemPool.Count);
            Item newItem = ItemManager.CreateByName(itemPool[r], 1);
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
                    Item itemToAdd = ReplaceItem(item.info);
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
            server.Command("spawn.fill_groups");
        }

        private void Init()
        {
            TimeZoneInfo.ClearCachedData();
            config = Config.ReadObject<PluginConfig>();
            timeData = Interface.Oxide.DataFileSystem.GetFile("TimedProgression/timeData");
            items = Interface.Oxide.DataFileSystem.GetFile("TimedProgression/items");
            //items.Clear();
            ResetProgressionTimer();
            if(items["Weapon"] == null)
                LoadDefaultItemsConfig();

            if(timeData["currentThreshold"] == null)
                timeData["currentThreshold"] = 0;

            RefreshLoot();
            CheckAllLoot();
            timer.Every(1f, UpdateLoop);
            timer.Every(10f, SaveLoop);
            return;
        }

        private void UpdateLoop()
        {
            if((int)timeData["currentThreshold"] == config.thresholds.Count)
                return;

            TimeSpan elapsed = DateTime.Now - wipeStart;
            if(elapsed.TotalSeconds >= config.thresholds[(int)timeData["currentThreshold"]])
            {
                timeData["currentThreshold"] = (int)timeData["currentThreshold"] + 1;
                RefreshLoot();
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
            foreach (var player in BasePlayer.activePlayerList)
            {
                string msg = $"ATTENTION: PHASE {(int)timeData["currentThreshold"]} HAS BEGUN";
                player.ChatMessage(msg);
                if(GUIAnnouncements != null)
                    GUIAnnouncements?.Call("CreateAnnouncement", msg, "Purple", "Yellow", player);
            }
        }

        private void RefreshVendingMachines()
        {
            foreach(var entity in BaseNetworkable.serverEntities.ToList())
            {
                if (entity is NPCVendingMachine)
                {
                    (entity as NPCVendingMachine).InstallFromVendingOrders();
                }
            }
        }

    }
}
