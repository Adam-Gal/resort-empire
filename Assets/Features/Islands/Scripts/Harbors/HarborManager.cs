using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Harbors stand on the shore, off the build grid, so they are not placed buildings.
// Each site gets a harbor once its island is open, and it is upgraded level by level
// like a building, using the levels of its BuildingDefinition.
[DefaultExecutionOrder(-70)]
public class HarborManager : MonoBehaviour
{
    [Serializable]
    public class Site
    {
        //Used in the save file, do not rename after release
        public string id;

        //Empty for the starting island, which is always open
        public string islandId;

        //Where the middle of the art goes, a negative x scale mirrors the harbor
        public Transform anchor;
    }

    public static HarborManager Instance { get; private set; }

    [SerializeField] private BuildingDefinition definition;
    [SerializeField] private Site[] sites;

    //Smoke over the harbor when a level is bought
    [SerializeField] private RemovalEffect upgradeEffect;
    [SerializeField] private string saveFileName = "harbors_save.json";

    // A harbor was built or upgraded.
    public event Action Changed;

    private readonly Dictionary<string, Harbor> harbors = new Dictionary<string, Harbor>();
    private HarborSaveData saveData;

    public BuildingDefinition Definition => definition;

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private string TempSavePath => SavePath + ".tmp";

    private void Awake()
    {
        Instance = this;
        saveData = ReadSaveFile() ?? new HarborSaveData();
    }

    private void Start()
    {
        if (IslandProgress.Instance != null) IslandProgress.Instance.Changed += SpawnOpenSites;
        SpawnOpenSites();
    }

    private void OnDestroy()
    {
        if (IslandProgress.Instance != null) IslandProgress.Instance.Changed -= SpawnOpenSites;
        if (Instance == this) Instance = null;
    }

    // A harbor whose art is under the world point.
    public Harbor Pick(Vector3 world)
    {
        foreach (Harbor harbor in harbors.Values)
        {
            if (harbor != null && harbor.Contains(world)) return harbor;
        }
        return null;
    }

    public bool CanUpgrade(Harbor harbor) => definition.HasLevel(harbor.Level + 1);

    public bool TryUpgrade(Harbor harbor)
    {
        if (!CanUpgrade(harbor)) return false;

        BuildingLevel next = definition.levels[harbor.Level + 1];
        if (!ResourceManager.Instance.TrySpend(next.upgradeCost)) return false;

        SetLevel(harbor, harbor.Level + 1);
        if (upgradeEffect != null) Instantiate(upgradeEffect).Play(harbor.Center, new Vector2Int(3, 2), null);
        Save();
        Changed?.Invoke();
        return true;
    }

    private void SpawnOpenSites()
    {
        bool any = false;
        foreach (Site site in sites)
        {
            if (harbors.ContainsKey(site.id) || site.anchor == null) continue;
            bool open = string.IsNullOrEmpty(site.islandId) ||
                        (IslandProgress.Instance != null && IslandProgress.Instance.IsUnlocked(site.islandId));
            if (!open) continue;

            var harbor = new GameObject("Harbor_" + site.id).AddComponent<Harbor>();
            harbor.transform.SetParent(site.anchor, false);
            harbor.Initialize(site.id, site.anchor);
            harbors[site.id] = harbor;
            SetLevel(harbor, Mathf.Clamp(GetSavedLevel(site.id), 0, definition.levels.Length - 1));
            any = true;
        }
        if (any) Changed?.Invoke();
    }

    private void SetLevel(Harbor harbor, int level)
    {
        harbor.SetArt(definition.PrefabAt(level), level);

        HarborLevelSaveData entry = saveData.harbors.Find(h => h.id == harbor.Id);
        if (entry == null) saveData.harbors.Add(entry = new HarborLevelSaveData { id = harbor.Id });
        entry.level = level;
    }

    private int GetSavedLevel(string id)
    {
        HarborLevelSaveData entry = saveData.harbors.Find(h => h.id == id);
        return entry != null ? entry.level : 0;
    }

    private HarborSaveData ReadSaveFile()
    {
        string path = File.Exists(SavePath) ? SavePath : (File.Exists(TempSavePath) ? TempSavePath : null);
        if (path == null) return null;

        try
        {
            return JsonUtility.FromJson<HarborSaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[HarborManager] Save file is corrupt, starting fresh: {e.Message}");
            return null;
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(TempSavePath, JsonUtility.ToJson(saveData));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(TempSavePath, SavePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[HarborManager] Failed to save harbors: {e.Message}");
        }
    }

    [ContextMenu("Debug/Delete Save File")]
    private void DebugDeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempSavePath)) File.Delete(TempSavePath);
        Debug.Log($"[HarborManager] Deleted save at {SavePath}.");
    }
}
