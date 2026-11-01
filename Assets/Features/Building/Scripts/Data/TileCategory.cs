using System;
using UnityEngine;
using UnityEngine.Tilemaps;

// Groups terrain tiles so buildings can say where they may stand.
// A category with a clear cost is an obstacle the player can pay to remove.
[CreateAssetMenu(menuName = "Resort Empire/Building/Tile Category", fileName = "TileCategory")]
public class TileCategory : ScriptableObject
{
    // An obstacle tile covering more than one cell. The tile sits on the front cell.
    [Serializable]
    public struct ObstacleSize
    {
        public TileBase tile;
        public Vector2Int size;
    }

    public string displayName;
    public TileBase[] tiles;

    //Price of one cell, a bigger obstacle costs this per covered cell
    public ResourceCost[] clearCost;
    public ObstacleSize[] obstacleSizes;

    public bool IsClearable => clearCost != null && clearCost.Length > 0;

    public Vector2Int GetSize(TileBase tile)
    {
        foreach (ObstacleSize entry in obstacleSizes)
        {
            if (entry.tile == tile) return entry.size;
        }
        return Vector2Int.one;
    }
}
