using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-90)]
public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    [SerializeField] private BuildGrid grid;
    [SerializeField] private Transform buildingsRoot;
    [SerializeField] private BuildingDefinition[] buildings;
    [SerializeField] private RuinedBuilding[] ruins;

    //Decorations the island starts with, they behave like bought ones
    [SerializeField] private PlacedBuildingSaveData[] startingBuildings;
    [SerializeField] private string saveFileName = "buildings_save.json";

    //Smoke when a building is demolished or jungle is cleared
    [SerializeField] private RemovalEffect removalEffect;

    //Sorts together with the clearable tiles, which use the same order
    [SerializeField] private int sortingOrder = 200;

    // Anything on the island changed, UI should refresh.
    public event Action Changed;

    private readonly List<PlacedBuilding> placed = new List<PlacedBuilding>();
    private BuildingSaveData saveData;
    private bool isChanged;

    public IReadOnlyList<BuildingDefinition> Buildings => buildings;
    public BuildGrid Grid => grid;

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private string TempSavePath => SavePath + ".tmp";

    private void Awake()
    {
        Instance = this;
    }

    // Start, because BuildGrid fills its tile lookup in Awake.
    private void Start()
    {
        saveData = ReadSaveFile() ?? new BuildingSaveData();
        ApplySave();
    }

    private void LateUpdate()
    {
        if (isChanged) Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && saveData != null) Save();
    }

    private void OnApplicationQuit()
    {
        if (saveData != null) Save();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool TryPlace(BuildingDefinition definition, Vector2Int origin)
    {
        if (!grid.CanPlace(definition, origin)) return false;
        if (!ResourceManager.Instance.TrySpend(definition.cost)) return false;

        Spawn(definition, origin);
        WriteBuildings();
        return true;
    }

    public bool TryMove(PlacedBuilding building, Vector2Int origin)
    {
        if (!grid.CanPlace(building.Definition, origin, building)) return false;

        Vector2Int size = building.Definition.size;
        grid.SetOccupant(building.Origin, size, null);
        grid.SetOccupant(origin, size, building);
        building.SetOrigin(origin);
        PositionBuilding(building);
        WriteBuildings();
        return true;
    }

    public void SetLevel(PlacedBuilding building, int level)
    {
        building.SetLevel(level);

        // Detached first, Destroy waits for the end of the frame and the art must stay the first child.
        Transform oldVisual = building.transform.GetChild(0);
        oldVisual.SetParent(null);
        Destroy(oldVisual.gameObject);

        AddVisual(building);
        PositionBuilding(building);
        WriteBuildings();
    }

    // Part of the price and of every upgrade bought.
    public ResourceCost[] GetRefund(PlacedBuilding building)
    {
        BuildingDefinition definition = building.Definition;
        var totals = new Dictionary<ResourceType, long>();
        AddToTotals(totals, definition.cost);
        for (int level = 1; level <= building.Level && definition.HasLevel(level); level++)
            AddToTotals(totals, definition.levels[level].upgradeCost);

        var refund = new List<ResourceCost>();
        foreach (KeyValuePair<ResourceType, long> total in totals)
            refund.Add(new ResourceCost(total.Key, (long)(total.Value * definition.refundRatio)));
        return refund.ToArray();
    }

    private static void AddToTotals(Dictionary<ResourceType, long> totals, ResourceCost[] costs)
    {
        if (costs == null) return;

        foreach (ResourceCost cost in costs)
        {
            totals.TryGetValue(cost.type, out long current);
            totals[cost.type] = current + cost.amount;
        }
    }

    public void Demolish(PlacedBuilding building)
    {
        foreach (ResourceCost refund in GetRefund(building))
        {
            if (refund.amount > 0) ResourceManager.Instance.Add(refund.type, refund.amount);
        }
        Remove(building);
    }

    // Takes the building away without paying anything back.
    public void Remove(PlacedBuilding building)
    {
        Vector2Int origin = building.Origin;
        Vector2Int size = building.Definition.size;
        GameObject visual = building.gameObject;
        grid.SetOccupant(origin, size, null);
        placed.Remove(building);

        // The effect sinks the art and destroys it, nothing can pick it any more.
        Destroy(building);
        PlayRemoval(origin, size, visual);
        WriteBuildings();
    }

    private void PlayRemoval(Vector2Int origin, Vector2Int size, GameObject visual)
    {
        if (removalEffect == null)
        {
            Destroy(visual);
            return;
        }
        Instantiate(removalEffect).Play(grid.FootprintCenter(origin, size), size, visual);
    }

    public ResourceCost[] GetClearCost(Vector2Int cell)
    {
        if (!grid.TryGetObstacle(cell, out Vector2Int origin)) return null;
        TileCategory category = grid.GetCategory(origin);
        if (category == null || !category.IsClearable) return null;

        Vector2Int size = grid.GetObstacleSize(origin);
        var cost = new ResourceCost[category.clearCost.Length];
        for (int i = 0; i < cost.Length; i++)
        {
            ResourceCost perCell = category.clearCost[i];
            cost[i] = new ResourceCost(perCell.type, perCell.amount * size.x * size.y);
        }
        return cost;
    }

    public bool TryClear(Vector2Int cell)
    {
        ResourceCost[] cost = GetClearCost(cell);
        if (cost == null || !ResourceManager.Instance.TrySpend(cost)) return false;

        grid.TryGetObstacle(cell, out Vector2Int origin);
        Vector2Int size = grid.GetObstacleSize(origin);
        GameObject visual = grid.CopyObstacleVisual(origin);
        grid.RemoveObstacle(origin);
        PlayRemoval(origin, size, visual);
        saveData.clearedCells.Add(origin);
        MarkChanged();
        return true;
    }

    public bool TryRestore(RuinedBuilding ruin)
    {
        if (ruin == null || !ResourceManager.Instance.TrySpend(ruin.RestoreCost)) return false;

        grid.SetOccupant(ruin.Origin, ruin.RestoredBuilding.size, null);
        Spawn(ruin.RestoredBuilding, ruin.Origin);
        saveData.restoredRuins.Add(ruin.RestoredBuilding.id);
        Destroy(ruin.gameObject);
        WriteBuildings();
        return true;
    }

    private PlacedBuilding Spawn(BuildingDefinition definition, Vector2Int origin, int level = 0)
    {
        var root = new GameObject(definition.displayName);
        root.transform.SetParent(buildingsRoot, false);

        // The sorting group sorts by the root, so the root sits on the front cell of the footprint.
        SortingGroup sorting = root.AddComponent<SortingGroup>();
        sorting.sortingOrder = sortingOrder;

        PlacedBuilding building = root.AddComponent<PlacedBuilding>();
        building.Initialize(definition, origin, level);
        AddVisual(building);

        PositionBuilding(building);
        grid.SetOccupant(origin, definition.size, building);
        placed.Add(building);
        return building;
    }

    private static void AddVisual(PlacedBuilding building)
    {
        BuildingDefinition definition = building.Definition;
        Instantiate(definition.PrefabAt(building.Level), building.transform).transform.localScale *= definition.spriteScale;
    }

    private void PositionBuilding(PlacedBuilding building)
    {
        BuildingDefinition definition = building.Definition;
        building.transform.position = grid.CellToWorld(building.Origin);
        building.transform.GetChild(0).position = SpritePosition(definition, building.Origin);
    }

    public Vector3 SpritePosition(BuildingDefinition definition, Vector2Int origin)
    {
        return grid.FootprintCenter(origin, definition.size) + (Vector3)definition.spriteOffset;
    }

    // Art rises above the footprint, so a tap on a roof lands on cells outside it.
    public UnityEngine.Object PickOccupant(Vector3 world)
    {
        UnityEngine.Object occupant = grid.GetOccupant(grid.WorldToCell(world));
        if (occupant != null) return occupant;

        foreach (PlacedBuilding building in placed)
        {
            if (IsOnRoof(world, building.Definition, building.Origin, building.Level)) return building;
        }
        foreach (RuinedBuilding ruin in ruins)
        {
            if (ruin != null && IsOnRoof(world, ruin.RestoredBuilding, ruin.Origin, 0)) return ruin;
        }
        return null;
    }

    // On the footprint or on the art above it.
    public bool IsOverBuilding(Vector3 world, BuildingDefinition definition, Vector2Int origin, int level = 0)
    {
        Vector2Int cell = grid.WorldToCell(world) - origin;
        bool onFootprint = cell.x >= 0 && cell.y >= 0 && cell.x < definition.size.x && cell.y < definition.size.y;
        return onFootprint || IsOnRoof(world, definition, origin, level);
    }

    private bool IsOnRoof(Vector3 world, BuildingDefinition definition, Vector2Int origin, int level)
    {
        // The footprint outline lifted by how far the art rises above its back corner.
        Vector2Int size = definition.size;
        Vector3 center = grid.FootprintCenter(origin, size);
        Vector2 back = (Vector2)center + new Vector2((size.x - size.y) * .25f, (size.x + size.y) * .125f);
        float halfWidth = (size.x + size.y) * .25f;
        float offset = world.x - center.x;
        if (Mathf.Abs(offset) > halfWidth) return false;

        float edgeSlope = .5f;
        float backEdge = back.y - Mathf.Abs(world.x - back.x) * edgeSlope;
        float rise = Mathf.Max(0f, definition.VisualHeightAt(level) - (size.x + size.y) * .25f);
        return world.y >= center.y && world.y <= backEdge + rise;
    }

    private void ApplySave()
    {
        foreach (RuinedBuilding ruin in ruins)
        {
            if (saveData.restoredRuins.Contains(ruin.RestoredBuilding.id))
                Destroy(ruin.gameObject);
            else
                grid.SetOccupant(ruin.Origin, ruin.RestoredBuilding.size, ruin);
        }

        foreach (Vector2Int cell in saveData.clearedCells)
            grid.RemoveObstacle(cell);

        foreach (PlacedBuildingSaveData entry in saveData.buildings)
        {
            BuildingDefinition definition = Array.Find(buildings, b => b.id == entry.id);
            if (definition != null)
                Spawn(definition, entry.origin, entry.level);
            else
                Debug.LogWarning($"[BuildingManager] Save has unknown building '{entry.id}'.");
        }

        if (!saveData.startingBuildingsPlaced)
            PlaceStartingBuildings();

        Changed?.Invoke();
    }

    // Once per save, so moved or demolished decorations do not come back.
    private void PlaceStartingBuildings()
    {
        foreach (PlacedBuildingSaveData entry in startingBuildings)
        {
            BuildingDefinition definition = Array.Find(buildings, b => b.id == entry.id);
            if (definition == null || saveData.clearedCells.Contains(entry.origin)) continue;
            if (grid.CanPlace(definition, entry.origin)) Spawn(definition, entry.origin);
        }

        saveData.startingBuildingsPlaced = true;
        WriteBuildings();
    }

    private void WriteBuildings()
    {
        saveData.buildings.Clear();
        foreach (PlacedBuilding building in placed)
            saveData.buildings.Add(new PlacedBuildingSaveData { id = building.Definition.id, origin = building.Origin, level = building.Level });
        MarkChanged();
    }

    private void MarkChanged()
    {
        isChanged = true;
        Changed?.Invoke();
    }

    private BuildingSaveData ReadSaveFile()
    {
        string path = File.Exists(SavePath) ? SavePath : (File.Exists(TempSavePath) ? TempSavePath : null);
        if (path == null) return null;

        try
        {
            return JsonUtility.FromJson<BuildingSaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildingManager] Save file is corrupt, starting fresh: {e.Message}");
            return null;
        }
    }

    public void Save()
    {
        if (saveData == null) return;

        try
        {
            File.WriteAllText(TempSavePath, JsonUtility.ToJson(saveData));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(TempSavePath, SavePath);
            isChanged = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildingManager] Failed to save buildings: {e.Message}");
        }
    }

    [ContextMenu("Debug/Delete Save File")]
    private void DebugDeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempSavePath)) File.Delete(TempSavePath);
        saveData = null;
        Debug.Log($"[BuildingManager] Deleted save at {SavePath}. Restart play mode for a fresh island.");
    }
}
