using System.Collections.Generic;

// A building guests can use during a wave, as the simulation sees it.
public struct ServicePoint
{
    public string key;
    public BuildingCategory category;

    //Guests served at once, beds for rooms
    public int capacity;

    //How many times faster it serves, above 1 while sped up
    public float speed;

    public ServicePoint(string key, BuildingCategory category, int capacity, float speed)
    {
        this.key = key;
        this.category = category;
        this.capacity = capacity;
        this.speed = speed;
    }
}

// How far guests walk between two places of an island, along its paths.
// Places are building keys or GuestSimulation.HarborKey.
public interface IGuestRoutes
{
    // World units from one place to the other, 0 when they are the same.
    float Distance(string from, string to);
}

// Guests of one wave on one island. They arrive at the harbor during the request window and wait there for a room,
// then ask for services one by one and queue at the least busy building of each.
// Guests come alone or as couples. A couple is one party of two: it takes two beds and two places at a service.
// Every move between the harbor, the room and a service is walked along the paths and takes as long as it does
// on screen, so the layout of the buildings matters. Walking is not waiting: waiting starts once a guest stands in a queue.
// Every guest has three basic needs: a room, food and drinks. Once the window closes nobody starts anything new:
// guests resting or still waiting for a room leave, those already on their way to a service, in line or being served
// finish it and then walk to the harbor. The wave is over once the last guest got there.
// A guest who waits too long leaves unhappy. Every guest rates the stay by how long they waited in total
// and by the basic needs left unmet.
// Holds no Unity objects, so it runs in tests.
public class GuestSimulation : IWaveRequests
{
    // Where guests arrive and leave, as a place for IGuestRoutes.
    public const string HarborKey = "harbor";

    public GuestWaveData Data { get; }

    private readonly GuestSettings settings;
    private readonly Dictionary<string, ServicePoint> points = new Dictionary<string, ServicePoint>();
    private readonly Dictionary<string, int> inService = new Dictionary<string, int>();
    private readonly Dictionary<string, int> inQueue = new Dictionary<string, int>();
    private readonly Dictionary<string, int> inbound = new Dictionary<string, int>();
    private readonly Dictionary<string, int> roomUse = new Dictionary<string, int>();
    private readonly Dictionary<string, float> loads = new Dictionary<string, float>();
    private readonly HashSet<string> queueStuck = new HashSet<string>();
    private readonly List<GuestData> queue = new List<GuestData>();
    private IGuestRoutes routes;
    private int roomCapacity;
    private int waitingForRoom;

    public GuestSimulation(GuestSettings settings, GuestWaveData data)
    {
        this.settings = settings;
        Data = data;
        if (Data.random == 0) Data.random = 2463534242;
    }

    public static GuestSimulation Create(GuestSettings settings, string islandId, int waveNumber, uint seed)
    {
        var data = new GuestWaveData
        {
            islandId = islandId,
            waveNumber = waveNumber,
            totalGuests = settings.GuestsForWave(waveNumber),
            random = seed
        };
        return new GuestSimulation(settings, data);
    }

    public int TotalGuests => Data.totalGuests;
    public int Arrived => Data.arrived;
    public int StillToCome => Data.totalGuests - Data.arrived;
    public int HappyGuests => Data.happyGuests;
    public int UnhappyGuests => Data.unhappyGuests;
    public int ServedRequests => Data.servedRequests;

    // Parties on the island, for drawing them. Couples are one entry.
    public IReadOnlyList<GuestData> Parties => Data.guests;

    // Guests who had the basic need met, counted once each.
    public int GetMetCount(GuestNeeds need)
    {
        switch (need)
        {
            case GuestNeeds.Accommodation: return Data.housedGuests;
            case GuestNeeds.Food: return Data.fedGuests;
            case GuestNeeds.Drinks: return Data.refreshedGuests;
            default: return 0;
        }
    }

    // Average stars of the guests who left so far, 0 before anyone did.
    public float AverageRating => Data.ratedGuests > 0 ? Data.ratingSum / Data.ratedGuests : 0f;
    public int RatedGuests => Data.ratedGuests;

    // Reputation the ratings so far are worth, can be negative.
    public int ReputationChange => Data.reputationChange;

    // Guests waiting for a room or in a queue.
    public int Waiting
    {
        get
        {
            int count = 0;
            foreach (GuestData guest in Data.guests)
                if (guest.state == GuestState.WaitingForRoom || guest.state == GuestState.Queued) count += guest.People;
            return count;
        }
    }

