using System;

// A price of something, in one resource. Use arrays/lists of these on buildings so they show
//  in the inspector, e.g. a hotel costs 2000 Money.
[Serializable]
public struct ResourceCost
{
    public ResourceType type;
    public long amount;

    public ResourceCost(ResourceType type, long amount)
    {
        this.type = type;
        this.amount = amount;
    }
}
