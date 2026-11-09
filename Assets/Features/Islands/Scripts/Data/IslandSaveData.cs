using System;
using System.Collections.Generic;

// Which islands are open and which bridges are repaired.
// Written to application.persistentDataPath as json.
[Serializable]
public class IslandSaveData
{
    public int version = 1;

    //Once open an island stays open, even if the requirement changes later
    public List<string> unlockedIslands = new List<string>();
    public List<string> repairedBridges = new List<string>();

    //Islands whose unlock dialog already popped up on its own, later only a tap brings it back
    public List<string> offeredIslands = new List<string>();
}
