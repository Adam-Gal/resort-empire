using System.Collections.Generic;
using UnityEngine;

// Tuning of the guests a wave brings. Values live here so they can change without touching the rules.
[CreateAssetMenu(menuName = "Resort Empire/Guests/Guest Settings", fileName = "GuestSettings")]
public class GuestSettings : ScriptableObject
{
    [Header("Arrivals")]
    //Guests the first wave on an island brings
    [Min(1)] public int baseGuests = 4;

    //Added with every later wave on the same island
    [Min(0)] public int guestsPerWave = 2;

    //No wave brings more than this
    [Min(1)] public int maxGuests = 60;

    //Part of the request window over which guests arrive, the rest lets the last ones finish
    [Range(.1f, 1f)] public float arrivalShare = .75f;

    [Header("Requests")]
    //Services one guest asks for, food and drinks are always among them
    [Min(1)] public int minRequests = 2;
    [Min(1)] public int maxRequests = 3;

    //Seconds one guest is served at a building, a speed up divides it
    [Min(.1f)] public float serviceSeconds = 5f;

    //Seconds a guest rests in their room between two requests
    [Min(0f)] public float minBreakSeconds = 4f;
    [Min(0f)] public float maxBreakSeconds = 8f;

    [Header("Patience")]
    //Seconds a guest waits in a queue before leaving unhappy
    [Min(1f)] public float queuePatienceSeconds = 25f;

    //Seconds a new guest waits for a free room before leaving unhappy
    [Min(1f)] public float roomPatienceSeconds = 60f;

    [Header("Rating")]
    //Seconds of waiting, all waits of a guest added up, that cost one star out of five
    [Min(.1f)] public float waitSecondsPerStar = 8f;

    [Header("Rewards")]
    //Money for every request served, paid right away
    [Min(0)] public int moneyPerRequest = 15;

    //Reputation a guest gives by their rating, 1 to 5 stars, rounded. Negative takes it, never below zero
    public int[] reputationByStars = { -1, 0, 1, 2, 3 };

    // Guests the given wave brings to an island.
    public int GuestsForWave(int waveNumber) =>
        Mathf.Min(maxGuests, baseGuests + guestsPerWave * Mathf.Max(0, waveNumber - 1));

    // Stars a guest gives after waiting this long in total, 1 to 5.
    public float RatingAfterWaiting(float waitedSeconds) =>
        Mathf.Clamp(5f - waitedSeconds / waitSecondsPerStar, 1f, 5f);

    // Reputation one guest's rating is worth.
    public int ReputationFor(float stars) =>
        reputationByStars[Mathf.Clamp(Mathf.RoundToInt(stars) - 1, 0, reputationByStars.Length - 1)];

    // Problems that make the settings unusable, empty when they are fine.
    public List<string> Validate()
    {
        var problems = new List<string>();
        if (baseGuests < 1) problems.Add("A wave must bring at least 1 guest.");
        if (maxGuests < baseGuests) problems.Add("Max guests must not be lower than base guests.");
        if (minRequests < 1) problems.Add("Guests must ask for at least 1 service.");
        if (maxRequests < minRequests) problems.Add("Max requests must not be lower than min requests.");
        if (maxBreakSeconds < minBreakSeconds) problems.Add("Max break must not be shorter than min break.");
        if (serviceSeconds <= 0f) problems.Add("Service must take some time.");
        if (reputationByStars == null || reputationByStars.Length != 5) problems.Add("Reputation by stars needs exactly 5 values, for 1 to 5 stars.");
        return problems;
    }

    private void OnValidate()
    {
        foreach (string problem in Validate()) Debug.LogWarning($"[GuestSettings] {problem}", this);
    }
}
