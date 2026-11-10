using System;
using UnityEngine;

[Serializable]
public abstract class GameResource
{
    [SerializeField] protected long amount;

    // Not saved meaningfully - always overwritten from the database on load,
    // so designers can change caps without breaking old saves.
    [SerializeField] protected long maxAmount;

    public abstract ResourceType Type { get; }

    public long Amount => amount;
    public long MaxAmount => maxAmount;
    public bool HasCap => maxAmount > 0;
    public bool IsFull => HasCap && amount >= maxAmount;

    public void Initialize(long startAmount, long max)
    {
        maxAmount = Math.Max(0, max);
        amount = 0;
        Add(startAmount, false);
    }

    public void SetMaxAmount(long max)
    {
        maxAmount = Math.Max(0, max);
    }

    public bool CanAfford(long cost)
    {
        return cost <= 0 || amount >= cost;
    }
//ignoreCaps ignores maximum resources amount. Currently it is unlimited in the DB, if we'll decide to add
//something like money vaults or resort levels - could be usefull for rewards
    public long Add(long value, bool ignoreCap) 
    {
        if (value <= 0) return 0;

        long before = amount;
        long target = amount > long.MaxValue - value ? long.MaxValue : amount + value;

        if (!ignoreCap && HasCap)
        {
            if (amount >= maxAmount) return 0;
            if (target > maxAmount) target = maxAmount;
        }

        amount = target;
        return amount - before;
    }

    public bool TrySpend(long cost)
    {
        if (cost < 0 || !CanAfford(cost)) return false;
        amount -= cost;
        return true;
    }

    public void SetAmountUnchecked(long value)
    {
        amount = Math.Max(0, value);
    }
}
