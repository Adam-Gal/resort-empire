using System.Collections.Generic;
using UnityEngine;

// A walk along the paths, as points from start to end with their length.
public class GuestRoute
{
    public readonly List<Vector3> points = new List<Vector3>();

    //Distance from the start to each point
    public readonly List<float> along = new List<float>();
    public float Length { get; private set; }

    //False when no path joins the two places and the walk is a straight line
    public bool OnPaths { get; set; }

    public void Measure()
    {
        along.Clear();
        Length = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            // Flat on the ground plane: grid cells carry a depth that figures do not have.
            Vector3 point = points[i];
            point.z = 0f;
            points[i] = point;

            if (i > 0) Length += Vector3.Distance(points[i - 1], points[i]);
            along.Add(Length);
        }
    }

    // Point at a distance from the start and the way the walk heads there.
    public Vector3 PointAt(float distance, out Vector3 heading)
    {
        heading = Vector3.right;
        if (points.Count == 0) return Vector3.zero;
        if (points.Count == 1) return points[0];

        // Past the end the line goes on the way the last piece points, so a long queue still has room.
        int last = points.Count - 1;
        if (distance >= Length)
        {
            heading = (points[last] - points[last - 1]).normalized;
            return points[last] + heading * (distance - Length);
        }

        int i = 1;
        while (i < last && along[i] < distance) i++;
        float piece = along[i] - along[i - 1];
        heading = piece > 0f ? (points[i] - points[i - 1]) / piece : heading;
        float t = piece > 0f ? (distance - along[i - 1]) / piece : 0f;
        return Vector3.Lerp(points[i - 1], points[i], Mathf.Clamp01(t));
    }
}

// Routes guests of one island walk, between its harbor and its buildings, along the laid paths.
// Worked out once per layout and kept until the paths change.
public class GuestRoutes : IGuestRoutes
{
    private readonly string islandId;
    private readonly PathNetwork paths;
    private readonly Dictionary<string, PlacedBuilding> buildings = new Dictionary<string, PlacedBuilding>();
    private readonly Dictionary<(string, string), GuestRoute> cache = new Dictionary<(string, string), GuestRoute>();
    private int version = -1;
    private int buildingCount = -1;
    private GuestRoute harborLine;

    public GuestRoutes(string islandId, PathNetwork paths)
    {
        this.islandId = islandId;
        this.paths = paths;
    }

    public float Distance(string from, string to) => from == to ? 0f : Get(from, to).Length;

    // The walk between two places, building keys or GuestSimulation.HarborKey.
    public GuestRoute Get(string from, string to)
    {
        Refresh();
        if (cache.TryGetValue((from, to), out GuestRoute route)) return route;

        route = new GuestRoute();
        Object start = Resolve(from);
        Object end = Resolve(to);
        if (paths != null && start != null && end != null)
        {
            route.OnPaths = paths.TryGetRoute(start, end, route.points);
        }
        else
        {
            // A place gone from the island or no paths at all: no walk, the guest is just there.
            route.points.Add(PositionOf(end != null ? end : start));
        }
        route.Measure();
        cache[(from, to)] = route;
        return route;
    }

    // The path leading from the harbor into the island, where guests without a room wait in line.
    // The shortest walk from the harbor to any building on the paths, null when none is.
    public GuestRoute HarborLine()
    {
        Refresh();
        if (harborLine != null) return harborLine;
        foreach (string key in buildings.Keys)
        {
            GuestRoute route = Get(GuestSimulation.HarborKey, key);
            if (route.OnPaths && route.Length > .5f && (harborLine == null || route.Length < harborLine.Length)) harborLine = route;
        }
        return harborLine;
    }

    // Where a place stands, the start of a walk from it.
    public Vector3 Position(string place) => Get(place, place).points.Count > 0 ? Get(place, place).points[0] : Vector3.zero;

    private void Refresh()
    {
        int nowVersion = paths != null ? paths.Version : 0;
        int nowCount = BuildingManager.Instance != null ? BuildingManager.Instance.Placed.Count : 0;
        if (nowVersion == version && nowCount == buildingCount) return;

        version = nowVersion;
        buildingCount = nowCount;
        cache.Clear();
        harborLine = null;
        buildings.Clear();
        if (BuildingManager.Instance == null) return;
        foreach (PlacedBuilding building in BuildingManager.Instance.Placed)
            if (building != null) buildings[WaveManager.BuildingKey(building)] = building;
    }

    private Object Resolve(string place)
    {
        if (place == GuestSimulation.HarborKey)
            return HarborManager.Instance != null ? HarborManager.Instance.GetHarbor(islandId) : null;
        return place != null && buildings.TryGetValue(place, out PlacedBuilding building) ? building : null;
    }

    private static Vector3 PositionOf(Object place)
    {
        if (place is Harbor harbor && harbor != null) return harbor.Center;
        if (place is PlacedBuilding building && building != null) return building.transform.position;
        return Vector3.zero;
    }
}
