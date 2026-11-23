using System.Collections.Generic;
using UnityEngine;

// A round upgrade icon floats over every building whose upgrade is still running,
// bobbing gently so it reads as work in progress. Gone once the new level stands.
public class UpgradeMarkers : MonoBehaviour
{
    //World space marker, copied for each upgrading building
    [SerializeField] private Transform markerPrefab;

    //World units between the top of the building's art and the bottom of the marker
    [SerializeField, Min(0f)] private float heightAboveArt = .25f;

    [SerializeField, Min(0f)] private float bobHeight = .12f;
    [SerializeField, Min(0f)] private float bobSpeed = 2.5f;

    //Grows when the camera zooms out past this lens size, so it stays readable from afar
    [SerializeField, Min(.1f)] private float referenceZoom = 8.5f;
    [SerializeField, Min(1f)] private float maxGrow = 2f;

    private readonly Dictionary<PlacedBuilding, Transform> markers = new Dictionary<PlacedBuilding, Transform>();
    private readonly List<Transform> pool = new List<Transform>();
    private readonly List<PlacedBuilding> gone = new List<PlacedBuilding>();
    private Vector3 baseScale;
    private Camera view;

    private void Awake()
    {
        baseScale = markerPrefab.localScale;
    }

    private void LateUpdate()
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager == null) return;

        if (view == null) view = Camera.main;
        float grow = view != null && view.orthographic ? Mathf.Clamp(view.orthographicSize / referenceZoom, 1f, maxGrow) : 1f;
        float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobHeight;

        foreach (PlacedBuilding building in manager.Placed)
        {
            // A building being moved is hidden, its marker waits until it is put down.
            if (building == null || !building.IsUpgrading || !building.gameObject.activeInHierarchy) continue;

            if (!markers.TryGetValue(building, out Transform marker))
            {
                marker = Take();
                markers[building] = marker;
            }

            marker.localScale = baseScale * grow;
            marker.position = TopOf(building) + Vector3.up * (heightAboveArt * grow + bob);
        }

        // Finished, demolished or moving buildings give their marker back.
        gone.Clear();
        foreach (KeyValuePair<PlacedBuilding, Transform> pair in markers)
        {
            PlacedBuilding building = pair.Key;
            if (building == null || !building.IsUpgrading || !building.gameObject.activeInHierarchy) gone.Add(building);
        }
        foreach (PlacedBuilding building in gone)
        {
            Transform marker = markers[building];
            markers.Remove(building);
            marker.gameObject.SetActive(false);
            pool.Add(marker);
        }
    }

    private Transform Take()
    {
        Transform marker;
        if (pool.Count > 0)
        {
            marker = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        }
        else
        {
            marker = Instantiate(markerPrefab, transform);
        }
        marker.gameObject.SetActive(true);
        return marker;
    }

    // Middle of the top edge of the building's art, the marker's bottom sits there.
    private static Vector3 TopOf(PlacedBuilding building)
    {
        SpriteRenderer art = building.GetComponentInChildren<SpriteRenderer>();
        if (art == null) return building.transform.position;
        Bounds bounds = art.bounds;
        return new Vector3(bounds.center.x, bounds.max.y, 0f);
    }
}
