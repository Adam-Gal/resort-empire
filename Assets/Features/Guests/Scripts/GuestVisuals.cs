using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Draws the guests of every running wave as they walk the paths and stand in line.
// Purely a view: it reads what the simulation says each party does and puts a figure there.
// Walks take as long in the simulation as on screen, so a walking figure is wherever its party is on its route.
// A party in its room or being served is inside, so its figure fades out at the door.
// Guests step on land in front of the pier and fade out there when they leave.
// Lines form on the path in front of a building's door and, for guests still without a room, on the path from the pier.
public class GuestVisuals : MonoBehaviour
{
    [Serializable]
    public class Look
    {
        //Standing still, then 4 steps of the walk. Front faces down-right, back faces up-right, mirrored for the left
        public Sprite[] front = new Sprite[5];
        public Sprite[] back = new Sprite[5];
    }

    //Clothing colours of each figure, a party keeps the one it got
    [SerializeField] private Look[] men;
    [SerializeField] private Look[] women;
    [SerializeField] private Look[] couples;

    //Grid the buildings stand on, to tell whether a guest is in front of a building or behind it
    [SerializeField] private BuildGrid grid;

    [Header("Size")]
    //World height of a figure, the sprites are one unit tall
    [SerializeField, Min(.05f)] private float height = .6f;

    //Ground taken by one guest in a line, a couple takes two
    [SerializeField, Min(.05f)] private float queueSpacing = .28f;

    //Shadow under the feet, width and height in world units
    [SerializeField] private Vector2 shadowSize = new Vector2(.32f, .12f);
    [SerializeField, Range(0f, 1f)] private float shadowAlpha = .35f;

    [Header("Motion")]
    //World units walked per step, so feet do not slide whatever the walk speed
    [SerializeField, Min(.01f)] private float strideLength = .14f;

    //Farther than this from where it should be, a figure appears there instead of crossing the grass
    [SerializeField, Min(.1f)] private float maxStroll = 1.2f;
    [SerializeField, Min(.01f)] private float fadeSeconds = .3f;

    [Header("Sorting")]
    //Same as the buildings. A guest overlapping a building's art goes one below or one above it,
    //by whether they stand behind or in front of its footprint
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 200;

    private class Figure
    {
        public Transform root;
        public SortingGroup sorting;
        public SpriteRenderer body;
        public SpriteRenderer shadow;
        public Look look;
        public bool placed;
        public float alpha;
        public float walked;
        public bool stepping;
        public bool seen;
        public bool shown;
        public Vector3 facing = new Vector3(1f, -.5f, 0f);
    }

    private struct Art
    {
        public PlacedBuilding building;
        public Bounds bounds;
    }

    private readonly Dictionary<(string, int), Figure> figures = new Dictionary<(string, int), Figure>();
    private readonly List<Figure> pool = new List<Figure>();
    private readonly List<(string, int)> gone = new List<(string, int)>();
    private readonly Dictionary<string, List<GuestData>> lines = new Dictionary<string, List<GuestData>>();
    private readonly List<GuestData> harborLine = new List<GuestData>();
    private readonly List<Art> arts = new List<Art>();
    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private Sprite shadowSprite;
    private float walkSpeed = 1f;

    private void Awake()
    {
        shadowSprite = MakeShadow();
        if (grid == null) grid = FindFirstObjectByType<BuildGrid>();
    }

    private void LateUpdate()
    {
        WaveManager waves = WaveManager.Instance;
        GuestManager guests = GuestManager.Instance;
        foreach (Figure figure in figures.Values) figure.seen = false;
        CollectArt();

        if (waves != null && guests != null)
        {
            walkSpeed = guests.Settings.walkSpeed;
            foreach (WaveSession session in waves.Sessions)
            {
                GuestSimulation simulation = guests.GetSimulation(session.IslandId);
                if (simulation == null) continue;
                Draw(session.IslandId, simulation, guests.GetRoutes(session.IslandId));
            }
        }

        // Parties gone from the island fade out where they were, in front of the pier when they left.
        gone.Clear();
        foreach (KeyValuePair<(string, int), Figure> pair in figures)
        {
            Figure figure = pair.Value;
            if (figure.seen) continue;
            figure.shown = false;
            figure.stepping = false;
            Fade(figure);
            ShowFrame(figure);
            if (figure.alpha <= 0f) gone.Add(pair.Key);
        }
        foreach ((string, int) key in gone)
        {
            Figure figure = figures[key];
            figures.Remove(key);
            figure.root.gameObject.SetActive(false);
            pool.Add(figure);
        }
    }

