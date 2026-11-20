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
    Left
}

// One guest of a running wave.
[Serializable]
public class GuestData
{
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

    //Seconds waited, rested or still to be served, depending on the state
    public float timer;

    //All seconds spent waiting so far, for a room and in queues, what the rating is made of
    public float waited;

    //Basic needs met so far, a room on arrival, then food and drinks when served
    public GuestNeeds met;
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
