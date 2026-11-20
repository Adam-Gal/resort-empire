using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Runs the guests of every wave and pays for them. Money comes for each request served,
// reputation when the wave ends. Runs just before WaveManager, so a wave sees its guests up to date.
[DefaultExecutionOrder(-75)]
public class GuestManager : MonoBehaviour
{
    public static GuestManager Instance { get; private set; }

    [SerializeField] private GuestSettings settings;
    [SerializeField] private string saveFileName = "guests_save.json";

    //How often running waves are written to disk, so a crash loses at most this much
    [SerializeField, Min(1f)] private float autosaveSeconds = 5f;

    private static readonly BuildingCategory[] ServiceCategories =
    {
        BuildingCategory.Rooms, BuildingCategory.Food, BuildingCategory.Drinks,
        BuildingCategory.Pool, BuildingCategory.Spa, BuildingCategory.Tennis, BuildingCategory.Activities
    };

    private readonly Dictionary<string, GuestSimulation> simulations = new Dictionary<string, GuestSimulation>();
    private readonly List<ServicePoint> points = new List<ServicePoint>();
    private float sinceSave;

    public GuestSettings Settings => settings;

    // A wave's guests all checked out and were paid for. The simulation keeps its final counts.
    public event Action<GuestSimulation> GuestsFinished;

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private string TempSavePath => SavePath + ".tmp";

    private void Awake()
    {
        Instance = this;
        foreach (string problem in settings.Validate())
            Debug.LogError($"[GuestManager] Guest settings: {problem}", settings);
    }

    private void Start()
    {
        Load();
        WaveManager.Instance.WaveStarted += OnWaveStarted;
        WaveManager.Instance.WaveEnded += OnWaveEnded;

        // Waves loaded from the save get their guests back, or new ones for an older save.
        foreach (WaveSession session in WaveManager.Instance.Sessions) Attach(session);
        DropStale();
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.WaveStarted -= OnWaveStarted;
            WaveManager.Instance.WaveEnded -= OnWaveEnded;
        }
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        WaveManager waves = WaveManager.Instance;
        if (waves == null) return;

        // Play time only, like the waves themselves.
        float seconds = Time.deltaTime;
        foreach (WaveSession session in waves.Sessions)
        {
            if (session.Phase == WavePhase.Ended) continue;

            GuestSimulation simulation = Attach(session);
            CollectPoints(waves, session.IslandId);
            simulation.Tick(seconds, session.IsAcceptingRequests, session.ElapsedSeconds, session.RequestWindowSeconds, points);
            PayRequests(simulation);
        }

        sinceSave += seconds;
        if (sinceSave >= autosaveSeconds) Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    private void OnApplicationQuit() => Save();

    // Guests of the wave running on the island, null when none runs.
    public GuestSimulation GetSimulation(string islandId)
    {
        simulations.TryGetValue(islandId, out GuestSimulation simulation);
        return simulation;
    }

    private void OnWaveStarted(WaveSession session)
    {
        Attach(session);
        Save();
    }

    private void OnWaveEnded(WaveSession session)
    {
        if (!simulations.TryGetValue(session.IslandId, out GuestSimulation simulation)) return;

        simulation.CheckOutEveryone();
        PayRequests(simulation);
        PayReputation(simulation);
        simulations.Remove(session.IslandId);
        Save();
        GuestsFinished?.Invoke(simulation);
    }

    // The guests of the session, made when it has none yet.
    private GuestSimulation Attach(WaveSession session)
    {
        if (!simulations.TryGetValue(session.IslandId, out GuestSimulation simulation) || simulation.Data.waveNumber != session.Number)
        {
            uint seed = (uint)(session.Number * 7919) ^ (uint)DateTime.UtcNow.Ticks;
            simulation = GuestSimulation.Create(settings, session.IslandId, session.Number, seed);
            simulations[session.IslandId] = simulation;
        }

        if (session.Requests != simulation) session.Requests = simulation;
        return simulation;
    }

    // Saved guests of waves that are no longer running.
    private void DropStale()
    {
        var stale = new List<string>();
        foreach (KeyValuePair<string, GuestSimulation> pair in simulations)
        {
            WaveSession session = WaveManager.Instance.GetSession(pair.Key);
            if (session == null || session.Number != pair.Value.Data.waveNumber) stale.Add(pair.Key);
        }
        foreach (string island in stale) simulations.Remove(island);
    }

    private void CollectPoints(WaveManager waves, string islandId)
    {
        points.Clear();
        foreach (BuildingCategory category in ServiceCategories)
        {
            foreach (PlacedBuilding building in waves.GetBuildings(islandId, category))
            {
                points.Add(new ServicePoint(
                    WaveManager.BuildingKey(building),
                    category,
                    building.Definition.CapacityAt(building.Level),
                    waves.GetServiceSpeed(islandId, building)));
            }
        }
    }

    private void PayRequests(GuestSimulation simulation)
    {
        GuestWaveData data = simulation.Data;
        int unpaid = data.servedRequests - data.paidRequests;
        if (unpaid <= 0) return;

        data.paidRequests = data.servedRequests;
        ResourceManager.Instance.Add(ResourceType.Money, (long)unpaid * settings.moneyPerRequest);
    }

    // Every guest's rating is worth some reputation, the wave gives or takes their sum.
    private void PayReputation(GuestSimulation simulation)
    {
        long change = simulation.ReputationChange;
        ResourceManager resources = ResourceManager.Instance;

        if (change > 0) resources.Add(ResourceType.Reputation, change);
        else if (change < 0)
        {
            long loss = Math.Min(-change, resources.GetAmount(ResourceType.Reputation));
            if (loss > 0) resources.TrySpend(ResourceType.Reputation, loss);
        }
    }

    private void Load()
    {
        GuestSaveData data = ReadSaveFile();
        if (data == null) return;

        foreach (GuestWaveData wave in data.waves)
        {
            if (wave == null || string.IsNullOrEmpty(wave.islandId)) continue;
            wave.guests ??= new List<GuestData>();
            simulations[wave.islandId] = new GuestSimulation(settings, wave);
        }
    }

    private GuestSaveData ReadSaveFile()
    {
        string path = File.Exists(SavePath) ? SavePath : (File.Exists(TempSavePath) ? TempSavePath : null);
        if (path == null) return null;

        try
        {
            return JsonUtility.FromJson<GuestSaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[GuestManager] Save file is corrupt, starting without guests: {e.Message}");
            return null;
        }
    }

    public void Save()
    {
        sinceSave = 0f;
        var data = new GuestSaveData();
        foreach (GuestSimulation simulation in simulations.Values) data.waves.Add(simulation.Data);

        try
        {
            // Written next to the old file first, so an interrupted save keeps a whole one.
            File.WriteAllText(TempSavePath, JsonUtility.ToJson(data));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(TempSavePath, SavePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GuestManager] Failed to save guests: {e.Message}");
        }
    }

    [ContextMenu("Debug/Delete Save File")]
    private void DebugDeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempSavePath)) File.Delete(TempSavePath);
        Debug.Log($"[GuestManager] Deleted save at {SavePath}.");
    }
}
