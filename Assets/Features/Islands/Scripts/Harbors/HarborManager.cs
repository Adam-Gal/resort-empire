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

        //Where the middle of the art goes, a negative x scale mirrors the harbor.
        //Only places the harbor, boats stop at the BoatAnchor inside the harbor level's art
        public Transform anchor;

        //Out at sea, where boats for this island appear. Empty: no boats come here
        public Transform boatStart;

        //Points boats pass on their way in, in order, to steer around rocks. They stop at the level's BoatAnchor
        public Transform[] boatRoute;

        //Points boats pass on their way out from the BoatAnchor, in order. They vanish at the last one.
        //Empty: they leave the way they came
        public Transform[] boatLeaveRoute;
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

    // Harbors standing now, one per open site.
    public Dictionary<string, Harbor>.ValueCollection Harbors => harbors.Values;

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

    // The harbor of the island, where its guests arrive and leave. Null while the island has none.
    public Harbor GetHarbor(string islandId)
    {
        foreach (Site site in sites)
        {
            string siteIsland = string.IsNullOrEmpty(site.islandId) ? IslandProgress.StartingIslandId : site.islandId;
            if (siteIsland == islandId && harbors.TryGetValue(site.id, out Harbor harbor)) return harbor;
        }
        return null;
    }

    // The points boats to the island sail through: in from their start at sea to the BoatAnchor of the harbor's level,
    // and out from there. The way out holds only the dock when the site has no leave route.
    // False while the island has no harbor, its site has no boat start or the harbor's level has no BoatAnchor.
    public bool TryGetBoatPaths(string islandId, List<Vector3> wayIn, List<Vector3> wayOut, out Harbor harbor)
    {
        foreach (Site site in sites)
        {
            string siteIsland = string.IsNullOrEmpty(site.islandId) ? IslandProgress.StartingIslandId : site.islandId;
            if (siteIsland != islandId || !harbors.TryGetValue(site.id, out harbor) || harbor == null) continue;
            if (site.boatStart == null || harbor.BoatAnchor == null) break;

            Vector3 dock = harbor.BoatAnchor.position;
            AddWayIn(site, wayIn);
            wayIn.Add(dock);
            wayOut.Add(dock);
            AddPoints(site.boatLeaveRoute, wayOut);
            return true;
        }
        harbor = null;
        return false;
    }

    private static void AddWayIn(Site site, List<Vector3> path)
    {
        path.Add(site.boatStart.position);
        AddPoints(site.boatRoute, path);
    }

    private static void AddPoints(Transform[] points, List<Vector3> path)
    {
        if (points == null) return;
        foreach (Transform point in points)
            if (point != null) path.Add(point.position);
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

    // Each site's boat routes in the scene view: the way in blue, the way out green, orange where boats stop and
    // the way they point there. Before play the stop comes from the BoatAnchor in the first level's art, placed the way
    // Harbor.SetArt places it on the site anchor. In play it is the BoatAnchor of the harbor standing there.
    private void OnDrawGizmos()
    {
        if (sites == null) return;

        var path = new List<Vector3>();
        var wayInColor = new Color(.2f, .8f, 1f, .9f);
        var wayOutColor = new Color(.3f, 1f, .4f, .9f);
        foreach (Site site in sites)
        {
            if (site == null || site.boatStart == null) continue;
            bool stops = TryGetBoatStop(site, out Vector3 stop);

            Gizmos.color = wayInColor;
            Gizmos.DrawWireSphere(site.boatStart.position, .25f);
            path.Clear();
            AddWayIn(site, path);
            if (stops) path.Add(stop);
            DrawRoute(path, site.boatRoute);

            if (stops && site.boatLeaveRoute != null && site.boatLeaveRoute.Length > 0)
            {
                Gizmos.color = wayOutColor;
                path.Clear();
                path.Add(stop);
                AddPoints(site.boatLeaveRoute, path);
                DrawRoute(path, site.boatLeaveRoute);
            }

            if (!stops) continue;
            path.Clear();
            AddWayIn(site, path);
            path.Add(stop);
            BoatRoute wayIn = BoatRoute.Through(path, .1f);
            if (wayIn == null) continue;
            wayIn.PointAt(wayIn.Length, out Vector3 ahead);
            Gizmos.color = new Color(1f, .6f, .1f);
            Gizmos.DrawWireSphere(stop, .2f);
            Gizmos.DrawLine(stop, stop + ahead * .6f);
        }
    }

    private static void DrawRoute(List<Vector3> path, Transform[] points)
    {
        if (points != null)
            foreach (Transform point in points)
                if (point != null) Gizmos.DrawWireSphere(point.position, .12f);

        BoatRoute route = BoatRoute.Through(path, .1f);
        if (route == null) return;
        for (int i = 1; i < route.points.Count; i++) Gizmos.DrawLine(route.points[i - 1], route.points[i]);
    }

    // Where boats stop at the site: the BoatAnchor in the harbor level's art, not the site anchor that places the harbor.
    private bool TryGetBoatStop(Site site, out Vector3 stop)
    {
        stop = Vector3.zero;
        if (harbors.TryGetValue(site.id, out Harbor harbor) && harbor != null && harbor.BoatAnchor != null)
        {
            stop = harbor.BoatAnchor.position;
            return true;
        }
        if (site.anchor == null || definition == null || definition.levels == null || definition.levels.Length == 0) return false;

        // Harbor.SetArt puts the art's sprite on the site anchor, mirrored by it. The BoatAnchor keeps its offset
        // from the sprite, so it lands that far from the site anchor.
        GameObject prefab = definition.PrefabAt(0);
        if (prefab == null) return false;
        Transform boatAnchor = Harbor.FindBoatAnchor(prefab.transform);
        SpriteRenderer sprite = prefab.GetComponentInChildren<SpriteRenderer>(true);
        if (boatAnchor == null || sprite == null) return false;

        stop = site.anchor.position + site.anchor.TransformVector(boatAnchor.position - sprite.transform.position);
        return true;
    }

    [ContextMenu("Debug/Delete Save File")]
    private void DebugDeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempSavePath)) File.Delete(TempSavePath);
        Debug.Log($"[HarborManager] Deleted save at {SavePath}.");
    }
}