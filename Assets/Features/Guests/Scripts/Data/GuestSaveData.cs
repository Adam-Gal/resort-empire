using System;
using System.Collections.Generic;

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

    //Served requests already paid for, the rest is paid on the next frame
    public int paidRequests;

    public int happyGuests;
    public int unhappyGuests;
    public List<GuestData> guests = new List<GuestData>();
}

// Guests of all running waves, written to application.persistentDataPath as json.
[Serializable]
public class GuestSaveData
{
    public int version = 1;
    public List<GuestWaveData> waves = new List<GuestWaveData>();
}