    // Guests on the island right now, those walking back to the harbor included.
    public int InResort
    {
        get
        {
            int count = 0;
            foreach (GuestData guest in Data.guests) count += guest.People;
            return count;
        }
    }

    // Arrived guests still without a room.
    public int WaitingForRoom => waitingForRoom;

    public int GetQueued(PlacedBuilding building) => GetQueued(WaveManager.BuildingKey(building));

    // Guests in line at the building, not yet being served.
    public int GetQueued(string buildingKey) => inQueue.TryGetValue(buildingKey, out int queued) ? queued : 0;

    // Everyone still on the island keeps the wave going, also those walking out to the harbor.
    public bool HasOpenRequests
    {
        get
        {
            foreach (GuestData guest in Data.guests)
                if (guest.state != GuestState.Left) return true;
            return false;
        }
    }

    // Every guest has come and gone, there is nothing left for the wave to do.
    public bool AllGuestsServed => Data.arrived >= Data.totalGuests && Data.guests.Count == 0;

    public float GetLoad(PlacedBuilding building) => GetLoad(WaveManager.BuildingKey(building));

    // Guests asking for the building against its capacity, 1 is exactly full.
    // Guests still walking there do not count, only those who are there.
    public float GetLoad(string buildingKey) => loads.TryGetValue(buildingKey, out float load) ? load : 0f;

    // The wave ended, everyone still here checks out. Only guests still waiting leave unhappy.
    // Guests already walking out were rated when they left.
    public void CheckOutEveryone()
    {
        foreach (GuestData guest in Data.guests)
        {
            if (guest.state == GuestState.Left || guest.state == GuestState.Leaving) continue;
            bool waiting = guest.state == GuestState.WaitingForRoom || guest.state == GuestState.Queued;
            Rate(guest, !waiting);
        }
        Data.guests.Clear();
        loads.Clear();
    }

    // Moves the guests on by seconds of play time. Accepting is false once the request window closed:
    // nobody new arrives and resting guests check out instead of asking for more.
    // Without routes every walk is instant.
    public void Tick(float seconds, bool accepting, float elapsedSeconds, float windowSeconds,
        IReadOnlyList<ServicePoint> current, IGuestRoutes routes = null)
    {
        this.routes = routes;
        IndexPoints(current);
        CountUse();

        if (accepting) Arrive(elapsedSeconds, windowSeconds);
        AssignRooms(seconds, accepting);
        Walk(seconds);
        Serve(seconds, accepting);
        Rest(seconds, accepting);

        Data.guests.RemoveAll(guest => guest.state == GuestState.Left);
        CountUse();
        UpdateLoads();
    }

    private void IndexPoints(IReadOnlyList<ServicePoint> current)
    {
        points.Clear();
        roomCapacity = 0;
        foreach (ServicePoint point in current)
        {
            points[point.key] = point;
            if (point.category == BuildingCategory.Rooms) roomCapacity += point.capacity;
        }
    }

    private void CountUse()
    {
        inService.Clear();
        inQueue.Clear();
        inbound.Clear();
        roomUse.Clear();
        waitingForRoom = 0;
        foreach (GuestData guest in Data.guests)
        {
            int people = guest.People;
            switch (guest.state)
            {
                case GuestState.WaitingForRoom: waitingForRoom += people; continue;
                case GuestState.Queued: Add(inQueue, guest.building, people); break;
                case GuestState.InService: Add(inService, guest.building, people); break;
                case GuestState.GoingToService: Add(inbound, guest.building, people); break;
                case GuestState.Left:
                case GuestState.Leaving: continue;
            }
            if (!string.IsNullOrEmpty(guest.room)) Add(roomUse, guest.room, people);
        }
    }

    // Guests come by boat: the boats land evenly over the first part of the window, the first one right away,
    // and each boat's guests step off one after another, so they leave the harbor almost together.
    // Some come as couples.
    private void Arrive(float elapsedSeconds, float windowSeconds)
    {
        float span = windowSeconds * settings.arrivalShare;
        while (Data.arrived < Data.totalGuests && elapsedSeconds >= ArrivalTime(Data.arrived, span))
        {
            int size = Data.totalGuests - Data.arrived >= 2 && Next01() < settings.coupleChance ? 2 : 1;
            var guest = new GuestData
            {
                id = Data.nextId++,
                size = size,
                look = Next01() < .5f ? 0 : 1,
                ticket = Data.nextTicket++,
                state = GuestState.WaitingForRoom,
                from = HarborKey
            };
            Data.guests.Add(guest);
            Data.arrived += size;
            waitingForRoom += size;
        }
    }

