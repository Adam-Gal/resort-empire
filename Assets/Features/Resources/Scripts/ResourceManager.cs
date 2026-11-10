using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


[DefaultExecutionOrder(-100)]
public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private TextAsset databaseJson;

    [Header("Saving")]
    [SerializeField] private string saveFileName = "resources_save.json";
    [SerializeField] private bool showStructuredDbPrint = false;

    [Header("Debug")]
    [SerializeField] private bool showChangesInLog = false;

    //type, new amount, delta (positive = gained, negative = spent)
    public event Action<ResourceType, long, long> ResourceChanged;

    //called once
    public event Action<StarterGiftData> StarterGiftClaimed;

    private ResourceDatabaseData database;
    private ResourceSaveData saveData;
    private readonly Dictionary<ResourceType, GameResource> resources = new Dictionary<ResourceType, GameResource>();
    private bool isChanged;

    public bool IsInitialized { get; private set; }
    public ResourceDatabaseData Database => database;
    public EnergySettings EnergySettings => database.energySettings;
    public StarterGiftData StarterGift => database.starterGift;

    public MoneyResource Money => saveData.money;
    public EnergyResource Energy => saveData.energy;

    public bool HasPendingStarterGift =>
        IsInitialized &&
        !saveData.starterGiftClaimed &&
        database.starterGift != null &&
        database.starterGift.enabled &&
        database.starterGift.rewards != null &&
        database.starterGift.rewards.Count > 0;

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private string TempSavePath => SavePath + ".tmp";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadDatabase();
        LoadSave();
        IsInitialized = true;
    }

    private void LateUpdate()
    {
        if (isChanged) 
        Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && IsInitialized) Save();
    }

    private void OnApplicationQuit()
    {
        if (IsInitialized) Save();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    public GameResource GetResource(ResourceType type)
    {
        resources.TryGetValue(type, out GameResource resource);
        return resource;
    }

    public long GetAmount(ResourceType type)
    {
        GameResource resource = GetResource(type);
        return resource != null ? resource.Amount : 0;
    }

    public ResourceDefinition GetDefinition(ResourceType type)
    {
        return database.GetDefinition(type);
    }

    public string GetDisplayName(ResourceType type)
    {
        ResourceDefinition def = GetDefinition(type);
        return def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName : type.ToString();
    }

    public bool CanAfford(ResourceType type, long amount)
    {
        GameResource resource = GetResource(type);
        return resource != null && resource.CanAfford(amount);
    }

    //check price func
    public bool CanAfford(IList<ResourceCost> costs)
    {
        if (costs == null) return true;

        Dictionary<ResourceType, long> totals = SumCosts(costs);
        foreach (KeyValuePair<ResourceType, long> pair in totals)
        {
            if (!CanAfford(pair.Key, pair.Value)) return false;
        }
        return true;
    }

    // Add resources func
    public long Add(ResourceType type, long amount, bool ignoreCap = false)
    {
        GameResource resource = GetResource(type);
        if (resource == null || amount <= 0) return 0;

        long added = resource.Add(amount, ignoreCap);
        if (added > 0) NotifyChanged(resource, added);
        return added;
    }

    public bool TrySpend(ResourceType type, long amount)
    {
        GameResource resource = GetResource(type);
        if (resource == null || amount < 0) return false;
        if (amount == 0) return true;

        if (!resource.TrySpend(amount)) return false;

        NotifyChanged(resource, -amount);
        return true;
    }

    /// <summary>
    /// Spends a full price, all or nothing. Use this when placing a building.
    /// </summary>
    public bool TrySpend(IList<ResourceCost> costs)
    {
        if (!CanAfford(costs)) return false;

        Dictionary<ResourceType, long> totals = SumCosts(costs);
        foreach (KeyValuePair<ResourceType, long> pair in totals)
        {
            TrySpend(pair.Key, pair.Value);
        }
        return true;
    }

    public long GetSpeedUpCost(float remainingSeconds)
    {
        return EnergyResource.CalculateSpeedUpCost(remainingSeconds, database.energySettings);
    }

    public bool CanAffordSpeedUp(float remainingSeconds)
    {
        return CanAfford(ResourceType.Energy, GetSpeedUpCost(remainingSeconds));
    }

    // energy spend func
    public bool TrySpeedUp(float remainingSeconds)
    {
        long cost = GetSpeedUpCost(remainingSeconds);
        return cost <= 0 || TrySpend(ResourceType.Energy, cost);
    }

    // starter gift func
    public bool ClaimStarterGift()
    {
        if (!HasPendingStarterGift) return false;

        StarterGiftData gift = database.starterGift;
        saveData.starterGiftClaimed = true;

        foreach (ResourceReward reward in gift.rewards)
        {
            if (reward.TryGetType(out ResourceType type))
                Add(type, reward.amount, true);
            else
                Debug.LogWarning($"[ResourceManager] Starter gift has unknown resource id '{reward.resourceId}'.");
        }

        Save();
        StarterGiftClaimed?.Invoke(gift);
        return true;
    }


    private void LoadDatabase()
    {
        database = null;

        if (databaseJson != null)
        {
            try
            {
                database = JsonUtility.FromJson<ResourceDatabaseData>(databaseJson.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceManager] Could not parse resource database: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("[ResourceManager] No database JSON assigned. Using empty defaults.");
        }

        if (database == null) database = new ResourceDatabaseData();
        if (database.resources == null) database.resources = new List<ResourceDefinition>();
        if (database.energySettings == null) database.energySettings = new EnergySettings();
        if (database.starterGift == null) database.starterGift = new StarterGiftData { enabled = false };

        foreach (ResourceDefinition def in database.resources)
        {
            if (!def.TryGetType(out _))
                Debug.LogWarning($"[ResourceManager] Database entry '{def.id}' has no matching ResourceType.");
        }
    }

    private void LoadSave()
    {
        saveData = ReadSaveFile();
        bool isNewSave = saveData == null;

        if (isNewSave) saveData = new ResourceSaveData();
        if (saveData.money == null) saveData.money = new MoneyResource();
        if (saveData.energy == null) saveData.energy = new EnergyResource();

        RegisterResources();

        foreach (GameResource resource in resources.Values)
        {
            ResourceDefinition def = database.GetDefinition(resource.Type);
            long max = def != null ? def.maxAmount : 0;

            if (isNewSave)
                resource.Initialize(def != null ? def.startAmount : 0, max);
            else
                resource.SetMaxAmount(max);
        }

        if (isNewSave) Save();
    }

    private void RegisterResources()
    {
        resources.Clear();
        resources[ResourceType.Money] = saveData.money;
        resources[ResourceType.Energy] = saveData.energy;
    }

    private ResourceSaveData ReadSaveFile()
    {
        string path = File.Exists(SavePath) ? SavePath : (File.Exists(TempSavePath) ? TempSavePath : null);
        if (path == null) return null;

        try
        {
            return JsonUtility.FromJson<ResourceSaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[ResourceManager] Save file is corrupt, starting fresh: {e.Message}");
            return null;
        }
    }

    public void Save()
    {
        if (saveData == null) return;

        try
        {
            string json = JsonUtility.ToJson(saveData, showStructuredDbPrint);
            File.WriteAllText(TempSavePath, json);
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(TempSavePath, SavePath);
            isChanged = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ResourceManager] Failed to save resources: {e.Message}");
        }
    }

    private void NotifyChanged(GameResource resource, long delta)
    {
        isChanged = true;

        if (showChangesInLog)
            Debug.Log($"[ResourceManager] {resource.Type} {(delta >= 0 ? "+" : "")}{delta} -> {resource.Amount}");

        ResourceChanged?.Invoke(resource.Type, resource.Amount, delta);
    }

    private static Dictionary<ResourceType, long> SumCosts(IList<ResourceCost> costs)
    {
        Dictionary<ResourceType, long> totals = new Dictionary<ResourceType, long>();
        if (costs == null) return totals;

        for (int i = 0; i < costs.Count; i++)
        {
            if (costs[i].amount <= 0) continue;
            totals.TryGetValue(costs[i].type, out long current);
            totals[costs[i].type] = current + costs[i].amount;
        }
        return totals;
    }

    // cheats :D

    [ContextMenu("Debug/Add 1000 Money")]
    private void DebugAddMoney() => Add(ResourceType.Money, 1000);

    [ContextMenu("Debug/Add 10 Energy")]
    private void DebugAddEnergy() => Add(ResourceType.Energy, 10, true);

    [ContextMenu("Debug/Delete Save File")]
    private void DebugDeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempSavePath)) File.Delete(TempSavePath);
        Debug.Log($"[ResourceManager] Deleted save at {SavePath}. Restart play mode to see the starter gift again.");

        saveData = null;
        IsInitialized = false;
    }
}
