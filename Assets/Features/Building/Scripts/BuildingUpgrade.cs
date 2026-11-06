using UnityEngine;

// Buying the next level of a building. BuildingManager swaps the art and saves it.
public class BuildingUpgrade : MonoBehaviour
{
    public static BuildingUpgrade Instance { get; private set; }

    //Smoke over the footprint when a level is bought
    [SerializeField] private RemovalEffect upgradeEffect;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool CanUpgrade(PlacedBuilding building)
    {
        return building.Definition.HasLevel(building.Level + 1);
    }

    public bool TryUpgrade(PlacedBuilding building)
    {
        if (!CanUpgrade(building)) return false;

        BuildingLevel next = building.Definition.levels[building.Level + 1];
        if (!ResourceManager.Instance.TrySpend(next.upgradeCost)) return false;

        BuildingManager.Instance.SetLevel(building, building.Level + 1);
        PlayEffect(building);
        return true;
    }

    private void PlayEffect(PlacedBuilding building)
    {
        if (upgradeEffect == null) return;

        Vector2Int size = building.Definition.size;
        Vector3 center = BuildingManager.Instance.Grid.FootprintCenter(building.Origin, size);
        Instantiate(upgradeEffect).Play(center, size, null);
    }
}
