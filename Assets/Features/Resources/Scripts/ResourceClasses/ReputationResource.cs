using System;


// Earned, never spent. Unlocks the other islands of the archipelago (see IslandProgress).
[Serializable]
public class ReputationResource : GameResource
{
    public override ResourceType Type => ResourceType.Reputation;
}