    // When the guest with this place in the wave steps off their boat.
    private float ArrivalTime(int guest, float span)
    {
        int boats = System.Math.Max(1, System.Math.Min(settings.boatsPerWave, Data.totalGuests));
        int boat = (int)((long)guest * boats / Data.totalGuests);
        int firstOnBoat = (int)(((long)boat * Data.totalGuests + boats - 1) / boats);
        return span * boat / boats + (guest - firstOnBoat) * settings.disembarkSeconds;
    }

    // First come, first housed, a couple needs two beds in one building. A party without a room waits at the
    // harbor until someone checks out. With a room they walk there.
    private void AssignRooms(float seconds, bool accepting)
    {
        foreach (GuestData guest in Data.guests)
        {
            if (guest.state != GuestState.WaitingForRoom) continue;

            // Too late to start a stay, they take the boat back.
            if (!accepting) { Leave(guest, false); continue; }

            string room = FindFreeRoom(guest.People);
            if (room != null)
            {
                Add(roomUse, room, guest.People);
                waitingForRoom -= guest.People;
                guest.room = room;
                Meet(guest, GuestNeeds.Accommodation);
                MakeRequests(guest);
                StartWalk(guest, HarborKey, GuestState.GoingToRoom);
                continue;
            }

            guest.timer += seconds;
            guest.waited += seconds;
            if (guest.timer >= settings.roomPatienceSeconds) Leave(guest, false);
        }
    }

    // A building with enough free beds for the party. Only when no building is big enough for a couple
    // does an empty, smaller one take them, so a couple never waits for good.
    private string FindFreeRoom(int people)
    {
        // Any building with room for the party, picked by chance weighted by its free beds,
        // so a bungalow gets guests from the start alongside a big hotel.
        int freeBeds = 0;
        string squeeze = null;
        bool anyFits = false;
        foreach (ServicePoint point in points.Values)
        {
            if (point.category != BuildingCategory.Rooms || point.capacity <= 0) continue;
            roomUse.TryGetValue(point.key, out int used);
            if (point.capacity >= people) anyFits = true;
            if (used + people <= point.capacity) freeBeds += point.capacity - used;
            else if (point.capacity < people && used == 0 && squeeze == null) squeeze = point.key;
        }

        if (freeBeds > 0)
        {
            float pick = Next01() * freeBeds;
            string last = null;
            foreach (ServicePoint point in points.Values)
            {
                if (point.category != BuildingCategory.Rooms || point.capacity <= 0) continue;
                roomUse.TryGetValue(point.key, out int used);
                if (used + people > point.capacity) continue;
                last = point.key;
                pick -= point.capacity - used;
                if (pick < 0f) return point.key;
            }
            return last;
        }
        return anyFits ? null : squeeze;
    }

    // Walkers get closer to where they are going. Arrived ones join the queue, rest in their room or are gone.
    private void Walk(float seconds)
    {
        foreach (GuestData guest in Data.guests)
        {
            if (guest.state != GuestState.GoingToRoom && guest.state != GuestState.GoingToService && guest.state != GuestState.Leaving)
                continue;

            guest.timer -= seconds;
            if (guest.timer <= 0f) Reach(guest);
        }
    }

    // Sets the party walking from a place to where the state leads: their room, the service building or the harbor.
    // A walk of no length ends right away.
    private void StartWalk(GuestData guest, string from, GuestState state)
    {
        guest.state = state;
        guest.from = from;
        guest.walkSeconds = WalkSeconds(from, Destination(guest));
        guest.timer = guest.walkSeconds;
        if (guest.timer <= 0f) Reach(guest);
    }

    // Where a walking party is heading.
    public static string Destination(GuestData guest)
    {
        switch (guest.state)
        {
            case GuestState.GoingToRoom: return guest.room;
            case GuestState.GoingToService: return guest.building;
            case GuestState.Leaving: return HarborKey;
            default: return null;
        }
    }

