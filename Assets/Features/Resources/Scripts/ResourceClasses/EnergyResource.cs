using System;
using UnityEngine;


[Serializable]
public class EnergyResource : GameResource
{
    public override ResourceType Type => ResourceType.Energy;
    public static long CalculateSpeedUpCost(float remainingSeconds, EnergySettings settings)
    {
        if (remainingSeconds <= 0f) return 0;

        float secondsPerEnergy = Mathf.Max(0.01f, settings.secondsPerEnergy);
        long cost = (long)Math.Ceiling(remainingSeconds / secondsPerEnergy);
        return Math.Max(settings.minSpeedUpCost, cost);
    }
}
