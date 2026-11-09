using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlacedBuildingSaveData
{
    public string id;
    public Vector2Int origin;

    //Index into BuildingDefinition.levels, older saves load as the first level
    public int level;

    //UTC ticks of an upgrade to the next level that is still being built, 0 when none
    public long upgradeStartTicks;
    public long upgradeEndTicks;

    public int employees;
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

    //The island's starting decorations were turned into buildings once
    public bool startingBuildingsPlaced;

    //Landmarks are placed one by one, so ones added in an update also reach older saves
    public List<string> placedLandmarks = new List<string>();
}