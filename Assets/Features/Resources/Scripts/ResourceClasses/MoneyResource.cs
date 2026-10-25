using System;


// put money-only logic here later like income multipliers, interest, etc.
[Serializable]
public class MoneyResource : GameResource
{
    public override ResourceType Type => ResourceType.Money;
}
