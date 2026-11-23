using System.Collections.Generic;
using UnityEngine;

// Tuning of the resort value. Values live here so they can change without touching the rules.
[CreateAssetMenu(menuName = "Resort Empire/Resort Value/Resort Value Settings", fileName = "ResortValueSettings")]
public class ResortValueSettings : ScriptableObject
{
    [Header("Buildings")]
    //Off leaves out palms, shrubs and the like, so the free ones the islands start with add nothing
    public bool countDecorations;

    [Header("Guest score")]
    //Stars the guest score starts at before the first wave, 3 changes nothing
    [Range(1f, 5f)] public float startingScore = 3f;

    //How far one wave moves the score toward its own average rating, 1 replaces it outright
    [Range(.05f, 1f)] public float waveWeight = .5f;

    //Buildings are worth this many times their price at a 1 star score
    [Range(0f, 1f)] public float lowestMultiplier = .7f;

    //And this many times at a 5 star score, 3 stars is always exactly 1
    [Min(1f)] public float highestMultiplier = 1.3f;

    // How much the guest score scales the buildings' worth.
    public float MultiplierFor(float score)
    {
        score = Mathf.Clamp(score, 1f, 5f);
        return score <= 3f
            ? Mathf.Lerp(lowestMultiplier, 1f, (score - 1f) / 2f)
            : Mathf.Lerp(1f, highestMultiplier, (score - 3f) / 2f);
    }

    // The guest score after a wave with this average rating.
    public float ScoreAfterWave(float score, float waveRating) =>
        Mathf.Clamp(Mathf.Lerp(score, waveRating, waveWeight), 1f, 5f);

    // Problems that make the settings unusable, empty when they are fine.
    public List<string> Validate()
    {
        var problems = new List<string>();
        if (lowestMultiplier > 1f) problems.Add("Lowest multiplier must not be above 1.");
        if (highestMultiplier < 1f) problems.Add("Highest multiplier must not be below 1.");
        return problems;
    }

    private void OnValidate()
    {
        foreach (string problem in Validate()) Debug.LogWarning($"[ResortValueSettings] {problem}", this);
    }
}
