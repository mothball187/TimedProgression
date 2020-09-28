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
using System.Text;


namespace Oxide.Plugins
{
    [Info("Timed Progression", "mothball187", "0.1.2")]
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
            NotifyPhaseInfo(player);
        }

        [Command("listitems")]
        private void ListItemsCmd(IPlayer player, string command, string[] args)
        {
            ListItems(player: player);
        }

        [Command("timedprogression.setthreshold")]
        private void SetThreshold(IPlayer player, string command, string[] args)
        {
            if(!player.HasPermission("timedprogression.configure"))
            {
                player.Reply(lang.GetMessage("NeedPermission", this, player.Id));
                return;
            }

            if(args.Length < 2)
            {
                player.Reply(lang.GetMessage("SetThresholdError", this, player.Id));
                return;
            }

            Int32 phase;
            if(!Int32.TryParse(args[0], out phase))
            {
                player.Reply(lang.GetMessage("SetThresholdError", this, player.Id));
                return;
            }

            Int32 minutes;
            if(!Int32.TryParse(args[1], out minutes))
            {
                player.Reply(lang.GetMessage("SetThresholdError", this, player.Id));
                return;
            }

            config.thresholds[phase - 1] = minutes;
            SaveConfig();
            player.Reply(string.Format(lang.GetMessage("SetThreshold", this, player.Id), phase, minutes));
        }

        [Command("timedprogression.setphase")]
        private void SetPhase(IPlayer player, string command, string[] args)
        {
            if(!player.HasPermission("timedprogression.configure"))
            {
                player.Reply(lang.GetMessage("NeedPermission", this, player.Id));
                return;
            }

            if(args.Length < 1)
            {
                player.Reply(lang.GetMessage("SetPhaseError", this, player.Id));
                return;
            }

            Int32 phase;
            if(!Int32.TryParse(args[0], out phase))
            {
                player.Reply(lang.GetMessage("SetPhaseError", this, player.Id));
                return;
            }

            timeData["currentPhase"] = phase;
            timeData.Save();

            RefreshVendingMachines();
            player.Reply(string.Format(lang.GetMessage("SetPhase", this, player.Id), phase));
            NotifyPhaseChange();
        }


        [Command("timedprogression.setwipetime")]
        private void SetWipeTime(IPlayer player, string command, string[] args)
        {
            if(!player.HasPermission("timedprogression.configure"))
            {
                player.Reply(lang.GetMessage("NeedPermission", this, player.Id));
                return;
            }

            if(args.Length < 1)
            {
                player.Reply(lang.GetMessage("SetWipeTimeError", this, player.Id));
                return;
            }

            DateTime dt;
            if(!DateTime.TryParse(args[0], out dt))
            {
                player.Reply(string.Format(lang.GetMessage("SetWipeTimeError", this, player.Id), args[0]));
                return;
            }

            player.Reply(string.Format(lang.GetMessage("SetWipeTime", this, player.Id), dt));
            timeData["wipeTime"] = args[0];
            timeData.Save();
        }



        #endregion Commands

        #region Configuration

        class PluginConfig
        {
            public List<long> thresholds;
            public string botChannel;
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
            items["Weapon", "grenade.beancan"] = 1;

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
            config.thresholds.Add(60 * 24 * 2); // 2 days
            config.thresholds.Add(60 * 24 * 4); // 4 days
            config.botChannel = "bots";
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Oxide Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SetThresholdError"] = "Error handling setthreshold command",
                ["SetThreshold"] = "Threshold {0} set to {1}",
                ["SetPhaseError"] = "Error handling setphase command",
                ["SetPhase"] = "Phase set to {0}",
                ["SetWipeTimeError"] = "Failed to parse supplied date string {0}",
                ["SetWipeTime"] = "Wipe time set to {0}",
                ["NotifyPlayer"] = "{0} is currently locked!",
                ["PhaseInfo1"] = "Current phase: {0}",
                ["PhaseInfo2"] = "Time left in this phase: {0}",
                ["ListItems"] = "{0} unlocks in phase {1}, in {2}",
                ["AllUnlocked"] = "All items unlocked!",
                ["NotifyPhaseChange"] = "ATTENTION: PHASE {0} HAS BEGUN",
                ["NeedPermission"] = "You must have the 'timedprogression.configure' permission to use this command.",
                ["ConfigError"] = "Failed to load config file (is the config file corrupt?) ({0})"
            }, this);
        }

        private void OnNewSave(string filename)
        {
            timeData["wipeTime"] = DateTime.Now.ToString();
            timeData["currentPhase"] = 1;
            timeData.Save();
            timer.Once(150f, RefreshVendingMachines);  
        }
		
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            UpdateContainer(entity);
        }

        private void NotifyPlayer(ItemDefinition itemdef, BasePlayer player)
        {
            string msg = string.Format(lang.GetMessage("NotifyPlayer", this, player.UserIDString), itemdef.displayName.english);
            player.ChatMessage(msg);
            GUIAnnouncements?.Call("CreateAnnouncement", msg, "Purple", "Yellow", player);
        }

        /*
        private object CanWearItem(PlayerInventory inventory, Item item, int targetSlot)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if(player.IsNpc)
                return null;

            if(!CanHaveItem(item.info))
            {
                DestroyItem(item);
                NotifyPlayer(item.info, player);
                return false;
            }
            return null;
        }

        private object CanEquipItem(PlayerInventory inventory, Item item, int targetPos)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if(player.IsNpc)
                return null;

            if(!CanHaveItem(item.info))
            {
                DestroyItem(item);
                NotifyPlayer(item.info, player);
                return false;
            }
            return null;
        }
        */

        private ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            BasePlayer player = container.GetOwnerPlayer();
            if(player == null || player.IsNpc || player.IsAdmin)
                return null;

            if(CanHaveItem(item.info))
                return null;

            ReplaceItem(item.info, item.amount).MoveToContainer(container, -1, false);
            item.Remove(0f);
            return ItemContainer.CanAcceptResult.CannotAccept;
        }

        private object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            if(CanHaveItem(bp.targetItem))
                return  null;

            NotifyPlayer(bp.targetItem, itemCrafter.GetComponent<BasePlayer>());
            return false;
        }

        private void NotifyPhaseInfo(IPlayer player)
        {
            int cp = (int)timeData["currentPhase"];
            player.Message(string.Format(lang.GetMessage("PhaseInfo1", this, player.Id), cp));
            if(cp - 1 >= config.thresholds.Count)
                return;

            DateTime wt = DateTime.Parse((string)timeData["wipeTime"]);
            TimeSpan elapsed = DateTime.Now - wt;
            string timeLeft = FormatTimeSpan(config.thresholds[cp - 1] - (long)elapsed.TotalMinutes);
            player.Message(string.Format(lang.GetMessage("PhaseInfo2", this, player.Id), timeLeft));
        }

        private object OnVendingTransaction(VendingMachine machine, BasePlayer buyer, int sellOrderId, int numberOfTransactions)
        {
            ProtoBuf.VendingMachine.SellOrder sellOrder = machine.sellOrders.sellOrders[sellOrderId];
            /*
            List<global::Item> list = machine.inventory.FindItemsByItemID(sellOrder.itemToSellID);
            if (list == null && list.Count == 0)
                return null;

            if(CanHaveItem(list[0].info))
                return null;

            if(buyer != null)
                NotifyPlayer(list[0].info, buyer);

            */

            ItemDefinition itemDef = ItemManager.FindItemDefinition(sellOrder.itemToSellID);
            if(itemDef == null)
                return null;

            if(CanHaveItem(itemDef))
                return null;

            if(buyer != null)
                NotifyPlayer(itemDef, buyer);

            return false;

        }

        private void OnPlayerConnected(BasePlayer player)
        {
            NotifyPhaseInfo(player.IPlayer);
        }

        private void OnServerInitialized()
        {
            if(DiscordCore != null)
                OnDiscordCoreReady();
        }

        #endregion Oxide Hooks

        private void BuildPhaseItemsStrings(Dictionary<string, object> catItems, ref Dictionary<int, StringBuilder> phaseItems)
        {
            foreach(string name in catItems.Keys)
            {
                int phase = (int)catItems[name];
                if(phase < 2)
                    continue;

                ItemDefinition itemdef = ItemManager.FindItemDefinition(name);
                if(itemdef == null)
                    continue;

                StringBuilder sb;
                if(phaseItems.TryGetValue(phase, out sb))
                {
                    sb.Append(", ");
                    sb.Append(itemdef.displayName.english);
                }
                else
                    phaseItems[phase] = new StringBuilder(itemdef.displayName.english);
            }

        }

        private void ListItems(IPlayer player=null, string channelId=null)
        {
            DateTime wt = DateTime.Parse((string)timeData["wipeTime"]);
            int cp = (int)timeData["currentPhase"];
            Dictionary<int, StringBuilder> phaseItems = new Dictionary<int, StringBuilder>();
            foreach (ItemCategory category in (ItemCategory[]) Enum.GetValues(typeof(ItemCategory)))
            {
                if(items[category.ToString("f")] == null)
                    continue;

                BuildPhaseItemsStrings(items[category.ToString("f")] as Dictionary<string, object>, ref phaseItems);
            }

            if(items["HeavyAmmo"] != null)
                BuildPhaseItemsStrings(items["HeavyAmmo"] as Dictionary<string, object>, ref phaseItems);

            bool messageSent = false;
            foreach(int phase in phaseItems.Keys)
            {
                if(cp < phase)
                {
                    TimeSpan elapsed = DateTime.Now - wt;
                    string timeLeft = FormatTimeSpan(config.thresholds[phase - 2] - (long)elapsed.TotalMinutes);
                    if(player != null)
                        player.Message(string.Format(lang.GetMessage("ListItems", this, player.Id), phaseItems[phase].ToString(), phase, timeLeft));
                    else if(DiscordCore != null && channelId != null)
                        SendMessage(channelId, string.Format(lang.GetMessage("ListItems", this), phaseItems[phase].ToString(), phase, timeLeft));
                    messageSent = true;
                }
            }

            if(!messageSent)
            {
                if(player != null)
                    player.Message(lang.GetMessage("AllUnlocked", this, player.Id));
                else if(DiscordCore != null && channelId != null)
                    SendMessage(channelId, lang.GetMessage("AllUnlocked", this));
            }
        }

        private void SendMessage(string channelId, string message)
        {
            DiscordCore.Call("SendMessageToChannel", channelId, $"{message}");
        }

        private object HandleListItems(IPlayer player, string channelId, string cmd, string[] args)
        {
            ListItems(channelId: channelId);
            return null;
        }

        private void OnDiscordCoreReady()
        {
            if (!(DiscordCore?.Call<bool>("IsReady") ?? false))
                return;

            DiscordCore.Call("RegisterCommand", "listitems", this, new Func<IPlayer, string, string, string[], object>(HandleListItems), "Show next items to unlock", null, true);
        }

        private string FormatTimeSpan(long minutes)
        {
            TimeSpan t = TimeSpan.FromMinutes( minutes );
            string answer = string.Format("{0:D2}d:{1:D2}h:{2:D2}m",
                t.Days, 
                t.Hours, 
                t.Minutes);
            return answer;
        }

        private bool ItemIsHeavyAmmo(ItemDefinition itemdef)
        {
            return (itemdef.category == ItemCategory.Ammunition && (itemdef.shortname.Contains("rocket") || itemdef.shortname.Contains("grenade")));
        }

        private string GetCategoryString(ItemDefinition itemdef)
        {
            string itemCategory;
            if(ItemIsHeavyAmmo(itemdef))
                itemCategory = "HeavyAmmo";
            else
                itemCategory = itemdef.category.ToString("f");

            return itemCategory;
        }

        private bool CanHaveItem(ItemDefinition itemdef)
        {
            string itemCategory = GetCategoryString(itemdef);

            //Puts($"{itemdef.shortname} is of the {itemCategory} category");
            if(items[itemCategory, itemdef.shortname] != null)
            {
                if((int)timeData["currentPhase"] < (int)items[itemCategory, itemdef.shortname])
                    return false;
            }
            return true;
        }

        private void DestroyItem(Item item)
        {
            //item.RemoveFromContainer();
            item.Remove(0f);
            //inventory.GetContainer(PlayerInventory.Type.Main).MarkDirty();
            //(inventory as BaseEntity).SendNetworkUpdate();
            ItemManager.DoRemoves();
        }

        private Item ReplaceItem(ItemDefinition itemdef, int amount)
        {
            List<string> itemPool = new List<string>();
            string cat = GetCategoryString(itemdef);
            
            Dictionary<string, object> catItems = items[cat] as Dictionary<string, object>;
            int currentPhase = (int)timeData["currentPhase"];
            while(currentPhase >= 1 && itemPool.Count == 0)
            {
                foreach(string name in catItems.Keys)
                {                    
                    if((int)items[cat, name] == currentPhase)
                        itemPool.Add(name);
                }

                currentPhase--;
            }

            if(itemPool.Count == 0)
            {
                //Puts($"Couldn't find replacement for {itemdef.shortname}");
                return null;
            }

            //Puts($"Replacing {itemdef.shortname}");
            return ItemManager.CreateByName(itemPool[rnd.Next(itemPool.Count)], amount);
        }
		
		void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if(entity is LootContainer)
				UpdateContainer(entity as BaseEntity);
		}

        private void UpdateContainer(BaseEntity container)
        {
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
                return;
            }
     
            bool updated = false;
            List<Item> itemsToRemove = new List<Item>();
            List<Item> itemsToAdd = new List<Item>();

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
                    //item.RemoveFromContainer();
                    //DestroyItem(item);
                    item.Remove(0f);
                }

                ItemManager.DoRemoves();

                foreach(Item item in itemsToAdd)
                {
                    item.MoveToContainer(inventory, -1, false);
                }

                //inventory.MarkDirty();
                container.SendNetworkUpdate();
                return;
            }
            return;
        }

        // in case the user puts rockets/grenades in the Ammunition category
        private void FixHeavyAmmoCategory()
        {
            if(items["Ammunition"] == null)
                return;
			
            Dictionary<string, object> ammoItems = new Dictionary<string, object>(items["Ammunition"] as Dictionary<string, object>);
            foreach(string shortname in ammoItems.Keys)
                items[GetCategoryString(ItemManager.FindItemDefinition(shortname)), shortname] = items["Ammunition", shortname];

            items.Save();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            if (!Config.Exists())
                LoadDefaultConfig();
            else
            {
                try
                {
                    config = Config.ReadObject<PluginConfig>();
                }
                catch (Exception ex)
                {
                    RaiseError(string.Format(lang.GetMessage("ConfigError", this), ex.Message));
                }
            }

            SaveConfig();
        }

        private void Init()
        {
            TimeZoneInfo.ClearCachedData();
            permission.RegisterPermission("timedprogression.configure", this);
            timeData = Interface.Oxide.DataFileSystem.GetDatafile("TimedProgression/timeData");
            items = Interface.Oxide.DataFileSystem.GetDatafile("TimedProgression/items");
            if(items["Weapon"] == null)
            {
                Puts("Loading default config");
                LoadDefaultItemsConfig();
            }

            FixHeavyAmmoCategory();

            if(timeData["currentPhase"] == null)
                timeData["currentPhase"] = 1;

            if(timeData["wipeTime"] == null)
                timeData["wipeTime"] = DateTime.Now.ToString();

            timer.Every(60f, UpdateLoop);
            timer.Once(150f, RefreshVendingMachines);           
        }

        private void UpdateLoop()
        {
            if((int)timeData["currentPhase"] - 1 == config.thresholds.Count)
                return;

            if((DateTime.Now - DateTime.Parse((string)timeData["wipeTime"])).TotalMinutes < config.thresholds[(int)timeData["currentPhase"] - 1])
                return;

            timeData["currentPhase"] = (int)timeData["currentPhase"] + 1;
            NotifyPhaseChange();
            RefreshVendingMachines();

        }

        private void Unload()
        {
            timeData.Save();
        }

        private void OnServerSave()
        {
            timeData.Save();
        }

        private void NotifyPhaseChange()
        {
            string msg;
            foreach (var player in BasePlayer.activePlayerList)
            {
                msg = string.Format(lang.GetMessage("NotifyPhaseChange", this, player.UserIDString), (int)timeData["currentPhase"]);
                player.ChatMessage(msg);
                GUIAnnouncements?.Call("CreateAnnouncement", msg, "Purple", "Yellow", player);
            }

            if(DiscordCore != null)
                SendMessage(config.botChannel, string.Format(lang.GetMessage("NotifyPhaseChange", this), (int)timeData["currentPhase"]));
        }

        private void RefreshVendingMachines()
        {
            foreach(var entity in BaseNetworkable.serverEntities)
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
                if(CanHaveItem(ItemManager.FindItemDefinition(entry.sellItem.itemid)))
                    machine.AddItemForSale(entry.sellItem.itemid, entry.sellItemAmount, entry.currencyItem.itemid, 
                                           entry.currencyAmount, machine.GetBPState(entry.sellItemAsBP, entry.currencyAsBP));
            }
        }

    }
}
