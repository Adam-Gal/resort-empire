using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Answers what is on a cell: terrain category and which building stands there.
public class BuildGrid : MonoBehaviour
{
    [SerializeField] private Grid grid;

    //Top layer first, the first tile found decides the category
    [SerializeField] private Tilemap[] layers;
    [SerializeField] private TileCategory[] categories;
    [SerializeField] private Tilemap obstacleLayer;

    private readonly Dictionary<TileBase, TileCategory> categoryByTile = new Dictionary<TileBase, TileCategory>();
    private readonly Dictionary<Vector2Int, Object> occupants = new Dictionary<Vector2Int, Object>();

    //Every cell an obstacle covers, pointing at the cell its tile sits on
    private readonly Dictionary<Vector2Int, Vector2Int> obstacleOrigins = new Dictionary<Vector2Int, Vector2Int>();

    private void Awake()
    {
        foreach (TileCategory category in categories)
        {
            foreach (TileBase tile in category.tiles)
            {
                if (tile != null) categoryByTile[tile] = category;
            }
        }

        foreach (Vector3Int position in obstacleLayer.cellBounds.allPositionsWithin)
        {
            TileBase tile = obstacleLayer.GetTile(position);
            if (tile == null || !categoryByTile.TryGetValue(tile, out TileCategory category)) continue;

            var origin = (Vector2Int)position;
            foreach (Vector2Int cell in Footprint(origin, category.GetSize(tile)))
                obstacleOrigins[cell] = origin;
        }
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        world.z = 0f;
        return (Vector2Int)grid.WorldToCell(world);
    }

    public Vector3 CellToWorld(Vector2Int cell) => grid.GetCellCenterWorld((Vector3Int)cell);

    // Middle of a footprint, where the building sprite sits.
    public Vector3 FootprintCenter(Vector2Int origin, Vector2Int size)
    {
        return (CellToWorld(origin) + CellToWorld(origin + size - Vector2Int.one)) * .5f;
    }

    public bool TryGetObstacle(Vector2Int cell, out Vector2Int origin) => obstacleOrigins.TryGetValue(cell, out origin);

    public Vector2Int GetObstacleSize(Vector2Int origin)
    {
        TileBase tile = obstacleLayer.GetTile((Vector3Int)origin);
        return tile != null && categoryByTile.TryGetValue(tile, out TileCategory category)
            ? category.GetSize(tile)
            : Vector2Int.one;
    }

    // A sprite that looks like the obstacle tile, so it can be animated after the tile is gone.
    public GameObject CopyObstacleVisual(Vector2Int origin)
    {
        var position = (Vector3Int)origin;
        Sprite sprite = obstacleLayer.GetSprite(position);
        if (sprite == null) return null;

        var copy = new GameObject("ClearedObstacle");
        Matrix4x4 matrix = obstacleLayer.GetTransformMatrix(position);
        copy.transform.SetPositionAndRotation(obstacleLayer.GetCellCenterWorld(position) + (Vector3)matrix.GetColumn(3), matrix.rotation);
        copy.transform.localScale = matrix.lossyScale;

        var tileRenderer = obstacleLayer.GetComponent<TilemapRenderer>();
        SpriteRenderer spriteRenderer = copy.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = obstacleLayer.GetColor(position) * obstacleLayer.color;
        spriteRenderer.sharedMaterial = tileRenderer.sharedMaterial;
        spriteRenderer.sortingLayerID = tileRenderer.sortingLayerID;
        spriteRenderer.sortingOrder = tileRenderer.sortingOrder;
        return copy;
    }

    public void RemoveObstacle(Vector2Int origin)
    {
        foreach (Vector2Int cell in Footprint(origin, GetObstacleSize(origin)))
            obstacleOrigins.Remove(cell);
        obstacleLayer.SetTile((Vector3Int)origin, null);
    }

    public TileCategory GetCategory(Vector2Int cell)
    {
        if (obstacleOrigins.TryGetValue(cell, out Vector2Int origin))
        {
            categoryByTile.TryGetValue(obstacleLayer.GetTile((Vector3Int)origin), out TileCategory obstacle);
            return obstacle;
        }

        foreach (Tilemap layer in layers)
        {
            TileBase tile = layer.GetTile((Vector3Int)cell);
            if (tile == null) continue;
            categoryByTile.TryGetValue(tile, out TileCategory category);
            return category;
        }
        return null;
    }

    public Object GetOccupant(Vector2Int cell)
    {
        occupants.TryGetValue(cell, out Object occupant);
        return occupant;
    }

    public void SetOccupant(Vector2Int origin, Vector2Int size, Object occupant)
    {
        foreach (Vector2Int cell in Footprint(origin, size))
        {
            if (occupant != null) occupants[cell] = occupant;
            else occupants.Remove(cell);
        }
    }

    // ignore lets a building being moved overlap its own old cells.
    public bool CanPlace(BuildingDefinition definition, Vector2Int origin, Object ignore = null)
    {
        foreach (Vector2Int cell in Footprint(origin, definition.size))
        {
            Object occupant = GetOccupant(cell);
            if (occupant != null && occupant != ignore) return false;
            if (!definition.CanStandOn(GetCategory(cell))) return false;
        }
        return true;
    }

    public static IEnumerable<Vector2Int> Footprint(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                yield return origin + new Vector2Int(x, y);
            }
        }
    }
}
