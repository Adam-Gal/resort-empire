using System;
using UnityEngine;

public enum BoatState
{
    // Sailing in from the open sea.
    Arriving,

    // At the dock while the wave runs.
    Docked,

    // The wave ended, sailing away along the way out.
    Leaving,

    // Out of sight, ready to be removed.
    Gone
}

// How boats move. Every boat shares one, so they all sail at the same speed.
[Serializable]
public class BoatMotion
{
    //World units per second at full speed
    [Min(.1f)] public float speed = 2f;

    //Stretch before the dock over which a boat slows down, and after leaving it over which it speeds up again
    [Min(0f)] public float easeDistance = 1.5f;

    //Slowest it gets while docking or setting off, as a part of the full speed
    [Range(.05f, 1f)] public float slowestShare = .25f;

    //Stretch at the far ends of the routes over which a boat fades in when it comes and out when it goes
    [Min(0f)] public float fadeDistance = 1f;

    //Seconds an old boat takes to fade away when the next wave's boat to the same island replaces it
    [Min(.05f)] public float vanishSeconds = .5f;

    // Part of the full speed with this much room left to slow down in, or this far gone to speed up over.
    public float Ease(float room) =>
        easeDistance > 0f ? Mathf.Lerp(slowestShare, 1f, Mathf.Clamp01(room / easeDistance)) : 1f;
}

// One boat bringing the guests of a wave to an island and taking them away again.
// It sails the way in from the open sea to the dock, waits there for the whole wave,
// then sails off along the way out. Holds no Unity objects, so it runs in tests.
public class Boat : IWaveArrival
{
    public string IslandId { get; }
    public int WaveNumber { get; }
    public BoatState State { get; private set; }
    public Vector3 Position { get; private set; }

    // Way the bow points, length 1.
    public Vector3 Facing { get; private set; }

    // Under way, so it leaves a wake.
    public bool Moving { get; private set; }

    // Guests may step off, the wave can begin.
    public bool HasLanded { get; private set; }

    // How much of the way in is done, 1 once docked. For a "boat on its way" bar.
    public float ArrivalProgress => State == BoatState.Arriving && wayIn.Length > 0f ? distance / wayIn.Length : 1f;

    private readonly BoatRoute wayIn;
    private readonly BoatRoute wayOut;
    private readonly BoatMotion motion;

    // How far along the route of the current state, the way in until the boat leaves.
    private float distance;
    private float vanish = 1f;
    private bool vanishing;

    // Way out starts at the dock. Docked: a wave that already began, loaded from the save, finds its boat at the dock.
    public Boat(string islandId, int waveNumber, BoatRoute wayIn, BoatRoute wayOut, BoatMotion motion, bool docked)
    {
        IslandId = islandId;
        WaveNumber = waveNumber;
        this.wayIn = wayIn;
        this.wayOut = wayOut;
        this.motion = motion;
        distance = docked ? wayIn.Length : 0f;
        State = docked ? BoatState.Docked : BoatState.Arriving;
        HasLanded = docked;
        Follow(wayIn);
    }

    // Faded in at the start of the way in and out at the end of the way out, 0 to 1.
    public float Alpha
    {
        get
        {
            float alpha = State == BoatState.Leaving || State == BoatState.Gone
                ? Fade(wayOut, wayOut.Length - distance)
                : Fade(wayIn, distance);
            return vanishing ? Mathf.Min(alpha, vanish) : alpha;
        }
    }

    // Lets the guests step off. BoatManager calls it once no boat is still on its way in.
    public void Land()
    {
        if (State == BoatState.Docked) HasLanded = true;
    }

    // The wave is over: sail off along the way out. A boat still coming in only fades away.
    public void Depart()
    {
        if (State == BoatState.Arriving) Vanish();
        if (State != BoatState.Docked) return;
        State = BoatState.Leaving;
        distance = 0f;
        Follow(wayOut);
    }

    // Fades out quickly wherever it is, to make room for the island's next boat.
    public void Vanish() => vanishing = true;

    // Moves the boat on by seconds of play time.
    public void Tick(float seconds)
    {
        if (State == BoatState.Gone) return;
        Moving = State == BoatState.Arriving || State == BoatState.Leaving;

        switch (State)
        {
            case BoatState.Arriving:
                distance = Mathf.Min(wayIn.Length, distance + seconds * motion.speed * motion.Ease(wayIn.Length - distance));
                Follow(wayIn);
                if (distance >= wayIn.Length) State = BoatState.Docked;
                break;

            case BoatState.Leaving:
                distance = Mathf.Min(wayOut.Length, distance + seconds * motion.speed * motion.Ease(distance));
                Follow(wayOut);
                if (distance >= wayOut.Length) State = BoatState.Gone;
                break;
        }

        if (vanishing)
        {
            vanish -= seconds / motion.vanishSeconds;
            if (vanish <= 0f) State = BoatState.Gone;
        }
    }

    private float Fade(BoatRoute route, float fromEnd)
    {
        float fade = Mathf.Min(motion.fadeDistance, route.Length * .5f);
        return fade > 0f ? Mathf.Clamp01(fromEnd / fade) : 1f;
    }

    // On the route at the current distance, the bow along it.
    private void Follow(BoatRoute route)
    {
        Position = route.PointAt(distance, out Vector3 ahead);
        Facing = ahead;
    }
}