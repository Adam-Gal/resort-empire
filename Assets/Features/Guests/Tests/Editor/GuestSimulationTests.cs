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
        settings.minRequests = 2;
        settings.maxRequests = 2;
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
}
