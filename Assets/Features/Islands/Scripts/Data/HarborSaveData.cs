using System;
using System.Collections.Generic;

[Serializable]
public class HarborLevelSaveData
{
    public string id;

    //Index into the harbor definition's levels
    public int level;
}

// Level of every harbor that was built.
// Written to application.persistentDataPath as json.
[Serializable]
public class HarborSaveData
{
    public int version = 1;
    public List<HarborLevelSaveData> harbors = new List<HarborLevelSaveData>();
}
