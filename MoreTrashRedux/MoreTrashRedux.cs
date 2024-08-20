using UnityEngine;
using System.IO;
using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using HMLLibrary;
using RaftModLoader;
using System.Runtime.CompilerServices;

public class MoreTrashRedux : Mod
{
    static Dictionary<ObjectSpawnerAssetSettings, (float,float)> editedValues;
    static public JsonModInfo modInfo;
    static public bool DiffCheck;
    static public float GlobalDelay;
    static public float WorldDelay;
    static public bool GlobalCheck;
    static public bool WorldCheck;
    static public bool scheduleCheck;
    static bool debugLogging = false;
    static bool commandSet = false;
    Harmony harmony;
    static public float multiplier
    {
        get
        {
            if (ExtraSettingsAPI_Loaded || commandSet)
            {
                if (DiffCheck)
                    return GlobalCheck ? float.PositiveInfinity : GlobalDelay;
                return WorldCheck ? float.PositiveInfinity : WorldDelay;
            }
            return 0.5f;
        }
    }
    public void Start()
    {
        modInfo = modlistEntry.jsonmodinfo;
        editedValues = new Dictionary<ObjectSpawnerAssetSettings, (float, float)>();
        ModifyCurrentSettings();
        (harmony = new Harmony("com.aidanamite.MoreTrashRedux")).PatchAll();
        Log("Mod has been loaded!");
    }

    public void Update()
    {
        if (scheduleCheck)
        {
            if (debugLogging)
                Log("Scheduled settings check start");
            getSettings();
            ModifyCurrentSettings();
            scheduleCheck = false;
        }
    }

    [ConsoleCommand(name: "toggleMoreTrashReduxLogging", docs: "Enables debug logging for the More Trash Redux mod")]
    public static string MyCommand(string[] args)
    {
        return $"Logging is now {debugLogging = !debugLogging}";
    }

    [ConsoleCommand(name: "iteminterval", docs: "Usage: iteminterval <multiplier> - Sets the multiplier for the item spawn delay")]
    public static string MyCommand2(string[] args)
    {
        if (args == null || args.Length < 1)
            return "Delay multiplier is " + multiplier;
        GlobalDelay = Parse(args[0]);
        if (GlobalDelay < 0.00001f)
            GlobalDelay = 0.00001f;
        ExtraSettingsAPI_SetInputValue("Spawn Delay",GlobalDelay.ToString());
        if (!DiffCheck)
        {
            DiffCheck = true;
            ExtraSettingsAPI_SetCheckboxState("Use global settings",DiffCheck);
        }
        if (GlobalCheck)
        {
            GlobalCheck = false;
            ExtraSettingsAPI_SetCheckboxState("Disable Trash Spawn", GlobalCheck);
        }
        if (!ExtraSettingsAPI_Loaded)
            commandSet = true;
        RemodifyAll();
        return "Delay multiplier is now " + GlobalDelay;
    }

    public void OnModUnload()
    {
        UnmodifyAll();
        harmony.UnpatchAll(harmony.Id);
        Log("Mod has been unloaded!");
    }

    public static void LogError(object message)
    {
        Debug.LogError("[" + modInfo.name + "]: " + message.ToString());
    }

    public static void WarningLog(object message)
    {
        Debug.LogWarning("[" + modInfo.name + "]: " + message.ToString());
    }

    public static void Log(object message)
    {
        Debug.Log("[" + modInfo.name + "]: " + message.ToString());
    }

    public static void ModifyCurrentSettings()
    {
        if (debugLogging)
            Log("Attempting to modify current settings with multiplier " + multiplier);
        var manager = ComponentManager<ObjectSpawnerManager>.Value;
        if (manager != null)
            foreach (var objectSpawner in new ObjectSpawner_RaftDirection[] { manager.plankSpawner, manager.itemSpawner })
                ModifySettings(Traverse.Create(objectSpawner).Field("currentSettings").GetValue<ObjectSpawnerAssetSettings>());
    }

