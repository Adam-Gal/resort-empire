using UnityEngine;

// A broken building already on the island. The tutorial restores it through
// BuildingManager.TryRestore, which replaces it with the real building.
public class RuinedBuilding : MonoBehaviour
{
    [SerializeField] private BuildingDefinition restoredBuilding;
    [SerializeField] private Vector2Int origin;
    [SerializeField] private ResourceCost[] restoreCost;

    public BuildingDefinition RestoredBuilding => restoredBuilding;
    public Vector2Int Origin => origin;
    public ResourceCost[] RestoreCost => restoreCost;
}
