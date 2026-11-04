using UnityEngine;

public enum BuildingCategory
{
    Rooms,
    Food,
    Drinks,
    Activities,
    Decorations
}

// One buildable thing. Create a new asset to add a building to the game.
[CreateAssetMenu(menuName = "Resort Empire/Building/Building Definition", fileName = "BuildingDefinition")]
public class BuildingDefinition : ScriptableObject
{
    //Used in the save file, do not rename after release
    public string id;
    public string displayName;
    public BuildingCategory category;
    public Sprite icon;
    public GameObject prefab;
    public Vector2Int size = Vector2Int.one;
    public ResourceCost[] cost;
    public TileCategory[] allowedTiles;

    //Part of the price returned on demolish
    [Range(0f, 1f)] public float refundRatio = .5f;

    // Decorations are cleared like obstacles, other buildings are demolished for a refund.
    public bool IsDecoration => category == BuildingCategory.Decorations;

    //From the footprint centre to the sprite pivot, so the art sits on its cells
    public Vector2 spriteOffset;

    //Fits art made for another size onto its footprint
    public float spriteScale = 1f;

    //Height of the visible art from its front corner, used to pick the roof on a tap
    public float visualHeight;

    public Sprite Picture => icon != null ? icon : prefab.GetComponentInChildren<SpriteRenderer>().sprite;

    public bool CanStandOn(TileCategory category)
    {
        return category != null && System.Array.IndexOf(allowedTiles, category) >= 0;
    }
}
