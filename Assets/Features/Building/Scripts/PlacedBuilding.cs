using UnityEngine;

// A building standing on the island. BuildingManager creates it.
public class PlacedBuilding : MonoBehaviour
{
    public BuildingDefinition Definition { get; private set; }
    public Vector2Int Origin { get; private set; }

    public void Initialize(BuildingDefinition definition, Vector2Int origin)
    {
        Definition = definition;
        Origin = origin;
    }

    public void SetOrigin(Vector2Int origin) => Origin = origin;
}