    private void Draw(string islandId, GuestSimulation simulation, GuestRoutes routes)
    {
        SortLines(simulation);

        foreach (GuestData party in simulation.Parties)
        {
            if (party.state == GuestState.Left) continue;

            var key = (islandId, party.id);
            bool inside = party.state == GuestState.Resting || party.state == GuestState.InService;
            if (!figures.TryGetValue(key, out Figure figure))
            {
                // Inside since before it was drawn, it gets a figure once it steps out.
                if (inside) continue;
                figure = Take(LookOf(party));
                figures[key] = figure;
            }
            figure.seen = true;
            figure.stepping = false;

            if (TryGetWalkSpot(party, figure, routes, out Vector3 spot, out Vector3 heading))
            {
                figure.shown = true;
                MoveTo(figure, spot, heading, true);
            }
            else if (TryGetPlace(party, routes, out Vector3 place, out Vector3 facing))
            {
                figure.shown = true;
                MoveTo(figure, place, facing, false);
            }
            else
            {
                // Inside: a figure still standing nearby, like in line, walks to the door first.
                Vector3 door = routes.Position(party.state == GuestState.InService ? party.building : party.room);
                float distance = (door - figure.root.position).magnitude;
                figure.shown = figure.placed && figure.alpha > 0f && distance > .05f && distance < maxStroll;
                if (figure.shown) MoveTo(figure, door, door - figure.root.position, false);
            }

            Fade(figure);
            Sort(figure);
            ShowFrame(figure);
        }
    }

    // Where a walking party is on its route now and which way it heads.
    private bool TryGetWalkSpot(GuestData party, Figure figure, GuestRoutes routes, out Vector3 spot, out Vector3 heading)
    {
        spot = heading = Vector3.zero;
        if (party.state != GuestState.GoingToRoom && party.state != GuestState.GoingToService && party.state != GuestState.Leaving)
            return false;

        GuestRoute route = routes.Get(party.from, GuestSimulation.Destination(party));
        float done = (party.walkSeconds > 0f ? 1f - Mathf.Clamp01(party.timer / party.walkSeconds) : 1f) * route.Length;

        if (figure.placed && figure.alpha > 0f)
        {
            // Setting off from a place in line further along the same path: it waits there until the walk reaches it.
            float at = NearestAlong(route, figure.root.position);
            if (at > done && at - done < maxStroll * 4f && (route.PointAt(at, out _) - figure.root.position).sqrMagnitude < .0025f)
                done = at;
        }

        // Heading for a service with a line: the walk ends at the back of the line, not at the door.
        if (party.state == GuestState.GoingToService && lines.TryGetValue(party.building, out List<GuestData> line) && line.Count > 0)
        {
            Vector3 back = PlaceInLine(party.building, Ahead(line, null) + party.People * .5f, routes, out _);
            float stop = NearestAlong(route, back);
            if ((route.PointAt(stop, out _) - back).sqrMagnitude < .0025f) done = Mathf.Min(done, stop);
        }

        spot = route.PointAt(done, out heading);
        return true;
    }

    // The party's place in a line and the way it faces there, false when it is not standing in one.
    private bool TryGetPlace(GuestData party, GuestRoutes routes, out Vector3 place, out Vector3 facing)
    {
        place = facing = Vector3.zero;
        if (party.state == GuestState.Queued)
        {
            place = PlaceInLine(party.building, Ahead(lines[party.building], party) + party.People * .5f, routes, out facing);
            return true;
        }
        if (party.state == GuestState.WaitingForRoom)
        {
            // On the path from the pier, the first in line right where guests step on land.
            GuestRoute route = routes.HarborLine();
            if (route == null)
            {
                place = routes.Position(GuestSimulation.HarborKey);
                facing = Vector3.down;
                return true;
            }
            place = route.PointAt((Ahead(harborLine, party) + party.People * .5f) * queueSpacing, out facing);
            return true;
        }
        return false;
    }

