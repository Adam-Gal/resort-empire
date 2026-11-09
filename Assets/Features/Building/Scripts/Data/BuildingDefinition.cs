using System;
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

    //Seconds from paying for this level until it is built, 0 is instant. Unused on the first one
    [Min(0f)] public float upgradeDuration;

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

    //Upgrade time one point of energy skips on this building. 0 means its upgrades can't be skipped
    [Min(0f)] public float minutesPerEnergy = 5f;

    //Off for buildings nobody works in
    public bool isEmploying = true;

    //Added to the 1 starting worker with each level
    [Min(0)] public int EmployeesPerLevel = 1;

    //Starting price of one employee 
    public ResourceCost[] hireCost;

    //Hire price is multiplied by this with each level
    [Min(1f)] public float hireCostMultiplier = 1.2f;

    public ResourceCost[] HireCostAt(int level)
    {
        if (hireCost == null) return new ResourceCost[0];

        double factor = Math.Pow(hireCostMultiplier, Mathf.Max(0, level));
        var cost = new ResourceCost[hireCost.Length];
        for (int i = 0; i < cost.Length; i++)
            cost[i] = new ResourceCost(hireCost[i].type, (long)Math.Round(hireCost[i].amount * factor));
        return cost;
    }

    public bool IsEmploying => isEmploying && !IsDecoration;

    public int MaxEmployeesAt(int level) => IsEmploying ? 1 + EmployeesPerLevel * Mathf.Max(0, level) : 0;

    public Sprite Picture => icon != null ? icon : prefab.GetComponentInChildren<SpriteRenderer>().sprite;

    public bool HasLevel(int level) => levels != null && level >= 0 && level < levels.Length;

    public GameObject PrefabAt(int level) => HasLevel(level) ? levels[level].prefab : prefab;

    public float VisualHeightAt(int level) => HasLevel(level) ? levels[level].visualHeight : visualHeight;

    // How long reaching this level takes, for the upgrade popup.
    public TimeSpan UpgradeDurationTo(int level) => TimeSpan.FromSeconds(HasLevel(level) ? levels[level].upgradeDuration : 0f);

    public Sprite PictureAt(int level) => PrefabAt(level).GetComponentInChildren<SpriteRenderer>().sprite;

    public bool CanStandOn(TileCategory category)
    {
        return category != null && System.Array.IndexOf(allowedTiles, category) >= 0;
    }
}