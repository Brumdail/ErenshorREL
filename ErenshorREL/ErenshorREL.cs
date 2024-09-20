using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using BepInEx.Logging;
using BepInEx.Configuration;
using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

/* Welcome to the Erenshor Random Epic Loot Mod! This mod will edit each NPC's loot table when they spawn based on configurable parameters.
 * The intent is to be similar to random loot servers like Everquest's Mischief or Teek rulesets. Rare NPCs should be able to drop similar power level loot from within their level range even if they wouldn't normally drop it.
 * I also will allow configurable parameters to adjust the chance of getting various rarity of loot, as well as outputting the mob's drop table in the game chat.
 * We will generate an array of lists with the index being the level
*/
namespace ErenshorREL
{
    [BepInPlugin(ModGUID, ModDescription, ModVersion)]
    [BepInProcess("Erenshor.exe")]
    public class ErenshorREL : BaseUnityPlugin
    {

        internal const string ModName = "ErenshorREL";
        internal const string ModVersion = "0.0.2";
        internal const string ModDescription = "Erenshor Random Epic Loot";
        internal const string Author = "Brumdail";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        //internal static readonly int windowId = 777002; //will be used later for identifying the mod's window
        internal static ErenshorREL context = null!;

        private readonly Harmony harmony = new Harmony(ModGUID);

        public static readonly ManualLogSource ErenshorRELLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        public enum Toggle
        {
            Off,
            On
        }

        void Awake()
        {
            context = this; // Set the context to the current instance
            Utilities.GenerateConfigs(); // Generate initial configuration if necessary
            harmony.PatchAll(); // Apply all Harmony patches
            SetupWatcher(); // Start watching for configuration file changes
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void Update()
        {
            int levelsAboveHardcap = 4;
            //check config values and limit the range
            if (ErenshorREL.RandomLootLevelsBelow.Value < 0) { ErenshorREL.RandomLootLevelsBelow.Value = 0; }
            if (ErenshorREL.RandomLootLevelsBelow.Value > 100) { ErenshorREL.RandomLootLevelsBelow.Value = 100; }
            if (ErenshorREL.RandomLootLevelsAbove.Value < 0) { ErenshorREL.RandomLootLevelsAbove.Value = 0; }
            //don't allow users to configure item levels above this additional level since they could get godly loot at level 1 otherwise.
            if (ErenshorREL.RandomLootLevelsAbove.Value > levelsAboveHardcap) { ErenshorREL.RandomLootLevelsAbove.Value = levelsAboveHardcap; }
            if (ErenshorREL.RandomLootCommonNPCChance.Value < 0.0f) { ErenshorREL.RandomLootCommonNPCChance.Value = 0.0f; }
            if (ErenshorREL.RandomLootCommonNPCChance.Value > 100.0f) { ErenshorREL.RandomLootCommonNPCChance.Value = 100.0f; }
            if (ErenshorREL.RandomLootRareNPCChance.Value < 0.0f) { ErenshorREL.RandomLootRareNPCChance.Value = 0.0f; }
            if (ErenshorREL.RandomLootRareNPCChance.Value > 100.0f) { ErenshorREL.RandomLootRareNPCChance.Value = 100.0f; }
            if ((ErenshorREL.RandomLootText.Value == "") || (ErenshorREL.RandomLootText.Value is null)) { ErenshorREL.RandomLootText.Value = "Mysterious forces have added a new treasure: "; }
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                ErenshorRELLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                ErenshorRELLogger.LogError($"There was an issue loading your {ConfigFileName}");
                ErenshorRELLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        internal static ConfigEntry<Toggle> RandomLootToggle = null!;
        internal static ConfigEntry<int> RandomLootLevelsBelow = null!;
        internal static ConfigEntry<int> RandomLootLevelsAbove = null!;
        internal static ConfigEntry<float> RandomLootCommonNPCChance = null!;
        internal static ConfigEntry<float> RandomLootRareNPCChance = null!;
        internal static ConfigEntry<string> RandomLootText = null!;
        //internal static ConfigEntry<Toggle> RandomLootRestrict = null!;
        internal static ConfigEntry<Toggle> RandomLootDebug = null!;
        //internal static ConfigEntry<KeyboardShortcut> RandomLootKey = null!;
        internal static bool _configApplied;

        internal ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            return configEntry;
        }

        internal ConfigEntry<T> config<T>(string group, string name, T value, string description)
        {
            return Config.Bind(group, name, value, new ConfigDescription(description));
        }

        internal class AcceptableShortcuts : AcceptableValueBase // Used for KeyboardShortcut Configs 
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
        }

        #endregion



        [HarmonyPatch(typeof(ItemDatabase))]
        [HarmonyPatch("Start")]
        class GenerateItemLists
        {
            /// <summary>
            /// Create an array of lists of the items at each item level
            /// </summary>
            public static List<Item>[]? ItemsByLevel { get; private set; }

