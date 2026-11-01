using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlacedBuildingSaveData
{
    public string id;
    public Vector2Int origin;
}

// player's island progress
// written to application.persistentDataPath as json
[Serializable]
public class BuildingSaveData
{
    public int version = 1;
    public List<PlacedBuildingSaveData> buildings = new List<PlacedBuildingSaveData>();
    public List<Vector2Int> clearedCells = new List<Vector2Int>();
    public List<string> restoredRuins = new List<string>();
}
