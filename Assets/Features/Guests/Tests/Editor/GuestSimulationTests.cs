using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GuestSimulationTests
{
    private const float Step = .1f;
    private const float Window = 60f;

    private GuestSettings settings;

    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<GuestSettings>();
        settings.baseGuests = 2;
        settings.guestsPerWave = 0;
        settings.maxGuests = 10;
        settings.arrivalShare = .5f;
        settings.optionalServiceChance = 1f;
        settings.serviceSeconds = 1f;
        settings.minBreakSeconds = 0f;
        settings.maxBreakSeconds = 0f;
        settings.queuePatienceSeconds = 5f;
        settings.roomPatienceSeconds = 30f;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(settings);

    private static List<ServicePoint> Resort(int rooms, int food, int drinks, float speed = 1f) => new List<ServicePoint>
    {
        new ServicePoint("room", BuildingCategory.Rooms, rooms, 1f),
        new ServicePoint("food", BuildingCategory.Food, food, speed),
        new ServicePoint("drinks", BuildingCategory.Drinks, drinks, speed)
    };

    // Runs the wave like WaveSession does: the window, then until nothing is open.
    private static float Run(GuestSimulation simulation, List<ServicePoint> points, float maxSeconds = 300f)
    {
        float elapsed = 0f;
        while (elapsed < maxSeconds)
        {
            bool accepting = elapsed < Window;
            simulation.Tick(Step, accepting, elapsed, Window, points);
            elapsed += Step;
            if (simulation.AllGuestsServed) break;
            if (!accepting && !simulation.HasOpenRequests) break;
        }
        return elapsed;
    }

    [Test]
    public void EnoughCapacity_EveryoneLeavesHappy()
    {
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 5, food: 5, drinks: 5));

        Assert.That(simulation.AllGuestsServed, Is.True);
        Assert.That(simulation.HappyGuests, Is.EqualTo(2));
        Assert.That(simulation.UnhappyGuests, Is.EqualTo(0));
        Assert.That(simulation.ServedRequests, Is.EqualTo(4));
    }

    [Test]
    public void WaveEndsEarly_WhenAllGuestsAreDone()
    {
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        float elapsed = Run(simulation, Resort(rooms: 5, food: 5, drinks: 5));

        Assert.That(elapsed, Is.LessThan(Window));
    }

    [Test]
    public void NoFreeRoom_GuestWaitsUntilOneChecksOut()
    {
        settings.arrivalShare = .01f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 1, food: 5, drinks: 5));

        Assert.That(simulation.HappyGuests, Is.EqualTo(2));
        Assert.That(simulation.UnhappyGuests, Is.EqualTo(0));
    }

    [Test]
    public void NoRoomForTooLong_GuestLeavesUnhappy()
    {
        settings.roomPatienceSeconds = 1f;
        settings.arrivalShare = .01f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 1, food: 5, drinks: 5));

        Assert.That(simulation.UnhappyGuests, Is.EqualTo(1));
        Assert.That(simulation.HappyGuests, Is.EqualTo(1));
    }

    [Test]
    public void LongQueue_GuestsLeaveUnhappy()
    {
        settings.baseGuests = 6;
        settings.arrivalShare = .01f;
        settings.serviceSeconds = 10f;
        settings.queuePatienceSeconds = 2f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 6, food: 1, drinks: 1));

        Assert.That(simulation.UnhappyGuests, Is.GreaterThan(0));
        Assert.That(simulation.HappyGuests + simulation.UnhappyGuests, Is.EqualTo(6));
    }

    [Test]
    public void Load_CountsQueuedGuestsAgainstCapacity()
    {
        settings.baseGuests = 4;
        settings.arrivalShare = .01f;
        settings.serviceSeconds = 30f;
        settings.queuePatienceSeconds = 60f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);
        List<ServicePoint> points = Resort(rooms: 4, food: 1, drinks: 1);

        for (int i = 0; i < 8; i++) simulation.Tick(Step, true, i * Step, Window, points);

        float asked = simulation.GetLoad("food") + simulation.GetLoad("drinks");
        Assert.That(asked, Is.EqualTo(4f), "4 guests, each at food or drinks, against 1 seat each");
        Assert.That(simulation.GetLoad("room"), Is.EqualTo(1f));
    }

    [Test]
    public void SpeedUp_ServesFaster()
    {
        var normal = GuestSimulation.Create(settings, "island", 1, 7);
        var fast = GuestSimulation.Create(settings, "island", 1, 7);
        settings.arrivalShare = .01f;

        float normalTime = Run(normal, Resort(rooms: 5, food: 5, drinks: 5));
        float fastTime = Run(fast, Resort(rooms: 5, food: 5, drinks: 5, speed: 2f));

        Assert.That(fastTime, Is.LessThan(normalTime));
    }

    [Test]
    public void WindowClosed_RestingGuestsCheckOutAndNobodyArrives()
    {
        settings.baseGuests = 4;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        simulation.Tick(Step, false, 0f, Window, Resort(rooms: 5, food: 5, drinks: 5));

        Assert.That(simulation.Arrived, Is.EqualTo(0));
        Assert.That(simulation.HasOpenRequests, Is.False);
    }

    [Test]
    public void RemovedBuilding_GuestGoesToAnotherOfTheSameService()
    {
        settings.arrivalShare = .01f;
        settings.serviceSeconds = 5f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);
        var points = new List<ServicePoint>
        {
            new ServicePoint("room", BuildingCategory.Rooms, 5, 1f),
            new ServicePoint("food", BuildingCategory.Food, 5, 1f),
            new ServicePoint("drinks", BuildingCategory.Drinks, 5, 1f)
        };
        simulation.Tick(Step, true, 0f, Window, points);
        simulation.Tick(Step, true, Step, Window, points);

        var rebuilt = new List<ServicePoint>
        {
            new ServicePoint("room", BuildingCategory.Rooms, 5, 1f),
            new ServicePoint("food2", BuildingCategory.Food, 5, 1f),
            new ServicePoint("drinks2", BuildingCategory.Drinks, 5, 1f)
        };
        Run(simulation, rebuilt);

        Assert.That(simulation.HappyGuests, Is.EqualTo(2));
        Assert.That(simulation.ServedRequests, Is.EqualTo(4));
    }

    [Test]
    public void GuestsForWave_GrowsAndCaps()
    {
        settings.baseGuests = 6;
        settings.guestsPerWave = 3;
        settings.maxGuests = 10;

        Assert.That(settings.GuestsForWave(1), Is.EqualTo(6));
        Assert.That(settings.GuestsForWave(2), Is.EqualTo(9));
        Assert.That(settings.GuestsForWave(5), Is.EqualTo(10));
    }

    [Test]
    public void NoWaiting_EveryoneRatesFiveStars()
    {
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 5, food: 5, drinks: 5));

        Assert.That(simulation.RatedGuests, Is.EqualTo(2));
        Assert.That(simulation.AverageRating, Is.EqualTo(5f));
        Assert.That(simulation.ReputationChange, Is.EqualTo(2 * settings.ReputationFor(5f)));
    }

    [Test]
    public void WaitingInQueues_LowersTheRating()
    {
        settings.baseGuests = 4;
        settings.arrivalShare = .01f;
        settings.serviceSeconds = 5f;
        settings.queuePatienceSeconds = 60f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 4, food: 1, drinks: 1));

        Assert.That(simulation.UnhappyGuests, Is.EqualTo(0));
        Assert.That(simulation.AverageRating, Is.LessThan(5f));
        Assert.That(simulation.AverageRating, Is.GreaterThan(1f));
    }

    [Test]
    public void GivingUp_RatesOneStar()
    {
        settings.roomPatienceSeconds = 1f;
        settings.arrivalShare = .01f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 1, food: 5, drinks: 5));

        // One guest got the room without waiting, the other gave up on it.
        Assert.That(simulation.RatedGuests, Is.EqualTo(2));
        Assert.That(simulation.AverageRating, Is.EqualTo(3f));
        Assert.That(simulation.ReputationChange, Is.EqualTo(settings.ReputationFor(5f) + settings.ReputationFor(1f)));
    }

    [Test]
    public void Rating_DropsWithTotalWaitingAndStaysBetweenOneAndFive()
    {
        settings.waitSecondsPerStar = 8f;

        Assert.That(settings.RatingAfterWaiting(0f), Is.EqualTo(5f));
        Assert.That(settings.RatingAfterWaiting(8f), Is.EqualTo(4f));
        Assert.That(settings.RatingAfterWaiting(4f + 4f), Is.EqualTo(settings.RatingAfterWaiting(8f)), "two short waits count like one long");
        Assert.That(settings.RatingAfterWaiting(1000f), Is.EqualTo(1f));
    }

    [Test]
    public void EnoughCapacity_EveryGuestHasAllThreeNeedsMet()
    {
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 5, food: 5, drinks: 5));

        foreach (GuestNeeds need in GuestNeedsInfo.Basic)
            Assert.That(simulation.GetMetCount(need), Is.EqualTo(2), need.ToString());
    }

    [Test]
    public void WindowClosed_GuestsStillGetFoodAndDrinksButNoExtras()
    {
        settings.arrivalShare = .01f;
        settings.minBreakSeconds = 1f;
        settings.maxBreakSeconds = 1f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);
        var points = Resort(rooms: 5, food: 5, drinks: 5);
        points.Add(new ServicePoint("pool", BuildingCategory.Pool, 5, 1f));

        // Guests arrive and get rooms, then the window closes while they rest.
        float elapsed = 0f;
        for (int i = 0; i < 5; i++, elapsed += Step) simulation.Tick(Step, true, elapsed, Window, points);
        Assert.That(simulation.Arrived, Is.EqualTo(2));
        while (elapsed < 60f && simulation.HasOpenRequests)
        {
            simulation.Tick(Step, false, elapsed, Window, points);
            elapsed += Step;
        }

        Assert.That(simulation.HasOpenRequests, Is.False);
        Assert.That(simulation.GetMetCount(GuestNeeds.Food), Is.EqualTo(2));
        Assert.That(simulation.GetMetCount(GuestNeeds.Drinks), Is.EqualTo(2));
        Assert.That(simulation.ServedRequests, Is.EqualTo(4), "activities are dropped once the window closed");

        // The wave ends like WaveManager ends it, everyone still resting checks out.
        simulation.CheckOutEveryone();
        Assert.That(simulation.AverageRating, Is.EqualTo(5f));
    }

    [Test]
    public void MissingService_UnmetNeedLowersTheRating()
    {
        settings.starsPerMissedNeed = 1f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);
        var noDrinks = new List<ServicePoint>
        {
            new ServicePoint("room", BuildingCategory.Rooms, 5, 1f),
            new ServicePoint("food", BuildingCategory.Food, 5, 1f)
        };

        Run(simulation, noDrinks);

        Assert.That(simulation.HappyGuests, Is.EqualTo(2));
        Assert.That(simulation.GetMetCount(GuestNeeds.Drinks), Is.EqualTo(0));
        Assert.That(simulation.AverageRating, Is.EqualTo(4f));
    }

    [Test]
    public void RatingFor_TakesStarsForUnmetNeedsAndStaysAboveOne()
    {
        settings.starsPerMissedNeed = 1f;

        Assert.That(settings.RatingFor(0f, 0), Is.EqualTo(5f));
        Assert.That(settings.RatingFor(0f, 2), Is.EqualTo(3f));
        Assert.That(settings.RatingFor(1000f, 3), Is.EqualTo(1f));
    }

    [Test]
    public void CountMissing_CountsBasicNeedsOnly()
    {
        Assert.That(GuestNeedsInfo.CountMissing(GuestNeeds.None), Is.EqualTo(3));
        Assert.That(GuestNeedsInfo.CountMissing(GuestNeeds.Accommodation | GuestNeeds.Food), Is.EqualTo(1));
        Assert.That(GuestNeedsInfo.CountMissing(GuestNeeds.All), Is.EqualTo(0));
        Assert.That(GuestNeedsInfo.For(BuildingCategory.Activities), Is.EqualTo(GuestNeeds.None));
    }

    [Test]
    public void OptionalServices_AreNotAskedForWithoutABuilding()
    {
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 5, food: 5, drinks: 5));

        Assert.That(simulation.ServedRequests, Is.EqualTo(4), "only food and drinks for each of the 2 guests");
    }

    [Test]
    public void OptionalServices_AreAskedForOnceBuilt()
    {
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);
        var points = Resort(rooms: 5, food: 5, drinks: 5);
        points.Add(new ServicePoint("pool", BuildingCategory.Pool, 5, 1f));
        points.Add(new ServicePoint("spa", BuildingCategory.Spa, 5, 1f));

        Run(simulation, points);

        Assert.That(simulation.ServedRequests, Is.EqualTo(8), "food, drinks, pool and spa for each of the 2 guests");
        Assert.That(simulation.HappyGuests, Is.EqualTo(2));
    }

    [Test]
    public void OptionalServices_ChanceZero_NobodyAsks()
    {
        settings.optionalServiceChance = 0f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);
        var points = Resort(rooms: 5, food: 5, drinks: 5);
        points.Add(new ServicePoint("tennis", BuildingCategory.Tennis, 5, 1f));

        Run(simulation, points);

        Assert.That(simulation.ServedRequests, Is.EqualTo(4));
    }

    [Test]
    public void ReputationFor_UsesRoundedStars()
    {
        settings.reputationByStars = new[] { -1, 0, 1, 2, 3 };

        Assert.That(settings.ReputationFor(1f), Is.EqualTo(-1));
        Assert.That(settings.ReputationFor(3.4f), Is.EqualTo(1));
        Assert.That(settings.ReputationFor(4.6f), Is.EqualTo(3));
    }
}
