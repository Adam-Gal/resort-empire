using System;
using System.Collections.Generic;
using UnityEngine;

// Tuning of guest waves. Values live here so they can change without touching the rules.
[CreateAssetMenu(menuName = "Resort Empire/Waves/Wave Settings", fileName = "WaveSettings")]
public class WaveSettings : ScriptableObject
{
    // A kind of building the island needs before a wave can start.
    [Serializable]
    public class Requirement
    {
        public BuildingCategory category;

        //Shown to the player when it is missing
        public string displayName;
    }

    //Seconds guests keep bringing new requests, after that the wave only finishes started ones
    [Min(1f)] public float requestWindowSeconds = 120f;

    //Basic services every island needs, guests always ask for these
    public Requirement[] requirements =
    {
        new Requirement { category = BuildingCategory.Rooms, displayName = "Ubytovanie" },
        new Requirement { category = BuildingCategory.Food, displayName = "Jedlo" },
        new Requirement { category = BuildingCategory.Drinks, displayName = "Nápoje" }
    };

    //How often a running wave is written to disk, so a crash loses at most this much
    [Min(1f)] public float autosaveSeconds = 5f;

    //Load against capacity from which a building barely keeps up, shown orange
    [Min(0f)] public float busyLoad = .8f;

    //Load above which a building no longer keeps up, shown red. At 1 a full building
    //with nobody waiting still counts as busy, one more guest asking makes it overloaded
    [Min(0f)] public float overloadedLoad = 1f;

    //Seconds a basic service must be overloaded before the resort warns about it
    [Min(0f)] public float overloadWarningDelay = 1f;

    //Seconds every basic service must keep up again before the warning goes away
    [Min(0f)] public float overloadWarningHold = 2f;

    [Header("Speed up")]
    //Energy one speed up costs, each building can be sped up once per wave
    [Min(1)] public int speedUpEnergy = 2;

    //Seconds the building serves faster, its capacity stays the same
    [Min(1f)] public float speedUpSeconds = 30f;

    //How many times faster it serves meanwhile
    [Min(1f)] public float speedUpMultiplier = 2f;

    // Problems that make the settings unusable, empty when they are fine.
    public List<string> Validate()
    {
        var problems = new List<string>();
        if (requestWindowSeconds < 1f) problems.Add("Request window must be at least 1 second.");
        if (autosaveSeconds < 1f) problems.Add("Autosave interval must be at least 1 second.");
        if (busyLoad >= overloadedLoad) problems.Add("Busy load must be lower than overloaded load.");
        if (speedUpEnergy < 1) problems.Add("Speed up must cost at least 1 energy.");
        if (speedUpMultiplier <= 1f) problems.Add("Speed up multiplier must be above 1.");
        if (requirements == null) return problems;

        var seen = new HashSet<BuildingCategory>();
        foreach (Requirement requirement in requirements)
        {
            if (requirement == null) { problems.Add("Empty requirement."); continue; }
            if (string.IsNullOrWhiteSpace(requirement.displayName)) problems.Add($"Requirement {requirement.category} has no display name.");
            if (!seen.Add(requirement.category)) problems.Add($"Requirement {requirement.category} is listed twice.");
        }
        return problems;
    }

    private void OnValidate()
    {
        foreach (string problem in Validate()) Debug.LogWarning($"[WaveSettings] {problem}", this);
    }
}