            static void Postfix()
            {
                // Ensure the feature is enabled and it's not the Demo -- Brian @ Burgee does not want non-demo items to be available in the demo or he'll have to pull them from the database.
                if ((ErenshorREL.RandomLootToggle.Value == Toggle.On) && !GameData.GM.DemoBuild)
                {
                    int maxItemLevel = 100;
                    // Initialize the array of lists
                    ItemsByLevel = new List<Item>[maxItemLevel + 1];
                    for (int i = 0; i <= maxItemLevel; i++)
                    {
                        ItemsByLevel[i] = new List<Item>();
                    }
                    foreach (Item item in GameData.ItemDB.ItemDB)
                    {
                        // Add item to the appropriate list based on its level
                        if (item.ItemLevel >= 1 && item.ItemLevel <= maxItemLevel && item.ItemValue > 0)
                        {
                            ItemsByLevel[item.ItemLevel].Add(item);
                        }
                    }

                    if (ErenshorREL.RandomLootDebug.Value == Toggle.On)
                    {
                        //Output the results:
                        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "ItemsByLevel.txt");
                        using (StreamWriter writer = new StreamWriter(filePath))
                        {
                            for (int level = 1; level < ItemsByLevel.Length; level++)
                            {
                                List<Item> itemsAtLevel = ItemsByLevel[level];
                                if (itemsAtLevel != null && itemsAtLevel.Count > 0)
                                {
                                    writer.WriteLine($"{itemsAtLevel.Count} Items at level {level}:");
                                    foreach (Item item in itemsAtLevel)
                                    {
                                        string strClasses = "";
                                        foreach (Class itemClass in item.Classes)
                                        {
                                            strClasses = strClasses + " " + itemClass.name;
                                        }
                                        writer.WriteLine($"- ID:{item.Id} {item.ItemName} Relic:{item.Relic} Unique:{item.Unique} Classes:{strClasses} Slot:{item.RequiredSlot.ToString()} Value:{item.ItemValue}");
                                    }
                                }
                            }
                        }
                        Debug.Log($"ItemsByLevel written to: {filePath}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Character))]
        [HarmonyPatch("DoDeath")]
        [HarmonyPriority(2)]
        [HarmonyBefore("Brumdail.ErenshorQoL")]
        class AddRandomLoot
        {
            /// <summary>
            /// Attempts to add random loot to the latest nearby corpse after each Character.DoDeath call
            /// </summary>
            static void Postfix(Character __instance)
            {
                // Ensure the feature is enabled and it's not the Demo -- Brian @ Burgee does not want non-demo items to be available in the demo or he'll have to pull them from the database.
                if ((ErenshorREL.RandomLootToggle.Value == Toggle.On) && !GameData.GM.DemoBuild)
                {
                    //Ensure the lists have been populated
                    if (GenerateItemLists.ItemsByLevel is not null)
                    {
                        // Ensure the character is an NPC and has a valid level
                        if (__instance.isNPC && __instance.MyStats.Level >= 1)
                        {
                            bool randomLootSuccess = false; //check for successful roll
                            bool isNPCWithinDistance = false; //check for an NPC that died nearby the player
                            bool isRareNPC = false; //needed to apply higher chance for a rare NPC
                            float requiredChance = 0f;
                            LootTable lootTable = __instance.GetComponent<LootTable>();
                            float randomLootDistance = 60f;
                            //int NPCGuaranteedDrops = 0;
                            //int NPCCommonDrops = 0;
                            //int NPCUncommonDrops = 0;
                            //int NPCRareDrops = 0;
                            //int NPCLegendaryDrops = 0;
                            int NPCMaxItemDrops = 0;
                            int NPCMaxNonCommonDrops = 0;
                            int NPCActualDrops = 0;

                            /*
                            //moved this block to the update section:
                            int levelsAboveHardcap = 4;
                            //check config values and limit the range
                            if (ErenshorREL.RandomLootLevelsBelow.Value < 0) { ErenshorREL.RandomLootLevelsBelow.Value = 0; }
                            if (ErenshorREL.RandomLootLevelsBelow.Value > 100) { ErenshorREL.RandomLootLevelsBelow.Value = 100; }
                            if (ErenshorREL.RandomLootLevelsAbove.Value < 0) { ErenshorREL.RandomLootLevelsAbove.Value = 0; }
                            //don't allow users to configure item levels above this additional level since they could get godly loot at level 1 otherwise.
                            if (ErenshorREL.RandomLootLevelsAbove.Value > levelsAboveHardcap) { ErenshorREL.RandomLootLevelsAbove.Value = levelsAboveHardcap; }
                            if (ErenshorREL.RandomLootCommonNPCChance.Value < 0.0f) { ErenshorREL.RandomLootCommonNPCChance.Value = 0.0f; }
                            if (ErenshorREL.RandomLootCommonNPCChance.Value > 100.0f) { ErenshorREL.RandomLootCommonNPCChance.Value = 100.0f; }
                            if (ErenshorREL.RandomLootRareNPCChance.Value < 0.0f) { ErenshorREL.RandomLootRareNPCChance.Value = 0.0f; }
                            if (ErenshorREL.RandomLootRareNPCChance.Value > 100.0f) { ErenshorREL.RandomLootRareNPCChance.Value = 100.0f; }
                            if ((ErenshorREL.RandomLootText.Value == "") || (ErenshorREL.RandomLootText.Value is null)) { ErenshorREL.RandomLootText.Value = "Mysterious forces have added a new treasure: "; }
                            */

                            //Is the NPC rare?
                            //check NPC loot table for how many rare items it can possibly drop
                            //NPCGuaranteedDrops = lootTable.GuaranteeOneDrop.Count;
                            //NPCCommonDrops = lootTable.CommonDrop.Count;
                            //NPCUncommonDrops = lootTable.UncommonDrop.Count;
                            //NPCRareDrops = lootTable.RareDrop.Count;
                            //NPCLegendaryDrops = lootTable.LegendaryDrop.Count;
                            /*if (ErenshorREL.RandomLootRestrict.Value == Toggle.On)
                            {
                                //disqualify if no guaranteed drops
                                if (NPCGuaranteedDrops <= 0) { NPCEligible = false; }
                            }*/
                            //new method -- based on number of drops:
                            NPCMaxItemDrops = lootTable.MaxNumberDrops;
                            NPCMaxNonCommonDrops = lootTable.MaxNonCommonDrops;
                            NPCActualDrops = lootTable.ActualDrops.Count;
                            if ((NPCMaxNonCommonDrops > 2) || (NPCActualDrops > 2)) { isRareNPC = true; }


                            //check resulting level calculations
                            int minLevel = __instance.MyStats.Level - ErenshorREL.RandomLootLevelsBelow.Value;
                            if (minLevel <= 0) { minLevel = 1; }
                            int maxLevel = __instance.MyStats.Level + ErenshorREL.RandomLootLevelsAbove.Value;
                            if (maxLevel > 100) { maxLevel = 100; }
                            if (maxLevel <= 0) { maxLevel = 1; }

                            float NPCDistance = Vector3.Distance(GameData.PlayerControl.transform.position, __instance.transform.position);
                            if (NPCDistance < randomLootDistance) { isNPCWithinDistance = true; }



                            float randomRoll = UnityEngine.Random.Range(0f, 100f);
                            if (isRareNPC)
                            {
                                requiredChance = ErenshorREL.RandomLootRareNPCChance.Value;
                            }
                            else
                            {
                                requiredChance = ErenshorREL.RandomLootCommonNPCChance.Value;
                            }
                            if (randomRoll <= requiredChance) { randomLootSuccess = true; }

                            //determine possible loot list by using the level ranges
                            List<Item> possibleLoot = new List<Item>();
                            if (isNPCWithinDistance && randomLootSuccess)
                            {

                                for (int i = minLevel; i <= maxLevel; i++)
                                {
                                    possibleLoot.AddRange(GenerateItemLists.ItemsByLevel[i]);
                                }

                                //= GenerateItemLists.ItemsByLevel[__instance.MyStats.Level];
                                if (possibleLoot != null && possibleLoot.Count > 0)
                                {
                                    // Select a random item from the list
                                    Item randomLoot = possibleLoot[UnityEngine.Random.Range(0, possibleLoot.Count)];

                                    // Add the item to the drop table
                                    lootTable.ActualDrops.Add(randomLoot);

                                    // Log the loot addition for debugging purposes
                                    if (ErenshorREL.RandomLootDebug.Value == Toggle.On) { UpdateSocialLog.LogAdd($"Generate Loot for {__instance.transform.name}: Level: {__instance.MyStats.Level} [{minLevel} to {maxLevel}] containing {possibleLoot.Count} items ", "yellow"); }
                                    // Output to the player
                                    UpdateSocialLog.LogAdd($"{ErenshorREL.RandomLootText.Value} + { randomLoot.ItemName} to { __instance.transform.name}", "green");
                                }
                            }
                            else { //unsuccessful for one or more reasons
                                if (ErenshorREL.RandomLootDebug.Value == Toggle.On) 
                                {
                                    string unsuccessfulReason = "";
                                    int reasonsCount = 0;
                                    if (!isNPCWithinDistance) {reasonsCount++; unsuccessfulReason = unsuccessfulReason + $"{reasonsCount}. NPC too far ({NPCDistance})."; }
                                    //if (!NPCEligible) { unsuccessfulReason = unsuccessfulReason + $" NPC doesn't have enough rare loot normally (Guaranteed Drops:{NPCGuaranteedDrops}, Common:{NPCCommonDrops}, Uncommon:{NPCUncommonDrops}, Rare:{NPCRareDrops}, Legendary:{NPCLegendaryDrops})."; }
                                    //if (!NPCEligible) { reasonsCount++; unsuccessfulReason = unsuccessfulReason + $"{reasonsCount}. NPC doesn't have Guaranteed Drops."; }
                                    if (!randomLootSuccess) { reasonsCount++; unsuccessfulReason = unsuccessfulReason + $"{reasonsCount}. Roll of {randomRoll} does not fall within required chance {requiredChance} (Rare Assumed:{isRareNPC})"; }
                                    UpdateSocialLog.LogAdd($"DO NOT Generate Loot because:{unsuccessfulReason}", "yellow"); 
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}