    // A spot in the line at a building, this many guests back from its door, facing the door.
    private Vector3 PlaceInLine(string building, float guestsAhead, GuestRoutes routes, out Vector3 facing)
    {
        // Back from the door along the way guests come from the harbor.
        GuestRoute route = routes.Get(building, GuestSimulation.HarborKey);
        Vector3 place = route.PointAt(guestsAhead * queueSpacing, out Vector3 away);
        facing = -away;
        return place;
    }

    // Parties standing in each line, front first.
    private void SortLines(GuestSimulation simulation)
    {
        foreach (List<GuestData> line in lines.Values) line.Clear();
        harborLine.Clear();
        foreach (GuestData party in simulation.Parties)
        {
            if (party.state == GuestState.WaitingForRoom) harborLine.Add(party);
            else if (party.state == GuestState.Queued && !string.IsNullOrEmpty(party.building))
            {
                if (!lines.TryGetValue(party.building, out List<GuestData> line)) lines[party.building] = line = new List<GuestData>();
                line.Add(party);
            }
        }
        harborLine.Sort(ByTicket);
        foreach (List<GuestData> line in lines.Values) line.Sort(ByTicket);
    }

    private static readonly Comparison<GuestData> ByTicket = (a, b) => a.ticket.CompareTo(b.ticket);

    private static float Ahead(List<GuestData> line, GuestData party)
    {
        float ahead = 0f;
        foreach (GuestData other in line)
        {
            if (other == party) break;
            ahead += other.People;
        }
        return ahead;
    }

    private Look LookOf(GuestData party)
    {
        Look[] looks = party.People > 1 ? couples : party.look == 1 ? women : men;
        if (looks == null || looks.Length == 0) return null;

        // Spread over the colours by id, so a party keeps its clothes for the whole wave.
        uint hash = (uint)party.id * 2654435761u;
        return looks[(int)(hash >> 16) % looks.Length];
    }

    // Puts the figure at the spot. A walking party is followed exactly; a figure standing in line steps over at walking pace.
    // Too far off, or not shown yet, it appears there instead of crossing the grass.
    private void MoveTo(Figure figure, Vector3 spot, Vector3 heading, bool follow)
    {
        spot.z = 0f;
        Vector3 position = figure.root.position;
        Vector3 offset = spot - position;
        float distance = offset.magnitude;
        float moved = 0f;

        if (!figure.placed || figure.alpha <= 0f || distance > maxStroll)
        {
            position = spot;
            figure.placed = true;
            figure.alpha = 0f;
        }
        else if (follow || distance <= walkSpeed * Time.deltaTime)
        {
            position = spot;
            moved = distance;
        }
        else
        {
            moved = walkSpeed * Time.deltaTime;
            position += offset / distance * moved;
            heading = offset;
        }

        figure.root.position = position;
        if (heading.sqrMagnitude > 1e-6f) figure.facing = heading;
        figure.walked += moved;
        figure.stepping = moved > 1e-4f;
    }

    private void ShowFrame(Figure figure)
    {
        if (figure.look == null) return;

        // The art faces right, mirrored when heading left. Going down the screen shows the front.
        bool front = figure.facing.y <= 0f;
        Sprite[] frames = front ? figure.look.front : figure.look.back;
        int frame = figure.stepping ? 1 + (int)(figure.walked / strideLength) % 4 : 0;
        if (frames != null && frame < frames.Length && frames[frame] != null) figure.body.sprite = frames[frame];
        figure.body.flipX = figure.facing.x < 0f;
    }

    private void Fade(Figure figure)
    {
        float step = Time.deltaTime / fadeSeconds;
        figure.alpha = Mathf.Clamp01(figure.alpha + (figure.shown ? step : -step));
        figure.body.color = new Color(1f, 1f, 1f, figure.alpha);
        figure.shadow.color = new Color(0f, 0f, 0f, shadowAlpha * figure.alpha);
    }

