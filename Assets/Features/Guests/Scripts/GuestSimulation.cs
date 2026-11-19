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

// Guests of one wave on one island. They arrive during the request window, wait for a room,
// then ask for services one by one and queue at the least busy building of each.
// A guest who waits too long leaves unhappy. Every guest rates the stay by how long they waited in total.
// Holds no Unity objects, so it runs in tests.
public class GuestSimulation : IWaveRequests
{
    public GuestWaveData Data { get; }

    private readonly GuestSettings settings;
    private readonly Dictionary<string, ServicePoint> points = new Dictionary<string, ServicePoint>();
    private readonly Dictionary<string, int> inService = new Dictionary<string, int>();
    private readonly Dictionary<string, int> inQueue = new Dictionary<string, int>();
    private readonly Dictionary<string, int> roomUse = new Dictionary<string, int>();
    private readonly Dictionary<string, float> loads = new Dictionary<string, float>();
    private readonly List<GuestData> queue = new List<GuestData>();
    private readonly List<BuildingCategory> pool = new List<BuildingCategory>();
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
                if (guest.state == GuestState.WaitingForRoom || guest.state == GuestState.Queued) count++;
            return count;
        }
    }

    // Guests on the island right now.
    public int InResort => Data.guests.Count;

    // Arrived guests still without a room.
    public int WaitingForRoom => waitingForRoom;

    public int GetQueued(PlacedBuilding building) => GetQueued(WaveManager.BuildingKey(building));

    // Guests in line at the building, not yet being served.
    public int GetQueued(string buildingKey) => inQueue.TryGetValue(buildingKey, out int queued) ? queued : 0;

    public bool HasOpenRequests
    {
        get
        {
            foreach (GuestData guest in Data.guests)
                if (guest.state == GuestState.WaitingForRoom || guest.state == GuestState.Queued || guest.state == GuestState.InService)
                    return true;
            return false;
        }
    }

    // Every guest has come and gone, there is nothing left for the wave to do.
    public bool AllGuestsServed => Data.arrived >= Data.totalGuests && Data.guests.Count == 0;

    public float GetLoad(PlacedBuilding building) => GetLoad(WaveManager.BuildingKey(building));

    // Guests asking for the building against its capacity, 1 is exactly full.
    public float GetLoad(string buildingKey) => loads.TryGetValue(buildingKey, out float load) ? load : 0f;

    // The wave ended, everyone still here checks out. Only guests still waiting leave unhappy.
    public void CheckOutEveryone()
    {
        foreach (GuestData guest in Data.guests)
        {
            if (guest.state == GuestState.Left) continue;
            Leave(guest, guest.state == GuestState.Resting || guest.state == GuestState.InService);
        }
        Data.guests.Clear();
        loads.Clear();
    }

    // Moves the guests on by seconds of play time. Accepting is false once the request window closed:
    // nobody new arrives and resting guests check out instead of asking for more.
    public void Tick(float seconds, bool accepting, float elapsedSeconds, float windowSeconds, IReadOnlyList<ServicePoint> current)
    {
        IndexPoints(current);
        CountUse();

        if (accepting) Arrive(elapsedSeconds, windowSeconds);
        AssignRooms(seconds);
        Serve(seconds);
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
        roomUse.Clear();
        waitingForRoom = 0;
        foreach (GuestData guest in Data.guests)
        {
            switch (guest.state)
            {
                case GuestState.WaitingForRoom: waitingForRoom++; continue;
                case GuestState.Queued: Increment(inQueue, guest.building); break;
                case GuestState.InService: Increment(inService, guest.building); break;
                case GuestState.Left: continue;
            }
            if (!string.IsNullOrEmpty(guest.room)) Increment(roomUse, guest.room);
        }
    }

    // Guests come evenly over the first part of the window, the first one right away.
    private void Arrive(float elapsedSeconds, float windowSeconds)
    {
        float span = windowSeconds * settings.arrivalShare;
        while (Data.arrived < Data.totalGuests && elapsedSeconds >= span * Data.arrived / Data.totalGuests)
        {
            Data.guests.Add(new GuestData { ticket = Data.nextTicket++, state = GuestState.WaitingForRoom });
            Data.arrived++;
        }
    }

    // First come, first housed. A guest without a room waits until someone checks out.
    private void AssignRooms(float seconds)
    {
        foreach (GuestData guest in Data.guests)
        {
            if (guest.state != GuestState.WaitingForRoom) continue;

            string room = FindFreeRoom();
            if (room != null)
            {
                Increment(roomUse, room);
                waitingForRoom--;
                guest.room = room;
                guest.state = GuestState.Resting;
                guest.timer = BreakSeconds();
                MakeRequests(guest);
                continue;
            }

            guest.timer += seconds;
            guest.waited += seconds;
            if (guest.timer >= settings.roomPatienceSeconds) Leave(guest, false);
        }
    }

    private string FindFreeRoom()
    {
        foreach (ServicePoint point in points.Values)
        {
            if (point.category != BuildingCategory.Rooms) continue;
            roomUse.TryGetValue(point.key, out int used);
            if (used < point.capacity) return point.key;
        }
        return null;
    }

    // Finishes service in progress, then lets queued guests in by the order they joined.
    private void Serve(float seconds)
    {
        queue.Clear();
        foreach (GuestData guest in Data.guests)
        {
            if (guest.state == GuestState.InService)
            {
                if (!points.TryGetValue(guest.building, out ServicePoint point)) { Reroute(guest); continue; }

                guest.timer -= seconds * point.speed;
                if (guest.timer > 0f) continue;

                inService[guest.building]--;
                Data.servedRequests++;
                guest.building = null;
                guest.state = GuestState.Resting;
                guest.timer = BreakSeconds();
            }
            else if (guest.state == GuestState.Queued)
            {
                queue.Add(guest);
            }
        }

        queue.Sort((a, b) => a.ticket.CompareTo(b.ticket));
        foreach (GuestData guest in queue)
        {
            if (!points.TryGetValue(guest.building, out ServicePoint point)) { Reroute(guest); continue; }

            inService.TryGetValue(guest.building, out int busy);
            if (busy < point.capacity)
            {
                inService[guest.building] = busy + 1;
                inQueue[guest.building]--;
                guest.state = GuestState.InService;
                guest.timer = settings.serviceSeconds;
                continue;
            }

            guest.timer += seconds;
            guest.waited += seconds;
            if (guest.timer >= settings.queuePatienceSeconds) Leave(guest, false);
        }
    }

    // Rested guests ask for their next service, or check out when there is none or the window closed.
    private void Rest(float seconds, bool accepting)
    {
        foreach (GuestData guest in Data.guests)
        {
            if (guest.state != GuestState.Resting) continue;

            guest.timer -= seconds;
            while (guest.state == GuestState.Resting && guest.timer <= 0f)
            {
                if (!accepting || guest.requests.Count == 0) { Leave(guest, true); break; }

                BuildingCategory category = guest.requests[0];
                guest.requests.RemoveAt(0);

                // A service the island lost is skipped, the guest asks for the next one.
                string building = PickBuilding(category);
                if (building == null) continue;

                Increment(inQueue, building);
                guest.current = category;
                guest.building = building;
                guest.state = GuestState.Queued;
                guest.ticket = Data.nextTicket++;
                guest.timer = 0f;
            }
        }
    }

    // The building of the service with the shortest wait for one more guest.
    private string PickBuilding(BuildingCategory category)
    {
        string best = null;
        float bestLoad = float.MaxValue;
        foreach (ServicePoint point in points.Values)
        {
            if (point.category != category) continue;

            inService.TryGetValue(point.key, out int busy);
            inQueue.TryGetValue(point.key, out int queued);
            float load = point.capacity > 0 ? (busy + queued + 1f) / point.capacity : float.MaxValue / 2f;
            if (load < bestLoad) { bestLoad = load; best = point.key; }
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
    }

    // Checks the guest out and takes their rating: stars lost to all their waiting, 1 star if they gave up.
    private void Leave(GuestData guest, bool happy)
    {
        if (happy) Data.happyGuests++;
        else Data.unhappyGuests++;

        float stars = happy ? settings.RatingAfterWaiting(guest.waited) : 1f;
        Data.ratingSum += stars;
        Data.ratedGuests++;
        Data.reputationChange += settings.ReputationFor(stars);

        guest.state = GuestState.Left;
        guest.building = null;
    }

    // Food and drinks always, then more picks from what the island offers, in a random order.
    private void MakeRequests(GuestData guest)
    {
        pool.Clear();
        pool.Add(BuildingCategory.Food);
        pool.Add(BuildingCategory.Drinks);
        if (Offers(BuildingCategory.Activities)) pool.Add(BuildingCategory.Activities);

        int count = Range(settings.minRequests, settings.maxRequests);
        guest.requests.Clear();
        guest.requests.Add(BuildingCategory.Food);
        guest.requests.Add(BuildingCategory.Drinks);
        while (guest.requests.Count < count) guest.requests.Add(pool[Range(0, pool.Count - 1)]);

        for (int i = guest.requests.Count - 1; i > 0; i--)
        {
            int j = Range(0, i);
            (guest.requests[i], guest.requests[j]) = (guest.requests[j], guest.requests[i]);
        }
        if (guest.requests.Count > count) guest.requests.RemoveRange(count, guest.requests.Count - count);
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

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out int count);
        counts[key] = count + 1;
    }
}
