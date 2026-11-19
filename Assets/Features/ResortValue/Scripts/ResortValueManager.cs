using System;
using System.IO;
using UnityEngine;

// What the whole resort is worth: the price of everything built and upgraded on it,
// scaled by how guests rated past waves. Good waves raise it, long waits lower it.
// Runs early, so the header finds it when it is enabled.
[DefaultExecutionOrder(-60)]
public class ResortValueManager : MonoBehaviour
{
    public static ResortValueManager Instance { get; private set; }

    [SerializeField] private ResortValueSettings settings;
    [SerializeField] private string saveFileName = "resort_value_save.json";

    // The value changed, with the new value.
    public event Action<long> ValueChanged;

    public long Value { get; private set; }

    // Money paid for the buildings standing now and their upgrades.
    public long BuildingsWorth { get; private set; }

    // Guests' ratings over past waves, in stars. Not shown to the player, it only moves the value.
    public float GuestScore { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private string TempSavePath => SavePath + ".tmp";

    private void Awake()
    {
        Instance = this;
        foreach (string problem in settings.Validate())
            Debug.LogError($"[ResortValueManager] Resort value settings: {problem}", settings);
        Load();
    }

    private void Start()
    {
        if (BuildingManager.Instance != null) BuildingManager.Instance.Changed += Recalculate;
        if (GuestManager.Instance != null) GuestManager.Instance.GuestsFinished += OnGuestsFinished;
        Recalculate();
    }

    private void OnDestroy()
    {
        if (BuildingManager.Instance != null) BuildingManager.Instance.Changed -= Recalculate;
        if (GuestManager.Instance != null) GuestManager.Instance.GuestsFinished -= OnGuestsFinished;
        if (Instance == this) Instance = null;
    }

    private void OnGuestsFinished(GuestSimulation guests)
    {
        if (guests.RatedGuests == 0) return;

        GuestScore = settings.ScoreAfterWave(GuestScore, guests.AverageRating);
        Save();
        Recalculate();
    }

    private void Recalculate()
    {
        BuildingsWorth = CountBuildingsWorth();
        long value = (long)Math.Round(BuildingsWorth * (double)settings.MultiplierFor(GuestScore));
        if (value == Value) return;

        Value = value;
        ValueChanged?.Invoke(Value);
    }

    private long CountBuildingsWorth()
    {
        BuildingManager buildings = BuildingManager.Instance;
        if (buildings == null) return 0;

        long worth = 0;
        foreach (PlacedBuilding building in buildings.Placed)
        {
            if (building == null) continue;
            BuildingDefinition definition = building.Definition;
            if (definition.IsDecoration && !settings.countDecorations) continue;

            worth += Money(definition.cost);
            for (int level = 1; level <= building.Level && definition.HasLevel(level); level++)
                worth += Money(definition.levels[level].upgradeCost);
        }
        return worth;
    }

    private static long Money(ResourceCost[] costs)
    {
        long money = 0;
        if (costs == null) return money;
        foreach (ResourceCost cost in costs)
            if (cost.type == ResourceType.Money) money += cost.amount;
        return money;
    }

    private void Load()
    {
        GuestScore = settings.startingScore;
        string path = File.Exists(SavePath) ? SavePath : (File.Exists(TempSavePath) ? TempSavePath : null);
        if (path == null) return;

        try
        {
            ResortValueSaveData data = JsonUtility.FromJson<ResortValueSaveData>(File.ReadAllText(path));
            if (data != null && data.hasScore) GuestScore = Mathf.Clamp(data.guestScore, 1f, 5f);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ResortValueManager] Save file is corrupt, starting from the default score: {e.Message}");
        }
    }

    private void Save()
    {
        var data = new ResortValueSaveData { guestScore = GuestScore, hasScore = true };
        try
        {
            // Written next to the old file first, so an interrupted save keeps a whole one.
            File.WriteAllText(TempSavePath, JsonUtility.ToJson(data));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(TempSavePath, SavePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ResortValueManager] Failed to save resort value: {e.Message}");
        }
    }

    [ContextMenu("Debug/Delete Save File")]
    private void DebugDeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempSavePath)) File.Delete(TempSavePath);
        Debug.Log($"[ResortValueManager] Deleted save at {SavePath}.");
    }
}
