using System;
using System.Collections.Generic;

[Serializable]
public class WaveSessionSaveData
{
    public string islandId;
    public int number;
    public WavePhase phase;

    //Only time spent playing counts, the wave is paused while the game is closed
    public float elapsedSeconds;
    public float requestWindowSeconds;

    //Buildings already sped up in this wave, and the seconds left on the running ones
    public List<string> spedUpBuildings = new List<string>();
    public List<string> boostBuildings = new List<string>();
    public List<float> boostSeconds = new List<float>();
}

// Waves running on the islands, written to application.persistentDataPath as json.
[Serializable]
public class WaveSaveData
{
    public int version = 1;
    public List<WaveSessionSaveData> sessions = new List<WaveSessionSaveData>();

    //Waves started so far on each island, used to number them
    public List<string> countIslands = new List<string>();
    public List<int> counts = new List<int>();
}