    private void Reach(GuestData guest)
    {
        guest.walkSeconds = 0f;
        switch (guest.state)
        {
            case GuestState.GoingToRoom:
                guest.state = GuestState.Resting;
                guest.timer = BreakSeconds();
                break;

            case GuestState.GoingToService:
                // The building went while they walked, they ask for the same service again.
                if (!points.ContainsKey(guest.building)) { Reroute(guest); break; }

                // In line by when they got there, waiting starts now.
                Add(inQueue, guest.building, guest.People);
                guest.state = GuestState.Queued;
                guest.ticket = Data.nextTicket++;
                guest.timer = 0f;
                break;

            case GuestState.Leaving:
                guest.state = GuestState.Left;
                break;
        }
    }

    private float WalkSeconds(string from, string to)
    {
        if (routes == null || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to) return 0f;
        float distance = routes.Distance(from, to);
        return distance > 0f ? distance / settings.walkSpeed : 0f;
    }

    // Finishes service in progress, then lets queued guests in by the order they got there.
    // Nobody passes the party at the head of a line, even when a single would fit where a couple does not.
    private void Serve(float seconds, bool accepting)
    {
        queue.Clear();
        foreach (GuestData guest in Data.guests)
        {
            if (guest.state == GuestState.InService)
            {
                if (!points.TryGetValue(guest.building, out ServicePoint point)) { Reroute(guest); continue; }

                guest.timer -= seconds * point.speed;
                if (guest.timer > 0f) continue;

                Add(inService, guest.building, -guest.People);
                Data.servedRequests += guest.People;
                Meet(guest, GuestNeedsInfo.For(guest.current));

                // After the window this was the last thing they do, they head for the harbor from here.
                if (!accepting) { Leave(guest, true); continue; }

                string served = guest.building;
                guest.building = null;
                StartWalk(guest, served, GuestState.GoingToRoom);
            }
            else if (guest.state == GuestState.Queued)
            {
                queue.Add(guest);
            }
        }

        queue.Sort((a, b) => a.ticket.CompareTo(b.ticket));
        queueStuck.Clear();
        foreach (GuestData guest in queue)
        {
            if (!points.TryGetValue(guest.building, out ServicePoint point)) { Reroute(guest); continue; }

            inService.TryGetValue(guest.building, out int busy);
            bool fits = busy + guest.People <= point.capacity || (busy == 0 && guest.People > point.capacity);
            if (fits && !queueStuck.Contains(guest.building))
            {
                Add(inService, guest.building, guest.People);
                Add(inQueue, guest.building, -guest.People);
                guest.state = GuestState.InService;
                guest.timer = settings.serviceSeconds;
                continue;
            }

            queueStuck.Add(guest.building);
            guest.timer += seconds;
            guest.waited += seconds;
            if (guest.timer >= settings.queuePatienceSeconds) Leave(guest, false);
        }
    }

    // Rested guests set off for their next service, or check out when there is none.
    // After the window closed they ask for nothing more and check out.
    private void Rest(float seconds, bool accepting)
    {
        foreach (GuestData guest in Data.guests)
        {
            if (guest.state != GuestState.Resting) continue;

            // The window closed: no more requests, they check out right away.
            if (!accepting) { Leave(guest, true); continue; }

            guest.timer -= seconds;
            while (guest.state == GuestState.Resting && guest.timer <= 0f)
            {
                if (guest.requests.Count == 0) { Leave(guest, true); break; }

                BuildingCategory category = guest.requests[0];
                guest.requests.RemoveAt(0);

                // A service the island lost is skipped, the guest asks for the next one.
                string building = PickBuilding(category, guest);
                if (building == null) continue;

                Add(inbound, building, guest.People);
                guest.current = category;
                guest.building = building;
                StartWalk(guest, guest.room, GuestState.GoingToService);
            }
        }
    }

    // The building of the service with the shortest wait for the party, guests on their way there counted.
    // Equally busy buildings go to the nearest.
    private string PickBuilding(BuildingCategory category, GuestData guest)
    {
        string best = null;
        float bestLoad = float.MaxValue;
        float bestWalk = float.MaxValue;
        foreach (ServicePoint point in points.Values)
        {
            if (point.category != category) continue;

            inService.TryGetValue(point.key, out int busy);
            inQueue.TryGetValue(point.key, out int queued);
            inbound.TryGetValue(point.key, out int coming);
            float load = point.capacity > 0 ? (busy + queued + coming + guest.People) / (float)point.capacity : float.MaxValue / 2f;
            float walk = WalkSeconds(guest.room, point.key);
            if (load < bestLoad - .0001f || (load < bestLoad + .0001f && walk < bestWalk))
            {
                bestLoad = load;
                bestWalk = walk;
                best = point.key;
            }
        }
        return best;
    }

