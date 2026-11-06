using System;
using ResortEmpire.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// Placement: dragging the ghost moves it cell by cell, dragging anywhere else still
// moves the camera. Outside placement a tap selects whatever is on the tapped cell.
public class BuildPlacementController : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private IslandCameraController cameraController;
    [SerializeField] private Tilemap overlay;
    [SerializeField] private TileBase overlayTile;
    [SerializeField] private Tilemap[] overlaySources;

    //Filled cells under the ghost or the selected thing
    [SerializeField] private Tilemap highlight;
    [SerializeField] private TileBase highlightTile;
    [SerializeField] private BuildSelectionMarker marker;
    [SerializeField] private Color validColor = new Color(.35f, 1f, .5f, .55f);
    [SerializeField] private Color invalidColor = new Color(1f, .3f, .3f, .55f);
    [SerializeField] private Color selectedColor = new Color(1f, .89f, .48f, .3f);
    [SerializeField] private float tapMaxDistance = 12f;

    // Tapped cell and what stands there, outside placement mode.
    public event Action<Vector2Int, UnityEngine.Object, Vector3> Tapped;
    public event Action<bool> CanConfirmChanged;

    private BuildingDefinition definition;
    private PlacedBuilding moving;
    private GameObject ghost;
    private SpriteRenderer[] ghostRenderers;
    private Vector2Int origin;
    private bool canConfirm;
    private Vector2 pressPosition;
    private bool pressStartedOnWorld;
    private bool draggingGhost;
    private Vector2Int grabOffset;

    public bool IsPlacing => definition != null;
    public BuildingDefinition Definition => definition;

    // Footprint around a cell, the same one the ghost uses.
    public static Vector2Int OriginAround(BuildingDefinition building, Vector2Int cell)
    {
        return cell - (building.size - Vector2Int.one) / 2;
    }

    // world is where the ghost starts, otherwise the middle of the screen.
    public void BeginPlace(BuildingDefinition building, Vector3? world = null)
    {
        Cancel();
        definition = building;
        BuildGrid grid = BuildingManager.Instance.Grid;
        ShowGhost(OriginAround(building, grid.WorldToCell(world ?? worldCamera.transform.position)));
    }

    public void BeginMove(PlacedBuilding building)
    {
        Cancel();
        definition = building.Definition;
        moving = building;
        moving.gameObject.SetActive(false);
        ShowGhost(moving.Origin);
    }

    // popTarget is the art to pop, an obstacle pops its tile on selectionOrigin instead.
    public void ShowSelection(Vector2Int selectionOrigin, Vector2Int size, Transform popTarget = null, bool isObstacle = false)
    {
        highlight.ClearAllTiles();
        highlight.color = selectedColor;
        foreach (Vector2Int cell in BuildGrid.Footprint(selectionOrigin, size))
            highlight.SetTile((Vector3Int)cell, highlightTile);

        marker.Show(BuildingManager.Instance.Grid, selectionOrigin, size, popTarget, isObstacle);
    }

    public void ClearSelection()
    {
        highlight.ClearAllTiles();
        marker.Hide();
    }

    public void Confirm()
    {
        if (!IsPlacing || !canConfirm) return;

        BuildingManager manager = BuildingManager.Instance;
        bool done = moving != null ? manager.TryMove(moving, origin) : manager.TryPlace(definition, origin);
        if (done) Cancel();
    }

    public void Cancel()
    {
        EndGhostDrag();
        if (moving != null) moving.gameObject.SetActive(true);
        moving = null;
        definition = null;
        if (ghost != null) Destroy(ghost);
        overlay.ClearAllTiles();
        ClearSelection();
    }

    private void ShowGhost(Vector2Int startOrigin)
    {
        ghost = Instantiate(moving != null ? definition.PrefabAt(moving.Level) : definition.prefab);
        ghost.transform.localScale *= definition.spriteScale;
        ghostRenderers = ghost.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer spriteRenderer in ghostRenderers)
            spriteRenderer.sortingOrder = short.MaxValue;
        FillOverlay();
        origin = new Vector2Int(int.MinValue, 0);
        SetGhostOrigin(startOrigin);
    }

    private void FillOverlay()
    {
        BuildGrid grid = BuildingManager.Instance.Grid;
        foreach (Tilemap source in overlaySources)
        {
            foreach (Vector3Int position in source.cellBounds.allPositionsWithin)
            {
                var cell = (Vector2Int)position;
                UnityEngine.Object occupant = grid.GetOccupant(cell);
                if ((occupant == null || occupant == moving) && definition.CanStandOn(grid.GetCategory(cell)))
                    overlay.SetTile(position, overlayTile);
            }
        }
    }

    private void Update()
    {
        if (IsPlacing)
            UpdateGhostDrag();
        else
            UpdateTap();
    }

    private void UpdateGhostDrag()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        BuildingManager manager = BuildingManager.Instance;
        Vector3 world = worldCamera.ScreenToWorldPoint(pointer.position.ReadValue());

        if (pointer.press.wasPressedThisFrame)
        {
            int level = moving != null ? moving.Level : 0;
            if (EventSystem.current.IsPointerOverGameObject() || !manager.IsOverBuilding(world, definition, origin, level)) return;

            draggingGhost = true;
            cameraController.InputLocked = true;
            grabOffset = origin - manager.Grid.WorldToCell(world);
        }
        else if (draggingGhost && pointer.press.isPressed)
        {
            SetGhostOrigin(manager.Grid.WorldToCell(world) + grabOffset);
        }
        else
        {
            EndGhostDrag();
        }
    }

    private void EndGhostDrag()
    {
        draggingGhost = false;
        cameraController.InputLocked = false;
    }

    private void SetGhostOrigin(Vector2Int next)
    {
        if (next == origin) return;

        BuildingManager manager = BuildingManager.Instance;
        BuildGrid grid = manager.Grid;
        origin = next;
        ghost.transform.position = manager.SpritePosition(definition, origin);
        canConfirm = grid.CanPlace(definition, origin, moving);

        // The ghost hides the cells under it, so it carries the color too.
        Color color = canConfirm ? validColor : invalidColor;
        foreach (SpriteRenderer spriteRenderer in ghostRenderers)
            spriteRenderer.color = color;

        highlight.ClearAllTiles();
        highlight.color = color;
        foreach (Vector2Int cell in BuildGrid.Footprint(origin, definition.size))
            highlight.SetTile((Vector3Int)cell, highlightTile);

        CanConfirmChanged?.Invoke(canConfirm);
    }

    private void UpdateTap()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 position = pointer.position.ReadValue();
        if (pointer.press.wasPressedThisFrame)
        {
            pressPosition = position;
            pressStartedOnWorld = !EventSystem.current.IsPointerOverGameObject();
        }
        else if (pointer.press.wasReleasedThisFrame && pressStartedOnWorld &&
                 Vector2.Distance(position, pressPosition) <= tapMaxDistance)
        {
            Vector3 world = worldCamera.ScreenToWorldPoint(position);
            BuildingManager manager = BuildingManager.Instance;
            Tapped?.Invoke(manager.Grid.WorldToCell(world), manager.PickOccupant(world), world);
        }
    }
}
