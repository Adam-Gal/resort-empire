using System;


// player's resource progress
// written to application.persistentDataPath as json
[Serializable]
public class ResourceSaveData
{
    public int version = 1;
    public MoneyResource money = new MoneyResource();
    public EnergyResource energy = new EnergyResource();

    public bool starterGiftClaimed;
}
