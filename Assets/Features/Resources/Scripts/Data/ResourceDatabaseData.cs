using System;
using System.Collections.Generic;

// Classes that mirror ResourceDatabase.json (static design data, never changed at runtime).
// JsonUtility only reads public (or [SerializeField]) fields, so field names must match the JSON keys.

[Serializable]
public class ResourceDatabaseData
{
    public int version = 1;
    public List<ResourceDefinition> resources = new List<ResourceDefinition>();
    public EnergySettings energySettings = new EnergySettings();
    public StarterGiftData starterGift = new StarterGiftData();

    public ResourceDefinition GetDefinition(ResourceType type)
    {
        for (int i = 0; i < resources.Count; i++)
        {
            if (resources[i].TryGetType(out ResourceType defType) && defType == type)
                return resources[i];
        }
        return null;
    }
}

[Serializable]
public class ResourceDefinition
{
    public string id;            // must match a ResourceType name, e.g. "Money"
    public string displayName;
    public string iconPath;      // path inside a Resources folder, without extension
    public long startAmount;     // given on a brand new save (before the starter gift)
    public long maxAmount;       // 0 = no cap

    public bool TryGetType(out ResourceType type)
    {
        return Enum.TryParse(id, true, out type);
    }
}

[Serializable]
public class EnergySettings
{
    public float secondsPerEnergy = 60f;  // 1 energy skips this many seconds of construction
    public long minSpeedUpCost = 1;       // never cheaper than this
}

[Serializable]
public class StarterGiftData
{
    public bool enabled = true;
    public string title;
    public string message;
    public List<ResourceReward> rewards = new List<ResourceReward>();
}


// Resource + amount as stored in JSON. Uses a string id because JsonUtility
// writes enums as numbers, which is fragile if the enum order ever changes.
[Serializable]
public class ResourceReward
{
    public string resourceId;
    public long amount;

    public bool TryGetType(out ResourceType type)
    {
        return Enum.TryParse(resourceId, true, out type);
    }
}
