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
        internal const string ModVersion = "0.0.1";
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
        internal static ConfigEntry<float> RandomLootChance = null!;
        //internal static ConfigEntry<KeyboardShortcut> AutoRunKey = null!;
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

        /*
        internal ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, string desc)
        {
            ConfigurationManagerAttributes attributes = new()
            {
                CustomDrawer = Utilities.TextAreaDrawer
            };
            return Config.Bind(group, name, value, new ConfigDescription(desc, null, attributes));
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }*/

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
                bool itemListDebug = true;
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
                    if (item.ItemLevel >= 0 && item.ItemLevel <= maxItemLevel)
                    {
                        ItemsByLevel[item.ItemLevel].Add(item);
                    }
                }

                if (itemListDebug)
                {
                    //DEBUG Output the results:
                    string filePath = System.IO.Path.Combine(Application.persistentDataPath, "ItemsByLevel.txt");
                    //string filePath = "C:\\Users\\brumd\\AppData\\LocalLow\\Burgee Media\\Erenshor\\ItemsByLevel.txt";
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        for (int level = 0; level < ItemsByLevel.Length; level++)
                        {
                            List<Item> itemsAtLevel = ItemsByLevel[level];
                            if (itemsAtLevel != null && itemsAtLevel.Count > 0)
                            {
                                //Debug.Log($"{itemsAtLevel.Count} Items at level {level}:");
                                writer.WriteLine($"{itemsAtLevel.Count} Items at level {level}:");
                                foreach (Item item in itemsAtLevel)
                                {
                                    //Debug.Log($"- ID:{item.Id} {item.ItemName}");
                                    writer.WriteLine($"- ID:{item.Id} {item.ItemName} Relic:{item.Relic} Unique:{item.Unique} Classes:{item.Classes.ToString()} Slot:{item.RequiredSlot.ToString()} Value:{item.ItemValue}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}