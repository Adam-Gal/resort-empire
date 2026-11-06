using UnityEngine;

public enum BuildingCategory
{
    Rooms,
    Food,
    Drinks,
    Activities,
    Decorations
}

[System.Serializable]
public class BuildingLevel
{
    public GameObject prefab;

    //Guests served at once, 0 hides it in the upgrade popup
    public int capacity;

    //Price of reaching this level, empty on the first one
    public ResourceCost[] upgradeCost;

    //Same as BuildingDefinition.visualHeight, higher levels have taller art
    public float visualHeight;
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

    //Off for landmarks the island starts with, they can't be bought
    public bool showInShop = true;

    //Off for landmarks that can only be moved, never cleared or demolished
    public bool canRemove = true;

    // Decorations are cleared like obstacles, other buildings are demolished for a refund.
    public bool IsDecoration => category == BuildingCategory.Decorations;

    //From the footprint centre to the sprite pivot, so the art sits on its cells
    public Vector2 spriteOffset;

    //Fits art made for another size onto its footprint
    public float spriteScale = 1f;

    //Height of the visible art from its front corner, used to pick the roof on a tap
    public float visualHeight;

    //First one is what gets built, leave empty if it can't be upgraded
    public BuildingLevel[] levels;

    public Sprite Picture => icon != null ? icon : prefab.GetComponentInChildren<SpriteRenderer>().sprite;

    public bool HasLevel(int level) => levels != null && level >= 0 && level < levels.Length;

    public GameObject PrefabAt(int level) => HasLevel(level) ? levels[level].prefab : prefab;

    public float VisualHeightAt(int level) => HasLevel(level) ? levels[level].visualHeight : visualHeight;

    public Sprite PictureAt(int level) => PrefabAt(level).GetComponentInChildren<SpriteRenderer>().sprite;

    public bool CanStandOn(TileCategory category)
    {
        return category != null && System.Array.IndexOf(allowedTiles, category) >= 0;
    }
}