    public static void ModifySettings(ObjectSpawnerAssetSettings set)
    {
        if (set == null)
        {
            if (debugLogging)
                Log("Attempted to modify a null set");
            return;
        }
        if (editedValues == null)
        {
            if (debugLogging)
                Log("Edited value memory is null");
            return;
        }
        if (editedValues.TryAdd(set, (set.spawnRateInterval.minValue, set.spawnRateInterval.maxValue)))
        {
            if (debugLogging)
                Log("Added new set to memory cache. Modifying values");
            set.spawnRateInterval.minValue *= multiplier;
            set.spawnRateInterval.maxValue *= multiplier;
        }
        else
            RemodifySettings(set);
    }

    public static void RemodifySettings(ObjectSpawnerAssetSettings set)
    {
        if (editedValues.ContainsKey(set))
        {
            if (debugLogging)
                Log("Remodifying set based on cached values");
            set.spawnRateInterval.minValue = editedValues[set].Item1 * multiplier;
            set.spawnRateInterval.maxValue = editedValues[set].Item2 * multiplier;
        }
        else if (debugLogging)
            Log("Could not remodify set. Not in cache");
    }

    public static void RemodifyAll()
    {
        if (editedValues == null || editedValues.Count <= 0)
        {
            if (debugLogging)
                Log("Could not remodify all, cache was " + editedValues == null ? "null" : "empty");
            return;
        }
        if (debugLogging)
            Log("Attempting to remodify all settings with multiplier " + multiplier);
        foreach (var pair in editedValues)
        {
            if (debugLogging)
                Log("Remodifying set based on cached values");
            pair.Key.spawnRateInterval.minValue = pair.Value.Item1 * multiplier;
            pair.Key.spawnRateInterval.maxValue = pair.Value.Item2 * multiplier;
        }
        foreach (var objectSpawner in new ObjectSpawner_RaftDirection[] { ComponentManager<ObjectSpawnerManager>.Value.plankSpawner, ComponentManager<ObjectSpawnerManager>.Value.itemSpawner })
            Traverse.Create(objectSpawner).Field("spawnDelay").SetValue(Traverse.Create(objectSpawner).Field("currentSettings").GetValue<ObjectSpawnerAssetSettings>().spawnRateInterval.GetRandomValue());
    }

    public static void UnmodifyAll()
    {
        if (editedValues == null || editedValues.Count <= 0)
        {
            if (debugLogging)
                Log("Could not unmodify all, cache was " + editedValues == null ? "null" : "empty");
            return;
        }
        foreach (var pair in editedValues)
        {
            if (debugLogging)
                Log("Unmodifying set based on cached values");
            pair.Key.spawnRateInterval.minValue = pair.Value.Item1;
            pair.Key.spawnRateInterval.maxValue = pair.Value.Item2;
        }
        editedValues.Clear();
    }

