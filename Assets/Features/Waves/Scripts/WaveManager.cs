using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Guest waves, at most one running on each island. The player always starts them,
// they never start on their own. Waves only advance while the game is played.
[DefaultExecutionOrder(-70)]
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [SerializeField] private WaveSettings settings;
    [SerializeField] private string saveFileName = "waves_save.json";

    // A wave started, ended, or something that decides whether one can start changed.
    public event Action WavesChanged;
    public event Action<WaveSession> WaveStarted;
    public event Action<WaveSession> WaveEnded;

    private readonly Dictionary<string, WaveSession> sessions = new Dictionary<string, WaveSession>();
    private readonly Dictionary<string, int> waveCounts = new Dictionary<string, int>();
    private readonly List<WaveSession> ended = new List<WaveSession>();
    private float sinceSave;
    private bool isChanged;

    public WaveSettings Settings => settings;

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private string TempSavePath => SavePath + ".tmp";

    private void Awake()
    {
        Instance = this;
        foreach (string problem in settings.Validate())
            Debug.LogError($"[WaveManager] Wave settings: {problem}", settings);
        Load();
    }

    private void Start()
    {
        if (BuildingManager.Instance != null) BuildingManager.Instance.Changed += OnWorldChanged;
        if (IslandProgress.Instance != null) IslandProgress.Instance.Changed += OnWorldChanged;
    }

    private void OnDestroy()
    {
        if (BuildingManager.Instance != null) BuildingManager.Instance.Changed -= OnWorldChanged;
        if (IslandProgress.Instance != null) IslandProgress.Instance.Changed -= OnWorldChanged;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (sessions.Count == 0) return;

        // Play time only, so a closed or backgrounded game pauses every wave.
        float seconds = Time.deltaTime;
        foreach (WaveSession session in sessions.Values)
        {
            if (session.Advance(seconds)) isChanged = true;
            if (session.Phase == WavePhase.Ended) ended.Add(session);
        }

        foreach (WaveSession session in ended)
        {
            sessions.Remove(session.IslandId);
            WaveEnded?.Invoke(session);
        }
        ended.Clear();

        sinceSave += seconds;
        if (isChanged || sinceSave >= settings.autosaveSeconds)
        {
            Save();
            if (isChanged) WavesChanged?.Invoke();
            isChanged = false;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    private void OnApplicationQuit() => Save();

    public WaveSession GetSession(string islandId)
    {
        sessions.TryGetValue(islandId, out WaveSession session);
        return session;
    }

    public bool IsWaveRunning(string islandId) => sessions.ContainsKey(islandId);

    // Waves ever started on the island, the running one included.
    public int GetWaveCount(string islandId)
    {
        waveCounts.TryGetValue(islandId, out int count);
        return count;
    }

    // Waves running now, one per island at most.
    public IEnumerable<WaveSession> Sessions => sessions.Values;

    // Why a wave cannot start on the island, null when it can.
    public string GetStartProblem(string islandId)
    {
        if (IsWaveRunning(islandId)) return "Na ostrove už prebieha vlna.";

        List<string> missing = GetMissingRequirements(islandId);
        return missing.Count > 0 ? "Chýba: " + string.Join(", ", missing) : null;
    }

    public bool CanStartWave(string islandId) => GetStartProblem(islandId) == null;

    // Safe to call twice in a row, the second call finds the wave running and does nothing.
    public bool TryStartWave(string islandId)
    {
        if (!CanStartWave(islandId)) return false;

        waveCounts.TryGetValue(islandId, out int count);
        waveCounts[islandId] = ++count;

        var session = new WaveSession(islandId, count, settings.requestWindowSeconds);
        sessions[islandId] = session;
        Save();
        WaveStarted?.Invoke(session);
        WavesChanged?.Invoke();
        return true;
    }

    // Basic services the island still lacks, by display name.
    public List<string> GetMissingRequirements(string islandId)
    {
        var missing = new List<string>();
        foreach (WaveSettings.Requirement requirement in settings.requirements)
        {
            if (!HasBuilding(islandId, requirement.category)) missing.Add(requirement.displayName);
        }
        return missing;
    }

    private bool HasBuilding(string islandId, BuildingCategory category) => GetBuildings(islandId, category).Count > 0;

    // How the building copes with the wave on its island, normal while no wave asks anything of it.
    // Exactly full with nobody waiting still keeps up, only a guest waiting on top makes it overloaded.
    public ServiceState GetServiceState(string islandId, PlacedBuilding building)
    {
        IWaveRequests requests = GetSession(islandId)?.Requests;
        if (requests == null) return ServiceState.Normal;

        return StateForLoad(requests.GetLoad(building), settings);
    }

    // The state a load stands for: busy from busyLoad, overloaded only above overloadedLoad.
    public static ServiceState StateForLoad(float load, WaveSettings settings)
    {
        if (load > settings.overloadedLoad + OverloadMargin) return ServiceState.Overloaded;
        return load >= settings.busyLoad ? ServiceState.Busy : ServiceState.Normal;
    }

    // Stable name of a building inside a wave and its save.
    public static string BuildingKey(PlacedBuilding building) =>
        $"{building.Definition.id}@{building.Origin.x},{building.Origin.y}";

    public const string NotEnoughEnergy = "Málo energie";

    // Why the building cannot be sped up now, null when it can.
    public string GetSpeedUpProblem(string islandId, PlacedBuilding building)
    {
        WaveSession session = GetSession(islandId);
        if (session == null) return "Vlna neprebieha";
        if (session.WasSpedUp(BuildingKey(building))) return "Už použité";
        if (!ResourceManager.Instance.CanAfford(ResourceType.Energy, settings.speedUpEnergy)) return NotEnoughEnergy;
        return null;
    }

    public bool IsSpeedUpRunning(string islandId, PlacedBuilding building) =>
        GetSession(islandId)?.IsBoosted(BuildingKey(building)) == true;

    // Play seconds the running speed up has left, 0 when none runs.
    public float GetSpeedUpSecondsLeft(string islandId, PlacedBuilding building)
    {
        WaveSession session = GetSession(islandId);
        return session != null && session.Boosts.TryGetValue(BuildingKey(building), out float left) ? left : 0f;
    }

    // How many times faster the building serves right now, for the request system.
    public float GetServiceSpeed(string islandId, PlacedBuilding building) =>
        IsSpeedUpRunning(islandId, building) ? settings.speedUpMultiplier : 1f;

    // Pays the energy and speeds the building up, nothing changes when it is not allowed.
    public bool TrySpeedUp(string islandId, PlacedBuilding building)
    {
        if (GetSpeedUpProblem(islandId, building) != null) return false;
        if (!ResourceManager.Instance.TrySpend(ResourceType.Energy, settings.speedUpEnergy)) return false;

        GetSession(islandId).TrySpeedUp(BuildingKey(building), settings.speedUpSeconds);
        Save();
        WavesChanged?.Invoke();
        return true;
    }

    // Basic services of the island asked for more than they can serve, so guests wait. Empty when all keep up.
    public List<WaveSettings.Requirement> GetOverloadedServices(string islandId)
    {
        var overloaded = new List<WaveSettings.Requirement>();
        if (GetSession(islandId)?.Requests == null) return overloaded;

        foreach (WaveSettings.Requirement requirement in settings.requirements)
        {
            if (GetWorstState(islandId, GetBuildings(islandId, requirement.category)) == ServiceState.Overloaded)
                overloaded.Add(requirement);
        }
        return overloaded;
    }

    //Loads are fractions, so exactly full can come out a hair above 1
    private const float OverloadMargin = .001f;

    // The worst state among the buildings, what a service card warns about.
    public ServiceState GetWorstState(string islandId, List<PlacedBuilding> buildings)
    {
        ServiceState worst = ServiceState.Normal;
        foreach (PlacedBuilding building in buildings)
        {
            ServiceState state = GetServiceState(islandId, building);
            if (state > worst) worst = state;
        }
        return worst;
    }

    // Buildings of one service on the island.
    public List<PlacedBuilding> GetBuildings(string islandId, BuildingCategory category)
    {
        var found = new List<PlacedBuilding>();
        BuildingManager buildings = BuildingManager.Instance;
        if (buildings == null) return found;

        foreach (PlacedBuilding building in buildings.Placed)
        {
            if (building == null || building.Definition.category != category) continue;
            Vector3 center = buildings.Grid.FootprintCenter(building.Origin, building.Definition.size);
            if (IslandIdAt(center) == islandId) found.Add(building);
        }
        return found;
    }

    private static string IslandIdAt(Vector3 world) =>
        IslandProgress.Instance != null ? IslandProgress.Instance.GetOpenIslandIdAt(world) : IslandProgress.StartingIslandId;

    private void OnWorldChanged() => WavesChanged?.Invoke();

    private void Load()
    {
        WaveSaveData data = ReadSaveFile() ?? new WaveSaveData();
        for (int i = 0; i < data.countIslands.Count && i < data.counts.Count; i++)
            waveCounts[data.countIslands[i]] = data.counts[i];

        foreach (WaveSessionSaveData entry in data.sessions)
        {
            if (entry == null || string.IsNullOrEmpty(entry.islandId) || entry.phase == WavePhase.Ended) continue;
            float window = entry.requestWindowSeconds > 0f ? entry.requestWindowSeconds : settings.requestWindowSeconds;
            var session = new WaveSession(entry.islandId, entry.number, window, entry.phase, Mathf.Max(0f, entry.elapsedSeconds));
            if (entry.spedUpBuildings != null)
            {
                foreach (string building in entry.spedUpBuildings)
                {
                    int boost = entry.boostBuildings != null ? entry.boostBuildings.IndexOf(building) : -1;
                    float left = boost >= 0 && boost < entry.boostSeconds.Count ? entry.boostSeconds[boost] : 0f;
                    session.RestoreSpeedUp(building, left);
                }
            }
            sessions[entry.islandId] = session;
        }
    }

    private WaveSaveData ReadSaveFile()
    {
        string path = File.Exists(SavePath) ? SavePath : (File.Exists(TempSavePath) ? TempSavePath : null);
        if (path == null) return null;

        try
        {
            return JsonUtility.FromJson<WaveSaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[WaveManager] Save file is corrupt, starting without waves: {e.Message}");
            return null;
        }
    }

    public void Save()
    {
        sinceSave = 0f;
        var data = new WaveSaveData();
        foreach (WaveSession session in sessions.Values)
        {
            var entry = new WaveSessionSaveData
            {
                islandId = session.IslandId,
                number = session.Number,
                phase = session.Phase,
                elapsedSeconds = session.ElapsedSeconds,
                requestWindowSeconds = session.RequestWindowSeconds
            };
            entry.spedUpBuildings.AddRange(session.SpedUp);
            foreach (KeyValuePair<string, float> boost in session.Boosts)
            {
                entry.boostBuildings.Add(boost.Key);
                entry.boostSeconds.Add(boost.Value);
            }
            data.sessions.Add(entry);
        }
        foreach (KeyValuePair<string, int> count in waveCounts)
        {
            data.countIslands.Add(count.Key);
            data.counts.Add(count.Value);
        }

        try
        {
            // Written next to the old file first, so an interrupted save keeps a whole one.
            File.WriteAllText(TempSavePath, JsonUtility.ToJson(data));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(TempSavePath, SavePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[WaveManager] Failed to save waves: {e.Message}");
        }
    }

    [ContextMenu("Debug/Delete Save File")]
    private void DebugDeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempSavePath)) File.Delete(TempSavePath);
        Debug.Log($"[WaveManager] Deleted save at {SavePath}.");
    }
}
