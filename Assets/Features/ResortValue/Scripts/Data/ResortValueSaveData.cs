using System;

// What the resort value remembers, written to application.persistentDataPath as json.
// The buildings' worth is not saved, it is counted again from the buildings.
[Serializable]
public class ResortValueSaveData
{
    public int version = 1;

    //Guests' ratings over past waves, in stars, newer waves count more
    public float guestScore;

    //Off until the first wave was rated, the score then starts from the settings
    public bool hasScore;
}