    // Buildings sort by the front corner of their footprint, which is wrong for a guest standing along a front side.
    // So a guest overlapping a building's art goes below all buildings when behind its footprint, above when in front.
    private void Sort(Figure figure)
    {
        int order = sortingOrder;
        if (grid != null && figure.alpha > 0f)
        {
            Bounds body = figure.body.bounds;
            Vector2 cell = FractionalCell(figure.root.position);
            bool front = false, behind = false;
            foreach (Art art in arts)
            {
                if (!art.bounds.Intersects(body)) continue;
                Vector2 min = (Vector2)art.building.Origin - new Vector2(.5f, .5f);
                if (cell.x < min.x || cell.y < min.y) front = true;
                else behind = true;
            }
            if (behind) order = sortingOrder - 1;
            else if (front) order = sortingOrder + 1;
        }
        figure.sorting.sortingOrder = order;
    }

    private void CollectArt()
    {
        arts.Clear();
        if (BuildingManager.Instance == null) return;
        foreach (PlacedBuilding building in BuildingManager.Instance.Placed)
        {
            if (building == null || !building.gameObject.activeInHierarchy) continue;
            building.GetComponentsInChildren(renderers);
            bool any = false;
            Bounds bounds = default;
            foreach (SpriteRenderer sprite in renderers)
            {
                if (!sprite.enabled) continue;
                if (!any) { bounds = sprite.bounds; any = true; }
                else bounds.Encapsulate(sprite.bounds);
            }
            if (any)
            {
                bounds.extents = new Vector3(bounds.extents.x, bounds.extents.y, 1000f);
                arts.Add(new Art { building = building, bounds = bounds });
            }
        }
    }

    // Grid coordinates of a world point, whole numbers at cell centres.
    private Vector2 FractionalCell(Vector3 world)
    {
        Vector2 origin = grid.CellToWorld(Vector2Int.zero);
        Vector2 ex = (Vector2)grid.CellToWorld(Vector2Int.right) - origin;
        Vector2 ey = (Vector2)grid.CellToWorld(Vector2Int.up) - origin;
        Vector2 p = (Vector2)world - origin;
        float det = ex.x * ey.y - ex.y * ey.x;
        return new Vector2((p.x * ey.y - p.y * ey.x) / det, (ex.x * p.y - ex.y * p.x) / det);
    }

    // Distance along the route to its point nearest the given one.
    private static float NearestAlong(GuestRoute route, Vector3 point)
    {
        float best = 0f, bestDistance = float.MaxValue;
        for (int i = 1; i < route.points.Count; i++)
        {
            Vector3 a = route.points[i - 1], b = route.points[i];
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            float distance = (a + ab * t - point).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = route.along[i - 1] + ab.magnitude * t;
            }
        }
        return best;
    }

    private Figure Take(Look look)
    {
        Figure figure;
        if (pool.Count > 0)
        {
            figure = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        }
        else
        {
            figure = new Figure { root = new GameObject("Guest").transform };
            figure.root.SetParent(transform, false);
            figure.sorting = figure.root.gameObject.AddComponent<SortingGroup>();

            figure.shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            figure.shadow.transform.SetParent(figure.root, false);
            figure.shadow.sprite = shadowSprite;
            figure.shadow.sortingOrder = 0;

            figure.body = new GameObject("Body").AddComponent<SpriteRenderer>();
            figure.body.transform.SetParent(figure.root, false);
            figure.body.sortingOrder = 1;
        }

        figure.sorting.sortingLayerName = sortingLayer;
        figure.sorting.sortingOrder = sortingOrder;
        figure.body.transform.localScale = Vector3.one * height;
        figure.shadow.transform.localScale = new Vector3(shadowSize.x, shadowSize.y, 1f);
        figure.look = look;
        figure.placed = false;
        figure.alpha = 0f;
        figure.walked = 0f;
        figure.stepping = false;
        figure.shown = true;
        if (look != null && look.front != null && look.front.Length > 0) figure.body.sprite = look.front[0];
        figure.root.gameObject.SetActive(true);
        return figure;
    }

    // A soft round blot one unit wide, scaled flat under the feet.
    private static Sprite MakeShadow()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GuestShadow" };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2f - 1f;
                float dy = (y + .5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                pixels[x + y * size] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
    }
}
