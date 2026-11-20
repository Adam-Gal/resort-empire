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
        settings.coupleChance = 0f;
        settings.walkSpeed = 1f;
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
    private static float Run(GuestSimulation simulation, List<ServicePoint> points, float maxSeconds = 300f,
        IGuestRoutes routes = null)
    {
        float elapsed = 0f;
        while (elapsed < maxSeconds)
        {
            bool accepting = elapsed < Window;
            simulation.Tick(Step, accepting, elapsed, Window, points, routes);
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
    public void WindowClosed_RestingGuestsAskForNothingMoreAndLeave()
    {
        settings.arrivalShare = .01f;
        settings.minBreakSeconds = 1f;
        settings.maxBreakSeconds = 1f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);
        var points = Resort(rooms: 5, food: 5, drinks: 5);

        // Guests arrive and get rooms, then the window closes while they rest.
        float elapsed = 0f;
        for (int i = 0; i < 5; i++, elapsed += Step) simulation.Tick(Step, true, elapsed, Window, points);
        Assert.That(simulation.Arrived, Is.EqualTo(2));
        while (elapsed < 60f && simulation.HasOpenRequests)
        {
            simulation.Tick(Step, false, elapsed, Window, points);
            elapsed += Step;
        }

        Assert.That(simulation.AllGuestsServed, Is.True);
        Assert.That(simulation.ServedRequests, Is.EqualTo(0), "nobody asks for anything once the window closed");
        Assert.That(simulation.RatedGuests, Is.EqualTo(2));
        Assert.That(simulation.AverageRating, Is.EqualTo(3f), "food and drinks were missed");
    }

    [Test]
    public void WindowClosed_ServiceUnderWayIsFinished_ThenTheyWalkToTheHarbor()
    {
        settings.baseGuests = 1;
        settings.serviceSeconds = 2f;
        var points = Resort(rooms: 5, food: 5, drinks: 5);
        var routes = new FakeRoutes(0f);
        routes.pairs[("food", GuestSimulation.HarborKey)] = 3f;
        routes.pairs[("drinks", GuestSimulation.HarborKey)] = 3f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        GuestData guest = null;
        for (int i = 0; i < 100 && guest == null; i++)
        {
            simulation.Tick(Step, true, i * Step, Window, points, routes);
            if (simulation.Parties.Count > 0 && simulation.Parties[0].state == GuestState.InService) guest = simulation.Parties[0];
        }
        Assert.That(guest, Is.Not.Null);
        string served = guest.building;

        // The window closes during the service: it is finished, then they head for the harbor from there.
        int before = simulation.ServedRequests;
        for (int i = 0; i < 25 && guest.state == GuestState.InService; i++) simulation.Tick(Step, false, Window, Window, points, routes);
        Assert.That(simulation.ServedRequests, Is.EqualTo(before + 1));
        Assert.That(guest.state, Is.EqualTo(GuestState.Leaving));
        Assert.That(guest.from, Is.EqualTo(served));
        Assert.That(simulation.HasOpenRequests, Is.True);

        for (int i = 0; i < 31; i++) simulation.Tick(Step, false, Window, Window, points, routes);
        Assert.That(simulation.HasOpenRequests, Is.False);
        Assert.That(simulation.AllGuestsServed, Is.True);
    }

    [Test]
    public void WindowClosed_GuestsStillWithoutARoomLeave()
    {
        settings.arrivalShare = .01f;
        settings.disembarkSeconds = 0f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);
        var points = Resort(rooms: 1, food: 5, drinks: 5);

        simulation.Tick(Step, true, 1f, Window, points);
        Assert.That(simulation.WaitingForRoom, Is.EqualTo(1));

        simulation.Tick(Step, false, Window, Window, points);
        Assert.That(simulation.WaitingForRoom, Is.EqualTo(0));
        Assert.That(simulation.UnhappyGuests, Is.EqualTo(1));
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

    // Every walk between two different places is the same length, unless set for a pair.
    private class FakeRoutes : IGuestRoutes
    {
        private readonly float distance;
        public readonly Dictionary<(string, string), float> pairs = new Dictionary<(string, string), float>();

        public FakeRoutes(float distance) => this.distance = distance;

        public float Distance(string from, string to)
        {
            if (from == to) return 0f;
            if (pairs.TryGetValue((from, to), out float d) || pairs.TryGetValue((to, from), out d)) return d;
            return distance;
        }
    }

    [Test]
    public void Walking_IsNotWaiting_GuestsStillRateFiveStars()
    {
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 5, food: 5, drinks: 5), routes: new FakeRoutes(3f));

        Assert.That(simulation.AllGuestsServed, Is.True);
        Assert.That(simulation.HappyGuests, Is.EqualTo(2));
        Assert.That(simulation.AverageRating, Is.EqualTo(5f));
    }

    [Test]
    public void Walking_TakesTime()
    {
        settings.baseGuests = 1;
        settings.optionalServiceChance = 0f;
        var near = GuestSimulation.Create(settings, "island", 1, 42);
        var far = GuestSimulation.Create(settings, "island", 1, 42);

        float nearSeconds = Run(near, Resort(rooms: 5, food: 5, drinks: 5), routes: new FakeRoutes(1f));
        float farSeconds = Run(far, Resort(rooms: 5, food: 5, drinks: 5), routes: new FakeRoutes(4f));

        // Harbor to room, there and back for food and for drinks, room to harbor: 6 walks, 3 s longer each.
        // Each walk ends on a whole step, so a little more.
        Assert.That(farSeconds - nearSeconds, Is.EqualTo(18f).Within(6 * Step + .05f));
    }

    [Test]
    public void GuestOnTheWay_DoesNotLoadTheBuildingUntilTheyArrive()
    {
        settings.baseGuests = 1;
        var points = Resort(rooms: 5, food: 1, drinks: 1);
        var routes = new FakeRoutes(0f);
        routes.pairs[("room", "food")] = 5f;
        routes.pairs[("room", "drinks")] = 5f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        // Gets the room and sets off for the first service right away, 5 s away.
        simulation.Tick(Step, true, 0f, Window, points, routes);
        GuestData guest = simulation.Parties[0];
        Assert.That(guest.state, Is.EqualTo(GuestState.GoingToService));
        Assert.That(simulation.GetLoad(guest.building), Is.EqualTo(0f));

        for (int i = 0; i < 51; i++) simulation.Tick(Step, true, 0f, Window, points, routes);
        Assert.That(guest.state, Is.Not.EqualTo(GuestState.GoingToService));
        Assert.That(guest.waited, Is.EqualTo(0f));
    }

    [Test]
    public void EquallyBusyServices_TheNearerIsChosen()
    {
        settings.baseGuests = 1;
        settings.optionalServiceChance = 0f;
        var points = new List<ServicePoint>
        {
            new ServicePoint("room", BuildingCategory.Rooms, 5, 1f),
            new ServicePoint("farFood", BuildingCategory.Food, 2, 1f),
            new ServicePoint("nearFood", BuildingCategory.Food, 2, 1f),
            new ServicePoint("drinks", BuildingCategory.Drinks, 2, 1f)
        };
        var routes = new FakeRoutes(1f);
        routes.pairs[("room", "farFood")] = 20f;
        routes.pairs[("room", "nearFood")] = 2f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        bool wentNear = false, wentFar = false;
        for (int i = 0; i < 600 && !simulation.AllGuestsServed; i++)
        {
            simulation.Tick(Step, true, i * Step, Window, points, routes);
            foreach (GuestData guest in simulation.Parties)
            {
                wentNear |= guest.building == "nearFood";
                wentFar |= guest.building == "farFood";
            }
        }

        Assert.That(wentNear, Is.True);
        Assert.That(wentFar, Is.False);
    }

    [Test]
    public void LeavingGuests_WalkBackToTheHarborBeforeTheWaveIsDone()
    {
        settings.baseGuests = 1;
        settings.optionalServiceChance = 0f;
        var points = Resort(rooms: 5, food: 5, drinks: 5);
        var routes = new FakeRoutes(0f);
        routes.pairs[("room", GuestSimulation.HarborKey)] = 5f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        GuestData leaving = null;
        for (int i = 0; i < 600 && leaving == null; i++)
        {
            simulation.Tick(Step, true, i * Step, Window, points, routes);
            if (simulation.Parties.Count > 0 && simulation.Parties[0].state == GuestState.Leaving) leaving = simulation.Parties[0];
        }

        Assert.That(leaving, Is.Not.Null);
        Assert.That(simulation.RatedGuests, Is.EqualTo(1), "rated when they check out");
        Assert.That(simulation.AllGuestsServed, Is.False);
        Assert.That(simulation.HasOpenRequests, Is.True, "the wave waits until they reached the harbor");

        for (int i = 0; i < 51; i++) simulation.Tick(Step, true, 0f, Window, points, routes);
        Assert.That(simulation.AllGuestsServed, Is.True);
        Assert.That(simulation.RatedGuests, Is.EqualTo(1));
    }

    [Test]
    public void Couple_CountsAsTwoGuests()
    {
        settings.coupleChance = 1f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        Run(simulation, Resort(rooms: 2, food: 2, drinks: 2));

        Assert.That(simulation.TotalGuests, Is.EqualTo(2));
        Assert.That(simulation.HappyGuests, Is.EqualTo(2));
        Assert.That(simulation.RatedGuests, Is.EqualTo(2));
        Assert.That(simulation.GetMetCount(GuestNeeds.Accommodation), Is.EqualTo(2));
        Assert.That(simulation.GetMetCount(GuestNeeds.Food), Is.EqualTo(2));
        Assert.That(simulation.ServedRequests, Is.EqualTo(4));
    }

    [Test]
    public void Couple_TakesTwoBedsInOneBuilding()
    {
        settings.coupleChance = 1f;
        var points = new List<ServicePoint>
        {
            new ServicePoint("roomA", BuildingCategory.Rooms, 1, 1f),
            new ServicePoint("roomB", BuildingCategory.Rooms, 2, 1f),
            new ServicePoint("food", BuildingCategory.Food, 5, 1f),
            new ServicePoint("drinks", BuildingCategory.Drinks, 5, 1f)
        };
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        simulation.Tick(Step, true, 0f, Window, points, new FakeRoutes(10f));

        Assert.That(simulation.Parties.Count, Is.EqualTo(1));
        Assert.That(simulation.Parties[0].People, Is.EqualTo(2));
        Assert.That(simulation.Parties[0].room, Is.EqualTo("roomB"));
    }

    [Test]
    public void Rooms_ChosenByFreeBeds_SoASmallBuildingGetsGuestsFromTheStart()
    {
        settings.baseGuests = 1;
        var points = new List<ServicePoint>
        {
            new ServicePoint("hotel", BuildingCategory.Rooms, 8, 1f),
            new ServicePoint("bungalow", BuildingCategory.Rooms, 2, 1f),
            new ServicePoint("food", BuildingCategory.Food, 5, 1f),
            new ServicePoint("drinks", BuildingCategory.Drinks, 5, 1f)
        };

        // The first guest of many waves: about 2 in 10 go to the bungalow, its share of the free beds.
        int inBungalow = 0;
        const int Waves = 200;
        for (uint seed = 1; seed <= Waves; seed++)
        {
            var simulation = GuestSimulation.Create(settings, "island", 1, seed * 7919);
            simulation.Tick(Step, true, 0f, Window, points, new FakeRoutes(10f));
            if (simulation.Parties[0].room == "bungalow") inBungalow++;
        }

        Assert.That(inBungalow, Is.InRange(Waves / 10, Waves * 3 / 10));
    }

    [Test]
    public void Rooms_AllBedsAreUsed()
    {
        settings.baseGuests = 10;
        settings.arrivalShare = .01f;
        settings.disembarkSeconds = 0f;
        var points = new List<ServicePoint>
        {
            new ServicePoint("hotel", BuildingCategory.Rooms, 8, 1f),
            new ServicePoint("bungalow", BuildingCategory.Rooms, 2, 1f),
            new ServicePoint("food", BuildingCategory.Food, 5, 1f),
            new ServicePoint("drinks", BuildingCategory.Drinks, 5, 1f)
        };
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        simulation.Tick(Step, true, 1f, Window, points, new FakeRoutes(10f));

        int inBungalow = 0, inHotel = 0;
        foreach (GuestData guest in simulation.Parties)
        {
            if (guest.room == "bungalow") inBungalow++;
            if (guest.room == "hotel") inHotel++;
        }
        Assert.That(inBungalow, Is.EqualTo(2));
        Assert.That(inHotel, Is.EqualTo(8));
    }

    [Test]
    public void Couple_ServedOnlyWhenBothFit_NobodyPassesThem()
    {
        settings.serviceSeconds = 3f;
        settings.queuePatienceSeconds = 100f;
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);
        var points = Resort(rooms: 5, food: 2, drinks: 2);

        // A single, then a couple, then another single, all in line at a restaurant for 2.
        GuestWaveData data = simulation.Data;
        data.totalGuests = 4;
        data.arrived = 4;
        data.guests.Add(new GuestData { id = 0, size = 1, ticket = 0, state = GuestState.Queued, building = "food", room = "room", current = BuildingCategory.Food });
        data.guests.Add(new GuestData { id = 1, size = 2, ticket = 1, state = GuestState.Queued, building = "food", room = "room", current = BuildingCategory.Food });
        data.guests.Add(new GuestData { id = 2, size = 1, ticket = 2, state = GuestState.Queued, building = "food", room = "room", current = BuildingCategory.Food });
        data.nextTicket = 3;

        simulation.Tick(Step, true, 0f, Window, points);

        Assert.That(data.guests[0].state, Is.EqualTo(GuestState.InService));
        Assert.That(data.guests[1].state, Is.EqualTo(GuestState.Queued), "one place left, the couple waits");
        Assert.That(data.guests[2].state, Is.EqualTo(GuestState.Queued), "the single behind does not pass them");
        Assert.That(simulation.GetQueued("food"), Is.EqualTo(3));
    }

    [Test]
    public void Guests_ArriveByBoat_EachBoatsGuestsAlmostTogether()
    {
        settings.baseGuests = 9;
        settings.boatsPerWave = 3;
        settings.disembarkSeconds = .5f;
        settings.arrivalShare = .5f;
        var points = Resort(rooms: 20, food: 20, drinks: 20);
        var simulation = GuestSimulation.Create(settings, "island", 1, 42);

        // Window 60 s, guests over the first 30 s: boats at 0, 10 and 20 s, 3 guests each half a second apart.
        int ArrivedBy(float seconds)
        {
            simulation.Tick(0f, true, seconds, Window, points);
            return simulation.Arrived;
        }

        Assert.That(ArrivedBy(0f), Is.EqualTo(1));
        Assert.That(ArrivedBy(1f), Is.EqualTo(3), "the first boat's guests step off within a second");
        Assert.That(ArrivedBy(9.9f), Is.EqualTo(3), "nobody more until the next boat");
        Assert.That(ArrivedBy(11f), Is.EqualTo(6));
        Assert.That(ArrivedBy(21f), Is.EqualTo(9));
    }

    [Test]
    public void OldSave_PartyWithoutSize_IsOneGuest()
    {
        Assert.That(new GuestData { size = 0 }.People, Is.EqualTo(1));
    }
}
