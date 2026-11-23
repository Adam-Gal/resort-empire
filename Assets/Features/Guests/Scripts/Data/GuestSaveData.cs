using System;
using System.Collections.Generic;

// The three basic needs every guest has. A guest can have any of them met.
[Flags]
public enum GuestNeeds
{
    None = 0,
    Accommodation = 1,
    Food = 2,
    Drinks = 4,
    All = Accommodation | Food | Drinks
}

public static class GuestNeedsInfo
{
    public static readonly GuestNeeds[] Basic = { GuestNeeds.Accommodation, GuestNeeds.Food, GuestNeeds.Drinks };

    // The need a service meets, None for extras like activities.
    public static GuestNeeds For(BuildingCategory category)
    {
        switch (category)
        {
            case BuildingCategory.Rooms: return GuestNeeds.Accommodation;
            case BuildingCategory.Food: return GuestNeeds.Food;
            case BuildingCategory.Drinks: return GuestNeeds.Drinks;
            default: return GuestNeeds.None;
        }
    }

    public static string DisplayName(GuestNeeds need)
    {
        switch (need)
        {
            case GuestNeeds.Accommodation: return "Ubytovanie";
            case GuestNeeds.Food: return "Jedlo";
            case GuestNeeds.Drinks: return "Nápoje";
            default: return need.ToString();
        }
    }

    // How many of the basic needs are missing.
    public static int CountMissing(GuestNeeds met)
    {
        int missing = 0;
        foreach (GuestNeeds need in Basic)
            if ((met & need) == 0) missing++;
        return missing;
    }
}

public enum GuestState
{
    // Arrived, no room was free yet.
    WaitingForRoom,

    // In their room between two requests.
    Resting,

    // Waiting at a building for a free spot.
    Queued,

    // Being served at a building.
    InService,

    // Checked out, happy or not.
    Left,

    // On the way to their room, from the harbor or after a service.
    GoingToRoom,

    // On the way to a service building, they join its queue on arrival.
    GoingToService,

    // Checked out and rated, walking back to the harbor.
    Leaving
}

// One party of a running wave, a single guest or a couple. A couple counts as two guests:
// two beds, two places at a service and two ratings.
[Serializable]
public class GuestData
{
    //Never reused within a wave, so the party keeps its look on screen
    public int id;

    //Guests in the party, 1 or 2. Saves from before couples have 0, read as 1
    public int size = 1;

    //Which figure is drawn for a single guest, 0 a man, 1 a woman
    public int look;

    //Order of arrival, then order of joining the current queue
    public int ticket;
    public GuestState state;

    //Building key of the room, empty until one was free
    public string room;

    //Building key of the service they wait for or are served at
    public string building;
    public BuildingCategory current;

    //Services still to ask for, in order
    public List<BuildingCategory> requests = new List<BuildingCategory>();

    //Seconds waited, rested, still to be served or still to walk, depending on the state
    public float timer;

    //Where the current walk started, a building key or the harbor, and how long the whole walk takes
    public string from;
    public float walkSeconds;

    //All seconds spent waiting so far, for a room and in queues, what the rating is made of
    public float waited;

    //Basic needs met so far, a room on arrival, then food and drinks when served
    public GuestNeeds met;

    public int People => size > 1 ? size : 1;
}

// Guests of the wave running on one island.
[Serializable]
public class GuestWaveData
{
    public string islandId;
    public int waveNumber;
    public int totalGuests;
    public int arrived;
    public int nextTicket;
    public int nextId;

    //State of the random numbers, so a loaded wave goes on the same way
    public uint random;

    public int servedRequests;

    //Guests who got a room, were served food and were served drinks, each counted once
    public int housedGuests;
    public int fedGuests;
    public int refreshedGuests;

    //Served requests already paid for, the rest is paid on the next frame
    public int paidRequests;

    public int happyGuests;
    public int unhappyGuests;

    //Stars of every guest who left, summed, and how many rated
    public float ratingSum;
    public int ratedGuests;

    //Reputation the ratings earned so far, given when the wave ends
    public int reputationChange;

    public List<GuestData> guests = new List<GuestData>();
}

// Guests of all running waves, written to application.persistentDataPath as json.
[Serializable]
public class GuestSaveData
{
    public int version = 1;
    public List<GuestWaveData> waves = new List<GuestWaveData>();
}
