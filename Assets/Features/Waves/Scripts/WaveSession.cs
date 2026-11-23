public enum WavePhase
{
    // Guests keep bringing new requests.
    Running,

    // Time is up, no new requests are taken, started ones are finished.
    Finishing,

    Ended,

    // The guests' boat is still on its way, the wave has not begun yet.
    // Last, so phases in older saves keep their numbers.
    Arriving
}

// What brings the guests of a wave to the island. BoatManager sets a boat,
// a wave without one begins right away.
public interface IWaveArrival
{
    // The guests reached the island, the wave can begin.
    bool HasLanded { get; }
}

// What the guest requests of a wave report back. The request system implements it,
// until then a wave has no requests and simply ends when its time is up.
public interface IWaveRequests
{
    // Accepted requests still being served or waiting in a queue.
    bool HasOpenRequests { get; }

    // Every guest got everything they asked for, the wave can end early.
    bool AllGuestsServed { get; }

    // How much the building is asked to serve against its capacity, 1 is exactly full.
    float GetLoad(PlacedBuilding building);
}

// How a service building copes during a wave.
public enum ServiceState
{
    Normal,

    // Close to its capacity, it barely keeps up.
    Busy,

    // More is asked than it can serve, guests wait.
    Overloaded
}

// Rules of one wave on one island, independent of how it is shown.
public class WaveSession
{
    public string IslandId { get; }
    public int Number { get; }
    public WavePhase Phase { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public float RequestWindowSeconds { get; }

    // Set by the request system once it exists.
    public IWaveRequests Requests { get; set; }

    // Set by BoatManager while the guests are on their way. Null lets the wave begin at once.
    public IWaveArrival Arrival { get; set; }

    // Buildings sped up in this wave, by key, and the play seconds left on the running speed ups.
    private readonly System.Collections.Generic.HashSet<string> spedUp = new System.Collections.Generic.HashSet<string>();
    private readonly System.Collections.Generic.Dictionary<string, float> boosts = new System.Collections.Generic.Dictionary<string, float>();

    public System.Collections.Generic.IEnumerable<string> SpedUp => spedUp;
    public System.Collections.Generic.IReadOnlyDictionary<string, float> Boosts => boosts;

    public bool WasSpedUp(string building) => spedUp.Contains(building);
    public bool IsBoosted(string building) => boosts.ContainsKey(building);

    // Once per building per wave.
    public bool TrySpeedUp(string building, float seconds)
    {
        if (Phase == WavePhase.Ended || Phase == WavePhase.Arriving || !spedUp.Add(building)) return false;
        boosts[building] = seconds;
        return true;
    }

    // Restores speed ups from a save.
    public void RestoreSpeedUp(string building, float secondsLeft)
    {
        spedUp.Add(building);
        if (secondsLeft > 0f) boosts[building] = secondsLeft;
    }

    public bool IsAcceptingRequests => Phase == WavePhase.Running;
    public float WindowProgress => RequestWindowSeconds > 0f ? UnityEngine.Mathf.Clamp01(ElapsedSeconds / RequestWindowSeconds) : 1f;

    public WaveSession(string islandId, int number, float requestWindowSeconds)
        : this(islandId, number, requestWindowSeconds, WavePhase.Running, 0f)
    {
    }

    public WaveSession(string islandId, int number, float requestWindowSeconds, WavePhase phase, float elapsedSeconds)
    {
        IslandId = islandId;
        Number = number;
        RequestWindowSeconds = requestWindowSeconds;
        Phase = phase;
        ElapsedSeconds = elapsedSeconds;
    }

    // Moves the wave on by seconds of play time. True when the phase changed.
    public bool Advance(float seconds)
    {
        if (Phase == WavePhase.Ended) return false;

        // Nothing runs until the guests landed, not the window and not the speed ups.
        if (Phase == WavePhase.Arriving)
        {
            if (Arrival != null && !Arrival.HasLanded) return false;
            Phase = WavePhase.Running;
            return true;
        }

        WavePhase before = Phase;
        bool boostEnded = TickBoosts(seconds);
        if (Phase == WavePhase.Running)
        {
            ElapsedSeconds += seconds;
            if (Requests != null && Requests.AllGuestsServed)
                Phase = WavePhase.Ended;
            else if (ElapsedSeconds >= RequestWindowSeconds)
                Phase = WavePhase.Finishing;
        }

        if (Phase == WavePhase.Finishing && (Requests == null || !Requests.HasOpenRequests))
            Phase = WavePhase.Ended;

        return Phase != before || boostEnded;
    }

    // True when a speed up ran out.
    private bool TickBoosts(float seconds)
    {
        if (boosts.Count == 0) return false;

        bool ended = false;
        var keys = new System.Collections.Generic.List<string>(boosts.Keys);
        foreach (string key in keys)
        {
            float left = boosts[key] - seconds;
            if (left > 0f) boosts[key] = left;
            else { boosts.Remove(key); ended = true; }
        }
        return ended;
    }
}