    // The building was removed, the guest asks for the same service again elsewhere.
    private void Reroute(GuestData guest)
    {
        guest.requests.Insert(0, guest.current);
        guest.building = null;
        guest.state = GuestState.Resting;
        guest.timer = 0f;
        guest.walkSeconds = 0f;
    }

    private void Meet(GuestData guest, GuestNeeds need)
    {
        if (need == GuestNeeds.None || (guest.met & need) != 0) return;

        guest.met |= need;
        switch (need)
        {
            case GuestNeeds.Accommodation: Data.housedGuests += guest.People; break;
            case GuestNeeds.Food: Data.fedGuests += guest.People; break;
            case GuestNeeds.Drinks: Data.refreshedGuests += guest.People; break;
        }
    }

    // Checks the party out and takes their rating, then they walk back to the harbor
    // from their room, or from the building they queued or were served at.
    // Guests who never got a room are at the harbor already.
    private void Leave(GuestData guest, bool happy)
    {
        string at = guest.state == GuestState.Queued || guest.state == GuestState.InService ? guest.building : guest.room;
        bool atHarbor = guest.state == GuestState.WaitingForRoom;
        Rate(guest, happy);

        guest.room = null;
        guest.building = null;
        if (atHarbor) guest.state = GuestState.Left;
        else StartWalk(guest, at, GuestState.Leaving);
    }

    // Stars lost to all their waiting and to unmet needs, 1 star if they gave up. Each guest of a couple rates.
    private void Rate(GuestData guest, bool happy)
    {
        int people = guest.People;
        if (happy) Data.happyGuests += people;
        else Data.unhappyGuests += people;

        float stars = happy ? settings.RatingFor(guest.waited, GuestNeedsInfo.CountMissing(guest.met)) : 1f;
        Data.ratingSum += stars * people;
        Data.ratedGuests += people;
        Data.reputationChange += settings.ReputationFor(stars) * people;

        guest.state = GuestState.Left;
    }

    // Optional services in the order they are rolled for, so a loaded wave rolls the same way.
    private static readonly BuildingCategory[] OptionalServices =
    {
        BuildingCategory.Pool, BuildingCategory.Spa, BuildingCategory.Tennis, BuildingCategory.Activities
    };

    // Food and drinks always, then each optional service the island offers by chance, in a random order.
    private void MakeRequests(GuestData guest)
    {
        guest.requests.Clear();
        guest.requests.Add(BuildingCategory.Food);
        guest.requests.Add(BuildingCategory.Drinks);
        foreach (BuildingCategory service in OptionalServices)
        {
            if (Offers(service) && Next01() < settings.optionalServiceChance) guest.requests.Add(service);
        }

        for (int i = guest.requests.Count - 1; i > 0; i--)
        {
            int j = Range(0, i);
            (guest.requests[i], guest.requests[j]) = (guest.requests[j], guest.requests[i]);
        }
    }

    private bool Offers(BuildingCategory category)
    {
        foreach (ServicePoint point in points.Values)
            if (point.category == category && point.capacity > 0) return true;
        return false;
    }

    private void UpdateLoads()
    {
        loads.Clear();
        foreach (ServicePoint point in points.Values)
        {
            int count;
            float extra = 0f;
            if (point.category == BuildingCategory.Rooms)
            {
                roomUse.TryGetValue(point.key, out count);
                // Guests without a room count against every room, by its share of the beds.
                if (roomCapacity > 0) extra = waitingForRoom * (float)point.capacity / roomCapacity;
            }
            else
            {
                inService.TryGetValue(point.key, out int busy);
                inQueue.TryGetValue(point.key, out int queued);
                count = busy + queued;
            }

            float asked = count + extra;
            loads[point.key] = point.capacity > 0 ? asked / point.capacity : (asked > 0f ? 2f : 0f);
        }
    }

    private float BreakSeconds() =>
        settings.minBreakSeconds + Next01() * (settings.maxBreakSeconds - settings.minBreakSeconds);

    // Whole number from min to max, both included.
    private int Range(int min, int max) => min + (int)(Next01() * (max - min + 1)) % (max - min + 1);

    // Xorshift, small and its whole state fits in the save.
    private float Next01()
    {
        uint x = Data.random;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        Data.random = x;
        return (x & 0xFFFFFF) / (float)0x1000000;
    }

    private static void Add(Dictionary<string, int> counts, string key, int amount)
    {
        counts.TryGetValue(key, out int count);
        counts[key] = count + amount;
    }
}
