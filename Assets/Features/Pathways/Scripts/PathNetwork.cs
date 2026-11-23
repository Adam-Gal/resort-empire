using System.Collections.Generic;
using ResortEmpire.Presentation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;

// Paths are drawn on their own from each harbor to every building guests use.
// They are worked out again only when the layout changes, never per frame, and from the
// layout alone, so a loaded save draws the same paths without storing them.
// Paths never block building: a building placed on one makes the path go around it.
//
// A path is two cells wide. It is laid in 2x2 blocks named by their lowest cell, and each
// block is one tile drawn on its middle (the path tilemap's tile anchor is 1,1).
// Blocks step two cells, so pieces sit edge to edge and turn with real corner tiles.
// To land exactly on a door a path may take a one-cell step. The piece it lays is always a
// straight one, never a corner, so the overlap keeps the strip's edges clean.
// The steps can line up with the pier two ways along it; each rebuild tries both and keeps
// the one that reaches the most buildings, nearest their doors, with the least path.
//
// A building no path reaches is tinted red, and a short error notice says so the moment it happens.
public class PathNetwork : MonoBehaviour
{
    // NE (+x), NW (+y), SW (-x), SE (-y). Bit 1 << index is that side in the Path_Connect tiles.
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1)
    };

    private const int Width = 2;

    //Search states per block: entered by a whole step in one of 4 directions, by none (a start),
    //or by a one-cell step in one of 4 directions (HalfStepState + direction)
    private const int NoDirection = 4;
    private const int HalfStepState = 5;
    private const int StatesPerCell = 9;
    private const int StepCost = 2;

    //Makes paths run straight instead of in steps
    private const int TurnCost = 2;

    //A one-cell step inside a straight run, dear so paths take it only to line up with a door
    private const int HalfStepCost = 4;

    //Added for a block touching shore, so paths keep off it unless there is no other way
    private const int ShoreCost = 3;

    //Added per rank when a path ends off its door, so a slightly longer way to the door itself wins
    private const int RankCost = 4;
    private const int MaxRank = 2;

    //More than the dearest step, so the bucket queue never wraps onto itself
    private const int BucketCount = HalfStepCost + TurnCost + ShoreCost + RankCost * MaxRank + 1;

    //Ways the two-cell steps can line up with a pier, a cell further along it or not
    private const int Alignments = 2;
    private const byte NotDrawn = 255;

    private static readonly ProfilerMarker RebuildMarker = new ProfilerMarker("PathNetwork.Rebuild");

    [SerializeField] private BuildGrid grid;
    [SerializeField] private Tilemap pathLayer;

    //Index is the mask of open sides: 1 NE, 2 NW, 4 SW, 8 SE. Tiles cover 2x2 cells
    [SerializeField] private TileBase[] connectTiles = new TileBase[16];

    //Ground a path may cross, jungle and water are left out
    [SerializeField] private TileCategory[] walkableTiles;

    //Ground paths cross only when they must, like the shore where piers meet the land or in front of a beach bar
    [SerializeField] private TileCategory[] landingTiles = new TileCategory[0];

    //The island tilemaps, paths stay inside their bounds
    [SerializeField] private Tilemap[] area;

    //How many cells from a harbor its path may start
    [SerializeField, Min(1)] private int harborReach = 8;
    //Multiplies the art of a building no path can reach, its door is blocked or cut off from the harbor
    [SerializeField] private Color blockedTint = new Color(1f, .45f, .45f, 1f);

    //Error notice shown when a building gets cut off. {0} is the building's name
    [SerializeField] private NoticeStack errorNotices;
    [SerializeField] private string doorBlockedText = "{0}: vchod je zablokovaný";
    [SerializeField] private string noRouteText = "{0}: chýba cesta";
    [SerializeField] private bool logTimings;

    private int xMin, yMin, width, height;
    private bool ready;

    //Terrain, blocked areas and locks. Worked out again only when an island opens
    private bool[] baseWalkable;
    private bool[] shore;
    private bool baseDirty = true;

    //Buildings, ruins and jungle on a cell, read lazily during one rebuild
    private bool[] occupied;
    private int[] occupiedStamp;

    //Stamps start above the arrays' zeroes, so nothing counts as set before the first rebuild
    private int generation = 1;
    private int currentTargetStamp;

    //Blocks drawn this rebuild, by their lowest cell
    private int[] inNetworkStamp;
    private byte[] links;
    private byte[] drawn;
    private readonly List<int> network = new List<int>();

    //Blocks on screen now, so a rebuild only touches tiles that change
    private readonly List<int> drawnBlocks = new List<int>();

    //Blocks new paths may branch from
    private readonly List<int> sources = new List<int>();

    //Cells under a path, for IsPath
    private int[] coveredStamp;

    //Blocks of a one-cell step, nothing branches from them
    private int[] noBranchStamp;

    //Search state, one per block and the direction it was entered from
    private int[] cost;
    private int[] previous;
    private int[] searchStamp;
    private int searchGeneration;
    private readonly List<int>[] buckets = new List<int>[BucketCount];

    //A building still to connect and the blocks along its sides that reach it
    private struct Target
    {
        public PlacedBuilding building;
        public int approachStart;
        public int approachCount;
        public bool connected;
    }

    //Where a path may end for a building: the block, the way into the building, and how far it is
    //from the ideal spot (0 right on the door, more when tucked under the front or slid aside)
    private struct Approach
    {
        public int block;
        public int direction;
        public int rank;
    }

    private readonly HashSet<PlacedBuilding> blocked = new HashSet<PlacedBuilding>();

    //Blocked ones whose door spot itself is taken, the rest only lack a route
    private readonly HashSet<PlacedBuilding> doorCovered = new HashSet<PlacedBuilding>();
    private readonly HashSet<PlacedBuilding> nowBlocked = new HashSet<PlacedBuilding>();
    private readonly Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    private readonly List<SpriteRenderer> spriteBuffer = new List<SpriteRenderer>();

    private readonly List<Target> targets = new List<Target>();
    private readonly List<Approach> approaches = new List<Approach>();
    private int[] targetStamp;
    private int[] approachAt;

    //How far the ends of the current layout are from their doors, summed
    private int rankSum;

    private int lastSignature;
    private bool forceRebuild = true;

    //Guest routes: blocks the laid paths join, where each building's path ends and where each harbor's starts.
    //Written by every Lay, the last one is the layout on screen
    private readonly List<Vector2Int> laidLinks = new List<Vector2Int>();
    private readonly Dictionary<PlacedBuilding, int> buildingEnds = new Dictionary<PlacedBuilding, int>();
    private readonly Dictionary<Harbor, int> harborRoots = new Dictionary<Harbor, int>();
    private readonly Dictionary<int, List<int>> linked = new Dictionary<int, List<int>>();
    private readonly Dictionary<int, float> routeCost = new Dictionary<int, float>();
    private readonly Dictionary<int, int> routeFrom = new Dictionary<int, int>();
    private readonly List<int> routeOpen = new List<int>();

    // Goes up every time the paths are laid again, so routes worked out before can be dropped.
    public int Version { get; private set; }

    public bool IsPath(Vector2Int cell)
    {
        int index = IndexOf(cell);
        return index >= 0 && coveredStamp[index] == generation;
    }

    private void Start()
    {
        BoundsInt bounds = area[0].cellBounds;
        foreach (Tilemap tilemap in area)
        {
            BoundsInt b = tilemap.cellBounds;
            bounds.SetMinMax(Vector3Int.Min(bounds.min, b.min), Vector3Int.Max(bounds.max, b.max));
        }
        xMin = bounds.xMin;
        yMin = bounds.yMin;
        width = bounds.size.x;
        height = bounds.size.y;

        int cells = width * height;
        baseWalkable = new bool[cells];
        shore = new bool[cells];
        occupied = new bool[cells];
        occupiedStamp = new int[cells];
        inNetworkStamp = new int[cells];
        coveredStamp = new int[cells];
        noBranchStamp = new int[cells];
        links = new byte[cells];
        drawn = new byte[cells];
        for (int i = 0; i < cells; i++) drawn[i] = NotDrawn;
        targetStamp = new int[cells];
        approachAt = new int[cells];
        cost = new int[cells * StatesPerCell];
        previous = new int[cells * StatesPerCell];
        searchStamp = new int[cells * StatesPerCell];
        for (int i = 0; i < BucketCount; i++) buckets[i] = new List<int>(256);

        pathLayer.ClearAllTiles();
        ready = true;

        BuildingManager.Instance.Changed += OnLayoutChanged;
        if (IslandProgress.Instance != null) IslandProgress.Instance.Changed += OnIslandsChanged;
        if (HarborManager.Instance != null) HarborManager.Instance.Changed += OnLayoutChanged;
        enabled = true;
    }

    private void OnDestroy()
    {
        if (BuildingManager.Instance != null) BuildingManager.Instance.Changed -= OnLayoutChanged;
        if (IslandProgress.Instance != null) IslandProgress.Instance.Changed -= OnIslandsChanged;
        if (HarborManager.Instance != null) HarborManager.Instance.Changed -= OnLayoutChanged;
    }

    // Many changes in one frame end in a single rebuild in LateUpdate, then the component sleeps.
    private void OnLayoutChanged() => enabled = true;

    private void OnIslandsChanged()
    {
        baseDirty = true;
        forceRebuild = true;
        enabled = true;
    }

    private void LateUpdate()
    {
        enabled = false;
        if (!ready) return;

        // Hiring or an upgrade also raise Changed, but leave every path where it was.
        int signature = LayoutSignature();
        if (forceRebuild || signature != lastSignature)
        {
            lastSignature = signature;
            forceRebuild = false;

            float start = Time.realtimeSinceStartup;
            using (RebuildMarker.Auto())
                Rebuild();
            if (logTimings)
                Debug.Log($"[PathNetwork] {network.Count} path tiles, {targets.Count} buildings, {blocked.Count} blocked, {(Time.realtimeSinceStartup - start) * 1000f:0.00} ms");
        }
        TintBlocked();
    }

    private int LayoutSignature()
    {
        unchecked
        {
            int hash = 17;
            IReadOnlyList<PlacedBuilding> placed = BuildingManager.Instance.Placed;
            for (int i = 0; i < placed.Count; i++)
            {
                PlacedBuilding building = placed[i];
                hash = hash * 31 + building.Origin.x;
                hash = hash * 31 + building.Origin.y;
                hash = hash * 31 + building.Definition.size.x;
                hash = hash * 31 + building.Definition.size.y;
                hash = hash * 31 + (building.Definition.IsDecoration ? 1 : 2);
            }
            hash = hash * 31 + grid.ObstacleCellCount;
            if (HarborManager.Instance != null) hash = hash * 31 + HarborManager.Instance.Harbors.Count;
            return hash;
        }
    }

    private void Rebuild()
    {
        if (baseDirty) RebuildBase();

        // Each alignment is laid out in full, the best is laid again last so its state stays.
        // Best reaches the most buildings, then ends nearest their doors, then has the least path.
        int best = 0, bestConnected = -1, bestRank = int.MaxValue, bestSize = int.MaxValue;
        for (int alignment = 0; alignment < Alignments; alignment++)
        {
            int connected = Lay(alignment);
            bool better = connected != bestConnected ? connected > bestConnected
                : rankSum != bestRank ? rankSum < bestRank
                : network.Count < bestSize;
            if (!better) continue;
            best = alignment;
            bestConnected = connected;
            bestRank = rankSum;
            bestSize = network.Count;
        }
        if (best != Alignments - 1) Lay(best);

        LinkRoutes();
        Draw();
        UpdateBlocked();
    }

    // Lays every path for one alignment. Returns how many buildings it reached.
    private int Lay(int alignment)
    {
        generation++;
        network.Clear();
        sources.Clear();
        rankSum = 0;
        laidLinks.Clear();
        buildingEnds.Clear();
        harborRoots.Clear();

        AddHarborRoots(alignment);
        CollectTargets();

        int remaining = 0;
        for (int i = 0; i < targets.Count; i++)
            if (targets[i].approachCount > 0) remaining++;

        // What is left when the search runs dry has its door blocked or no way to a harbor.
        int connected = 0;
        while (remaining > 0)
        {
            int reached = Search();
            if (reached < 0) break;
            Connect(reached);
            connected++;
            remaining--;
            MarkTargets();
        }
        return connected;
    }

    // Ground, landmarks and locks. Scanned on load and when an island opens, jungle and buildings are read lazily.
    private void RebuildBase()
    {
        baseDirty = false;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = new Vector2Int(x + xMin, y + yMin);
                TileCategory category = grid.GetTerrainCategory(cell);
                int index = x + y * width;
                shore[index] = Contains(landingTiles, category);
                baseWalkable[index] = (shore[index] || Contains(walkableTiles, category)) &&
                                      !grid.IsBlocked(cell) && !grid.IsLocked(cell);
            }
        }
    }

    private static bool Contains(TileCategory[] categories, TileCategory category)
    {
        if (category == null) return false;
        for (int i = 0; i < categories.Length; i++)
            if (categories[i] == category) return true;
        return false;
    }

    private int BlockCost(int block) =>
        shore[block] || shore[block + 1] || shore[block + width] || shore[block + width + 1] ? ShoreCost : 0;

    private int IndexOf(Vector2Int cell)
    {
        int x = cell.x - xMin;
        int y = cell.y - yMin;
        if (x < 0 || y < 0 || x >= width || y >= height) return -1;
        return x + y * width;
    }

    private Vector2Int CellOf(int index) => new Vector2Int(index % width + xMin, index / width + yMin);

    private bool IsWalkable(int index)
    {
        if (!baseWalkable[index]) return false;
        if (occupiedStamp[index] != generation)
        {
            occupiedStamp[index] = generation;
            Vector2Int cell = CellOf(index);
            occupied[index] = !ReferenceEquals(grid.GetOccupant(cell), null) || grid.TryGetObstacle(cell, out _);
        }
        return !occupied[index];
    }

    // Block index of the 2x2 cells starting at this one, -1 when any of them is taken.
    private int FreeBlock(Vector2Int cell)
    {
        int index = IndexOf(cell);
        if (index < 0 || cell.x - xMin + 1 >= width || cell.y - yMin + 1 >= height) return -1;
        return IsWalkable(index) && IsWalkable(index + 1) && IsWalkable(index + width) && IsWalkable(index + width + 1)
            ? index
            : -1;
    }

    private void AddToNetwork(int block, bool isSource)
    {
        if (inNetworkStamp[block] == generation) return;
        inNetworkStamp[block] = generation;
        links[block] = 0;
        network.Add(block);
        if (isSource) sources.Add(block);

        coveredStamp[block] = generation;
        coveredStamp[block + 1] = generation;
        coveredStamp[block + width] = generation;
        coveredStamp[block + width + 1] = generation;
    }

    // Each harbor's path starts where its pier meets the land, in line with the pier and open towards it.
    // alignment 1 moves that first block a cell further along the pier.
    private void AddHarborRoots(int alignment)
    {
        if (HarborManager.Instance == null) return;

        foreach (Harbor harbor in HarborManager.Instance.Harbors)
        {
            if (harbor == null) continue;
            if (TryPierEnd(harbor, alignment, out int block, out int towardsPier))
            {
                AddToNetwork(block, true);
                links[block] |= (byte)(1 << towardsPier);
                harborRoots[harbor] = block;
            }
            else
                AddNearestRoot(harbor, alignment);
        }
    }

    // The pier lies along the grid's x axis, a mirrored one along y. The first block sits on the
    // pier's end, half under it, so the path comes out from beneath the planks. Of the two ends,
    // the one with land closer wins.
    private bool TryPierEnd(Harbor harbor, int alignment, out int block, out int towardsPier)
    {
        block = -1;
        towardsPier = 0;
        Vector2 middle = FractionalCell(harbor.Center);
        int axis = harbor.Mirrored ? 1 : 0;
        int across = 1 - axis;
        float halfLength = HarborManager.Instance.Definition.size.x * .5f;

        // The block is two cells wide, so it is centred across the pier.
        int acrossStart = Mathf.RoundToInt(middle[across] - .5f);
        int along = alignment & 1;

        int bestStep = int.MaxValue;
        for (int sign = 1; sign >= -1; sign -= 2)
        {
            // Block centred on the end: its two cells are at start and start + 1.
            int first = Mathf.RoundToInt(middle[axis] + sign * halfLength - .5f) + sign * along;
            for (int step = 0; step <= harborReach && step < bestStep; step++)
            {
                var cell = Vector2Int.zero;
                cell[axis] = first + sign * step;
                cell[across] = acrossStart;
                int found = FreeBlock(cell);
                if (found < 0) continue;

                bestStep = step;
                block = found;
                // Directions: 0 +x, 1 +y, 2 -x, 3 -y. Back towards the pier is against sign.
                towardsPier = sign > 0 ? axis + 2 : axis;
                break;
            }
        }
        return block >= 0;
    }

    // Cell coordinates of a world point, whole numbers at cell centres.
    private Vector2 FractionalCell(Vector3 world)
    {
        Vector2 origin = grid.CellToWorld(Vector2Int.zero);
        Vector2 ex = (Vector2)grid.CellToWorld(Vector2Int.right) - origin;
        Vector2 ey = (Vector2)grid.CellToWorld(Vector2Int.up) - origin;
        Vector2 p = (Vector2)world - origin;
        float det = ex.x * ey.y - ex.y * ey.x;
        return new Vector2((p.x * ey.y - p.y * ey.x) / det, (ex.x * p.y - ex.y * p.x) / det);
    }

    // Fallback for odd harbor art: the free block nearest its middle, moved by the alignment when free.
    private void AddNearestRoot(Harbor harbor, int alignment)
    {
        Vector3 pier = harbor.Center;
        Vector2Int center = grid.WorldToCell(pier);
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int dy = -harborReach; dy <= harborReach; dy++)
        {
            for (int dx = -harborReach; dx <= harborReach; dx++)
            {
                var cell = new Vector2Int(center.x + dx, center.y + dy);
                int block = FreeBlock(cell);
                if (block < 0) continue;
                float distance = (BlockCenter(cell) - pier).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = block;
                }
            }
        }
        if (best < 0) return;

        int moved = FreeBlock(CellOf(best) + new Vector2Int(alignment & 1, 0));
        if (moved >= 0) best = moved;
        AddToNetwork(best, true);
        links[best] |= (byte)(1 << DirectionTowards(CellOf(best), pier));
        harborRoots[harbor] = best;
    }

    private Vector3 BlockCenter(Vector2Int cell) => (grid.CellToWorld(cell) + grid.CellToWorld(cell + Vector2Int.one)) * .5f;

    private int DirectionTowards(Vector2Int block, Vector3 world)
    {
        Vector3 from = BlockCenter(block);
        Vector2 offset = world - from;
        int best = 0;
        float bestDot = float.MinValue;
        for (int d = 0; d < Directions.Length; d++)
        {
            float dot = Vector2.Dot(offset, (Vector2)(grid.CellToWorld(block + Directions[d]) - grid.CellToWorld(block)).normalized);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = d;
            }
        }
        return best;
    }

    // Every building guests go to, with the blocks a path may reach it from.
    private void CollectTargets()
    {
        targets.Clear();
        approaches.Clear();
        IReadOnlyList<PlacedBuilding> placed = BuildingManager.Instance.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            PlacedBuilding building = placed[i];
            if (building.Definition.IsDecoration) continue;

            int start = approaches.Count;
            if (building.Definition.doorSide != DoorSide.None)
                AddDoorApproaches(building);
            else
                AddSideApproaches(building);
            targets.Add(new Target { building = building, approachStart = start, approachCount = approaches.Count - start });
        }
        MarkTargets();
    }

    // Without a door any block touching a side will do. Blocks lying wholly along the side come
    // first, ones sticking a cell past a corner after.
    private void AddSideApproaches(PlacedBuilding building)
    {
        Vector2Int origin = building.Origin;
        Vector2Int size = building.Definition.size;
        for (int outward = 0; outward < Directions.Length; outward++)
        {
            bool alongY = outward == 0 || outward == 2;
            int sideLength = alongY ? size.y : size.x;
            for (int k = -1; k <= sideLength - Width + 1; k++)
            {
                int rank = k < 0 || k > sideLength - Width ? 2 : 0;
                AddEndAt(building, SideBlock(origin, size, outward, k), outward, rank);
            }
        }
    }

    // The path ends on the door. Only when the door spot is taken does it end a cell aside.
    private void AddDoorApproaches(PlacedBuilding building)
    {
        BuildingDefinition definition = building.Definition;
        int outward = (int)definition.doorSide - 1;
        bool alongY = outward == 0 || outward == 2;
        int sideLength = alongY ? definition.size.y : definition.size.x;
        int k = Mathf.Clamp(definition.doorOffset, 0, Mathf.Max(0, sideLength - Width));
        Vector2Int door = SideBlock(building.Origin, definition.size, outward, k);
        Vector2Int aside = alongY ? Vector2Int.up : Vector2Int.right;

        AddEndAt(building, door, outward, 0);
        AddEndAt(building, door + aside, outward, 2);
        AddEndAt(building, door - aside, outward, 2);
    }

    // The block touching side outward of the footprint, k cells from that side's lower end.
    private static Vector2Int SideBlock(Vector2Int origin, Vector2Int size, int outward, int k)
    {
        switch (outward)
        {
            case 0: return new Vector2Int(origin.x + size.x, origin.y + k);
            case 1: return new Vector2Int(origin.x + k, origin.y + size.y);
            case 2: return new Vector2Int(origin.x - Width, origin.y + k);
            default: return new Vector2Int(origin.x + k, origin.y - Width);
        }
    }

    // A block touching the building on side outward, opening into it.
    private void AddEndAt(PlacedBuilding building, Vector2Int cell, int outward, int rank)
    {
        int block = FreeBlock(cell);
        if (block >= 0) approaches.Add(new Approach { block = block, direction = (outward + 2) % 4, rank = rank });
    }

    // A new stamp drops the marks of buildings connected since, without clearing the array.
    private void MarkTargets()
    {
        currentTargetStamp++;
        for (int t = 0; t < targets.Count; t++)
        {
            Target target = targets[t];
            if (target.connected) continue;
            for (int a = 0; a < target.approachCount; a++)
            {
                int index = target.approachStart + a;
                int block = approaches[index].block;

                // The spot nearest the door wins where approaches share a block.
                if (targetStamp[block] == currentTargetStamp &&
                    approaches[approachAt[block]].rank <= approaches[index].rank) continue;
                targetStamp[block] = currentTargetStamp;
                approachAt[block] = index;
            }
        }
    }

    private int TargetOf(int approach)
    {
        for (int t = 0; t < targets.Count; t++)
        {
            Target target = targets[t];
            if (approach >= target.approachStart && approach < target.approachStart + target.approachCount) return t;
        }
        return -1;
    }

    // Cheapest way from the paths so far to the nearest building still waiting, bucket queued Dijkstra
    // over blocks two cells apart. Returns the search state that reached it, or -1 when nothing more can be reached.
    private int Search()
    {
        searchGeneration++;
        for (int i = 0; i < BucketCount; i++) buckets[i].Clear();
        int queued = 0;

        for (int i = 0; i < sources.Count; i++)
        {
            if (noBranchStamp[sources[i]] == generation) continue;
            int state = sources[i] * StatesPerCell + NoDirection;
            searchStamp[state] = searchGeneration;
            cost[state] = 0;
            previous[state] = -1;
            buckets[0].Add(state);
            queued++;
        }

        int current = 0;
        while (queued > 0)
        {
            List<int> bucket = buckets[current % BucketCount];
            if (bucket.Count == 0)
            {
                current++;
                continue;
            }

            int state = bucket[bucket.Count - 1];
            bucket.RemoveAt(bucket.Count - 1);
            queued--;
            if (cost[state] != current) continue;

            int block = state / StatesPerCell;
            if (targetStamp[block] == currentTargetStamp) return state;

            int entered = state % StatesPerCell;
            bool afterHalf = entered >= HalfStepState;
            int direction = afterHalf ? entered - HalfStepState : entered;
            Vector2Int cell = CellOf(block);
            for (int d = 0; d < Directions.Length; d++)
            {
                // Right after a one-cell step only a whole step straight on is allowed, so the
                // overlapping piece is always a straight one and its edges stay on the strip's edges.
                if (afterHalf && d != direction) continue;
                int turn = direction != NoDirection && direction != d ? TurnCost : 0;
                Relax(state, current, FreeBlock(cell + Directions[d] * Width), d, StepCost + turn, ref queued);

                if (!afterHalf)
                    Relax(state, current, FreeBlock(cell + Directions[d]), HalfStepState + d, HalfStepCost + turn, ref queued);
            }
        }
        return -1;
    }

    private void Relax(int from, int current, int next, int entered, int stepCost, ref int queued)
    {
        // Paths laid already are branched from, never run over or half covered.
        // A door spot right beside a path is still taken, rather than leaving the building cut off.
        if (next < 0 || (IsCovered(next) && targetStamp[next] != currentTargetStamp)) return;

        int nextCost = current + stepCost + BlockCost(next);
        if (targetStamp[next] == currentTargetStamp) nextCost += RankCost * Mathf.Min(MaxRank, approaches[approachAt[next]].rank);
        int nextState = next * StatesPerCell + entered;
        if (searchStamp[nextState] == searchGeneration && cost[nextState] <= nextCost) return;

        searchStamp[nextState] = searchGeneration;
        cost[nextState] = nextCost;
        previous[nextState] = from;
        buckets[nextCost % BucketCount].Add(nextState);
        queued++;
    }

    private bool IsCovered(int block) =>
        coveredStamp[block] == generation || coveredStamp[block + 1] == generation ||
        coveredStamp[block + width] == generation || coveredStamp[block + width + 1] == generation;

    private static int DirectionOf(int state)
    {
        int entered = state % StatesPerCell;
        return entered >= HalfStepState ? entered - HalfStepState : entered;
    }

    // Lays the found route as path and opens its end towards the building.
    private void Connect(int reachedState)
    {
        int end = reachedState / StatesPerCell;
        int approach = approachAt[end];
        AddToNetwork(end, true);
        links[end] |= (byte)(1 << approaches[approach].direction);
        rankSum += approaches[approach].rank;

        int state = reachedState;
        while (previous[state] >= 0)
        {
            int from = previous[state] / StatesPerCell;
            int to = state / StatesPerCell;
            int direction = DirectionOf(state);

            // The two blocks of a one-cell step overlap, a branch from either would cover the strip.
            if (state % StatesPerCell >= HalfStepState)
            {
                noBranchStamp[from] = generation;
                noBranchStamp[to] = generation;
            }
            AddToNetwork(from, true);
            AddToNetwork(to, true);
            laidLinks.Add(new Vector2Int(from, to));
            links[from] |= (byte)(1 << direction);
            links[to] |= (byte)(1 << ((direction + 2) % 4));
            state = previous[state];
        }

        int target = TargetOf(approach);
        buildingEnds[targets[target].building] = end;
        MarkConnected(target);
    }

    // Joins the blocks of the laid paths both ways, for routes.
    private void LinkRoutes()
    {
        foreach (List<int> next in linked.Values) next.Clear();
        foreach (Vector2Int link in laidLinks)
        {
            Link(link.x, link.y);
            Link(link.y, link.x);
        }
        Version++;
    }

    private void Link(int from, int to)
    {
        if (!linked.TryGetValue(from, out List<int> next)) linked[from] = next = new List<int>();
        if (!next.Contains(to)) next.Add(to);
    }

    // The way a guest walks between two places along the paths, each a building (from the block at its door)
    // or a harbor (from its pier). Fills points from start to end. False when no path joins them,
    // the points are then just the two places, for a straight walk.
    public bool TryGetRoute(Object from, Object to, List<Vector3> points)
    {
        points.Clear();
        Vector3 start = PlaceOf(from, out int startBlock);
        Vector3 end = PlaceOf(to, out int endBlock);
        points.Add(start);

        if (!ready || startBlock < 0 || endBlock < 0 || !FindRoute(startBlock, endBlock))
        {
            points.Add(end);
            return false;
        }

        // Back from the end, then turned around.
        int first = points.Count;
        for (int block = endBlock; ; block = routeFrom[block])
        {
            points.Add(BlockCenter(CellOf(block)));
            if (block == startBlock) break;
        }
        points.Reverse(first, points.Count - first);
        points.Add(end);
        DropRepeats(points);
        return true;
    }

    // Where a place stands for walking and the path block it is reached from, -1 when none reaches it.
    private Vector3 PlaceOf(Object place, out int block)
    {
        block = -1;
        if (place is Harbor harbor)
        {
            // Guests step on land where the pier meets the path, they do not walk the pier itself.
            if (harbor != null && harborRoots.TryGetValue(harbor, out int root))
            {
                block = root;
                return BlockCenter(CellOf(root));
            }
            return harbor != null ? harbor.Center : Vector3.zero;
        }
        if (place is PlacedBuilding building && building != null)
        {
            if (buildingEnds.TryGetValue(building, out int end))
            {
                block = end;
                return BlockCenter(CellOf(end));
            }
            return grid.FootprintCenter(building.Origin, building.Definition.size);
        }
        return Vector3.zero;
    }

    // Shortest way over the laid blocks, Dijkstra with a plain list: the network is small and routes are cached.
    private bool FindRoute(int start, int goal)
    {
        routeCost.Clear();
        routeFrom.Clear();
        routeOpen.Clear();
        routeCost[start] = 0f;
        routeFrom[start] = start;
        routeOpen.Add(start);

        while (routeOpen.Count > 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < routeOpen.Count; i++)
                if (routeCost[routeOpen[i]] < routeCost[routeOpen[bestIndex]]) bestIndex = i;
            int block = routeOpen[bestIndex];
            routeOpen.RemoveAt(bestIndex);
            if (block == goal) return true;
            if (!linked.TryGetValue(block, out List<int> next)) continue;

            Vector3 here = BlockCenter(CellOf(block));
            foreach (int other in next)
            {
                float cost = routeCost[block] + Vector3.Distance(here, BlockCenter(CellOf(other)));
                if (routeCost.TryGetValue(other, out float known) && known <= cost) continue;
                if (!routeCost.ContainsKey(other)) routeOpen.Add(other);
                routeCost[other] = cost;
                routeFrom[other] = block;
            }
        }
        return false;
    }

    private static void DropRepeats(List<Vector3> points)
    {
        for (int i = points.Count - 1; i > 0; i--)
            if ((points[i] - points[i - 1]).sqrMagnitude < 1e-6f) points.RemoveAt(i);
    }

    // Buildings left without a path this rebuild. Those that got one back lose the tint.
    private void UpdateBlocked()
    {
        nowBlocked.Clear();
        doorCovered.Clear();
        for (int t = 0; t < targets.Count; t++)
        {
            if (targets[t].connected) continue;
            nowBlocked.Add(targets[t].building);
            if (targets[t].approachCount == 0) doorCovered.Add(targets[t].building);
        }

        foreach (PlacedBuilding building in blocked)
            if (building != null && !nowBlocked.Contains(building)) Restore(building);

        // Said once, when a building gets cut off, and for those already cut off on load.
        if (errorNotices != null)
        {
            foreach (PlacedBuilding building in nowBlocked)
            {
                if (blocked.Contains(building)) continue;
                string text = doorCovered.Contains(building) ? doorBlockedText : noRouteText;
                errorNotices.Show(string.Format(text, building.Definition.displayName));
            }
        }

        blocked.Clear();
        blocked.UnionWith(nowBlocked);

        // Renderers of demolished buildings or replaced art are dropped.
        spriteBuffer.Clear();
        foreach (SpriteRenderer sprite in originalColors.Keys)
            if (sprite == null) spriteBuffer.Add(sprite);
        foreach (SpriteRenderer sprite in spriteBuffer) originalColors.Remove(sprite);
    }

    // Also run after an upgrade swaps a blocked building's art, which leaves the paths as they are.
    private void TintBlocked()
    {
        foreach (PlacedBuilding building in blocked)
        {
            if (building == null) continue;
            building.GetComponentsInChildren(true, spriteBuffer);
            foreach (SpriteRenderer sprite in spriteBuffer)
            {
                if (originalColors.ContainsKey(sprite)) continue;
                originalColors[sprite] = sprite.color;
                sprite.color = sprite.color * blockedTint;
            }
        }
    }

    private void Restore(PlacedBuilding building)
    {
        building.GetComponentsInChildren(true, spriteBuffer);
        foreach (SpriteRenderer sprite in spriteBuffer)
        {
            if (!originalColors.TryGetValue(sprite, out Color color)) continue;
            sprite.color = color;
            originalColors.Remove(sprite);
        }
    }

    public bool IsBlocked(PlacedBuilding building) => blocked.Contains(building);


    private void MarkConnected(int t)
    {
        Target target = targets[t];
        target.connected = true;
        targets[t] = target;
    }

    // Only blocks whose tile changed are touched, the tilemap rebuilds just those chunks.
    private void Draw()
    {
        for (int i = 0; i < drawnBlocks.Count; i++)
        {
            int block = drawnBlocks[i];
            if (inNetworkStamp[block] == generation) continue;
            drawn[block] = NotDrawn;
            pathLayer.SetTile((Vector3Int)CellOf(block), null);
        }

        for (int i = 0; i < network.Count; i++)
        {
            int block = network[i];
            byte mask = links[block];
            if (drawn[block] == mask) continue;
            drawn[block] = mask;
            pathLayer.SetTile((Vector3Int)CellOf(block), connectTiles[mask]);
        }

        drawnBlocks.Clear();
        drawnBlocks.AddRange(network);
    }
}
