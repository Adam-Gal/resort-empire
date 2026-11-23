using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Boats that bring the guests of a wave to its island's harbor and take them away again.
// A new wave waits until its boat docked. With several boats on their way in, guests step off only once
// the last of them docked, so islands started together begin together. When the wave ends, after the last guest
// walked back to the harbor, the boat sails off along the site's leave route.
// Routes come from HarborManager: each site's boat start and route points end at the BoatAnchor of the harbor's level,
// its leave route starts there.
// An island without a harbor, like one whose harbor is still ruined, gets no boat and its wave begins right away.
// Boats are made as children of this object. Runs before GuestManager and WaveManager, so a wave sees its boat up to date.
[DefaultExecutionOrder(-80)]
public class BoatManager : MonoBehaviour
{
    public static BoatManager Instance { get; private set; }

    //Boat made for every wave, a BoatView on it sets how it looks
    [SerializeField] private GameObject boatPrefab;

    //How every boat moves, they all share it so they sail at the same speed
    [SerializeField] private BoatMotion motion = new BoatMotion();

    //Spacing of the points the smooth route is made of, in world units
    [SerializeField, Min(.02f)] private float routeStep = .1f;

    //Lowest harbor level boats come to, 0 is the level a harbor is first built at
    [SerializeField, Min(0)] private int minHarborLevel = 0;

    [Header("Sorting")]
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 100;

    private class Entry
    {
        public Boat boat;
        public BoatView view;
    }

    private readonly List<Entry> boats = new List<Entry>();

    // Last wave number given a boat on each island, so every wave is looked at once.
    private readonly Dictionary<string, int> handled = new Dictionary<string, int>();
    private readonly List<Vector3> pathIn = new List<Vector3>();
    private readonly List<Vector3> pathOut = new List<Vector3>();

    public BoatMotion Motion => motion;

    private void Awake()
    {
        Instance = this;
        if (boatPrefab == null) Debug.LogError("[BoatManager] No boat prefab, waves start without boats.", this);
    }

    private void Start()
    {
        if (WaveManager.Instance != null) WaveManager.Instance.WaveStarted += Attach;
    }

    private void OnDestroy()
    {
        WaveManager waves = WaveManager.Instance;
        if (waves != null)
        {
            waves.WaveStarted -= Attach;

            // No wave may keep waiting for a boat that is gone.
            foreach (WaveSession session in waves.Sessions)
                if (session.Arrival is Boat) session.Arrival = null;
        }
        if (Instance == this) Instance = null;
    }

    // The boat of the wave running on the island, null when it has none.
    public Boat GetBoat(string islandId)
    {
        WaveSession session = WaveManager.Instance != null ? WaveManager.Instance.GetSession(islandId) : null;
        if (session == null) return null;
        foreach (Entry entry in boats)
            if (entry.boat.IslandId == islandId && entry.boat.WaveNumber == session.Number) return entry.boat;
        return null;
    }

    private void Update()
    {
        WaveManager waves = WaveManager.Instance;
        if (waves != null)
        {
            // Waves loaded from the save, or started before this saw them, get their boat.
            foreach (WaveSession session in waves.Sessions) Attach(session);

            // A boat whose wave is over sails off.
            foreach (Entry entry in boats)
            {
                WaveSession session = waves.GetSession(entry.boat.IslandId);
                if (session == null || session.Number != entry.boat.WaveNumber) entry.boat.Depart();
            }
        }

        // Play time only, like the waves themselves.
        float seconds = Time.deltaTime;
        bool anyArriving = false;
        foreach (Entry entry in boats)
        {
            entry.boat.Tick(seconds);
            if (entry.boat.State == BoatState.Arriving) anyArriving = true;
        }

        // Guests step off only once every boat on its way in has docked.
        if (!anyArriving)
            foreach (Entry entry in boats) entry.boat.Land();

        for (int i = boats.Count - 1; i >= 0; i--)
        {
            Entry entry = boats[i];
            if (entry.boat.State == BoatState.Gone)
            {
                Remove(i, waves);
                continue;
            }
            entry.view.Show(entry.boat.Position, entry.boat.Facing, entry.boat.Moving, entry.boat.Alpha);
        }
    }

    // Gives the wave a boat when its island has an open harbor. Once per wave.
    private void Attach(WaveSession session)
    {
        if (handled.TryGetValue(session.IslandId, out int number) && number == session.Number) return;
        handled[session.IslandId] = session.Number;
        if (session.Phase == WavePhase.Ended || boatPrefab == null) return;

        if (!FindRoutes(session.IslandId, out BoatRoute wayIn, out BoatRoute wayOut))
        {
            Debug.Log($"[BoatManager] No boat sails to island {session.IslandId}: it has no harbor, the harbor is below " +
                      $"level {minHarborLevel}, its site in HarborManager has no boat start, or the level's art has no " +
                      $"{Harbor.BoatAnchorName}. The wave starts without a boat.", this);
            return;
        }

        // The island's previous boat, still on its way out, makes room.
        foreach (Entry entry in boats)
            if (entry.boat.IslandId == session.IslandId) entry.boat.Vanish();

        // A wave that already began, loaded from the save, finds its boat docked.
        bool docked = session.Phase != WavePhase.Arriving;
        var boat = new Boat(session.IslandId, session.Number, wayIn, wayOut, motion, docked);
        boats.Add(new Entry { boat = boat, view = Spawn(boat) });
        session.Arrival = boat;
    }

    // The ways in to and out from the island's harbor, false when no boat can sail there.
    // A site without a leave route sends its boats back the way they came.
    private bool FindRoutes(string islandId, out BoatRoute wayIn, out BoatRoute wayOut)
    {
        wayIn = wayOut = null;
        HarborManager harbors = HarborManager.Instance;
        if (harbors == null) return false;

        pathIn.Clear();
        pathOut.Clear();
        if (!harbors.TryGetBoatPaths(islandId, pathIn, pathOut, out Harbor harbor)) return false;
        if (harbor.Level < minHarborLevel) return false;

        wayIn = BoatRoute.Through(pathIn, routeStep);
        if (wayIn == null) return false;

        if (pathOut.Count < 2)
        {
            pathOut.Clear();
            for (int i = pathIn.Count - 1; i >= 0; i--) pathOut.Add(pathIn[i]);
        }
        wayOut = BoatRoute.Through(pathOut, routeStep);
        return wayOut != null;
    }

    private BoatView Spawn(Boat boat)
    {
        GameObject made = Instantiate(boatPrefab, boat.Position, Quaternion.identity, transform);
        made.name = $"Boat {boat.IslandId} #{boat.WaveNumber}";

        SortingGroup sorting = made.GetComponent<SortingGroup>();
        if (sorting == null) sorting = made.AddComponent<SortingGroup>();
        sorting.sortingLayerName = sortingLayer;
        sorting.sortingOrder = sortingOrder;

        BoatView view = made.GetComponent<BoatView>();
        if (view == null) view = made.AddComponent<BoatView>();
        view.Show(boat.Position, boat.Facing, false, boat.Alpha);
        return view;
    }

    private void Remove(int index, WaveManager waves)
    {
        Entry entry = boats[index];
        boats.RemoveAt(index);

        WaveSession session = waves != null ? waves.GetSession(entry.boat.IslandId) : null;
        if (session != null && session.Arrival == entry.boat) session.Arrival = null;
        if (entry.view != null) Destroy(entry.view.gameObject);
    }
}