    public void getSettings()
    {
        if (!ExtraSettingsAPI_Loaded)
        {
            if (debugLogging)
                Log("Settings fetch failed. the Extra Settings API does not appear to be loaded");
            return;
        }
        DiffCheck = ExtraSettingsAPI_GetCheckboxState("Use global settings");
        if (debugLogging)
            Log("Use Globals setting has been fetched as " + DiffCheck);
        GlobalCheck = ExtraSettingsAPI_GetCheckboxState("Disable Trash Spawn");
        if (debugLogging)
            Log("Global Disable Trash setting has been fetched as " + GlobalCheck);
        try
        {
            GlobalDelay = Parse(ExtraSettingsAPI_GetInputValue("Spawn Delay"));
            if (GlobalDelay < 0.00001)
            {
                Log($"Global Spawn Delay setting has been fetched as {GlobalDelay} which is too small. Defaulted to 0.00001");
                GlobalDelay = 0.00001f;
            }
            else if (debugLogging)
                Log($"Global Spawn Delay setting has been fetched as {GlobalDelay}");
        }
        catch (Exception e)
        {
            GlobalDelay = 1;
            LogError($"Couldn't parse \"{ExtraSettingsAPI_GetInputValue("Spawn Delay")}\" as float for Global Delay Multiplier\n{e}");
        }
        WorldCheck = ExtraSettingsAPI_GetCheckboxState("Disable Trash Spawn ");
        if (debugLogging)
            Log("World Disable Trash setting has been fetched as " + WorldCheck);
        try
        {
            WorldDelay = Parse(ExtraSettingsAPI_GetInputValue("Spawn Delay "));
            if (WorldDelay < 0.00001)
            {
                Log($"World Spawn Delay setting has been fetched as {WorldDelay} which is too small. Defaulted to 0.00001");
                WorldDelay = 0.00001f;
            }
            else if (debugLogging)
                Log($"World Spawn Delay setting has been fetched as {WorldDelay}");
        }
        catch (Exception e)
        {
            WorldDelay = 1;
            LogError($"Couldn't parse \"{ExtraSettingsAPI_GetInputValue("Spawn Delay ")}\" as float for World Delay Multiplier\n{e}");
        }
    }

    static float Parse(string value, float fallback = 1)
    {
        if (debugLogging)
            Log($"Attempting to parse \"{value}\"");
        if (string.IsNullOrWhiteSpace(value))
        {
            if (debugLogging)
                Log("The value that was attempted to parse was " + (string.IsNullOrEmpty(value) ? "null/empty" : "whitespace") + ". returning " + fallback);
            return fallback;
        }
        if (value.Contains(",") && !value.Contains("."))
            value = value.Replace(',', '.');
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            if (debugLogging)
                Log("Successfully parsed value " + v);
            return v;
        }
        if (debugLogging)
            Log("Failed to parse the value. returning " + fallback);
        return fallback;
    }

    public void ExtraSettingsAPI_SettingsClose()
    {
        getSettings();
        RemodifyAll();
    }
    public void ExtraSettingsAPI_Load()
    {
        commandSet = false;
        getSettings();
        RemodifyAll();
    }
    public void ExtraSettingsAPI_Unload()
    {
        RemodifyAll();
    }
    public void ExtraSettingsAPI_ButtonPress(string name)
    {
        if (name == "Clear Items")
            if (Raft_Network.IsHost)
                foreach (ObjectSpawner_RaftDirection objectSpawner in FindObjectsOfType<ObjectSpawner_RaftDirection>())
                    while (objectSpawner.spawnedObjects.Count > 0)
                        PickupObjectManager.RemovePickupItemNetwork(objectSpawner.spawnedObjects[0]);
    }

    static bool ExtraSettingsAPI_Loaded = false;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ExtraSettingsAPI_GetCheckboxState(string SettingName)
    {
        if (ExtraSettingsAPI_Loaded)
            LogError("Settings API has not patched the mod correctly");
        return false;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string ExtraSettingsAPI_GetInputValue(string SettingName)
    {
        if (ExtraSettingsAPI_Loaded)
            LogError("Settings API has not patched the mod correctly");
        return "";
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ExtraSettingsAPI_SetCheckboxState(string SettingName, bool value)
    {
        if (ExtraSettingsAPI_Loaded)
            LogError("Settings API has not patched the mod correctly");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ExtraSettingsAPI_SetInputValue(string SettingName, string value)
    {
        if (ExtraSettingsAPI_Loaded)
            LogError("Settings API has not patched the mod correctly");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ExtraSettingsAPI_SaveSettings()
    {
        if (ExtraSettingsAPI_Loaded)
            LogError("Settings API has not patched the mod correctly");
    }
}

[HarmonyPatch(typeof(SO_ObjectSpawner), "GetSettings")]
public class Patch_GetObjectSpawnerSettings
{
    static void Postfix()
    {
        MoreTrashRedux.scheduleCheck = true;
    }
}