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
    [SerializeField] private string saveFileName = "buildings_save.json";

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

    public ResourceCost[] GetRefund(BuildingDefinition definition)
    {
        var refund = new ResourceCost[definition.cost.Length];
        for (int i = 0; i < refund.Length; i++)
        {
            ResourceCost cost = definition.cost[i];
            refund[i] = new ResourceCost(cost.type, (long)(cost.amount * definition.refundRatio));
        }
        return refund;
    }

    public void Demolish(PlacedBuilding building)
    {
        foreach (ResourceCost refund in GetRefund(building.Definition))
        {
            if (refund.amount > 0) ResourceManager.Instance.Add(refund.type, refund.amount);
        }

        grid.SetOccupant(building.Origin, building.Definition.size, null);
        placed.Remove(building);
        Destroy(building.gameObject);
        WriteBuildings();
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
        grid.RemoveObstacle(origin);
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

    private PlacedBuilding Spawn(BuildingDefinition definition, Vector2Int origin)
    {
        var root = new GameObject(definition.displayName);
        root.transform.SetParent(buildingsRoot, false);

        // The sorting group sorts by the root, so the root sits on the front cell of the footprint.
        SortingGroup sorting = root.AddComponent<SortingGroup>();
        sorting.sortingOrder = sortingOrder;

        PlacedBuilding building = root.AddComponent<PlacedBuilding>();
        building.Initialize(definition, origin);
        Instantiate(definition.prefab, root.transform).transform.localScale *= definition.spriteScale;

        PositionBuilding(building);
        grid.SetOccupant(origin, definition.size, building);
        placed.Add(building);
        return building;
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
            if (IsOnRoof(world, building.Definition, building.Origin)) return building;
        }
        foreach (RuinedBuilding ruin in ruins)
        {
            if (ruin != null && IsOnRoof(world, ruin.RestoredBuilding, ruin.Origin)) return ruin;
        }
        return null;
    }

    // On the footprint or on the art above it.
    public bool IsOverBuilding(Vector3 world, BuildingDefinition definition, Vector2Int origin)
    {
        Vector2Int cell = grid.WorldToCell(world) - origin;
        bool onFootprint = cell.x >= 0 && cell.y >= 0 && cell.x < definition.size.x && cell.y < definition.size.y;
        return onFootprint || IsOnRoof(world, definition, origin);
    }

    private bool IsOnRoof(Vector3 world, BuildingDefinition definition, Vector2Int origin)
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
        float rise = Mathf.Max(0f, definition.visualHeight - (size.x + size.y) * .25f);
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
                Spawn(definition, entry.origin);
            else
                Debug.LogWarning($"[BuildingManager] Save has unknown building '{entry.id}'.");
        }

        Changed?.Invoke();
    }

    private void WriteBuildings()
    {
        saveData.buildings.Clear();
        foreach (PlacedBuilding building in placed)
            saveData.buildings.Add(new PlacedBuildingSaveData { id = building.Definition.id, origin = building.Origin });
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
