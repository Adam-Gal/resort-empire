using NUnit.Framework;
using UnityEngine;

public class ServiceStateTests
{
    private WaveSettings settings;

    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<WaveSettings>();
        settings.busyLoad = .8f;
        settings.overloadedLoad = 1f;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(settings);

    [Test]
    public void FullWithNobodyWaiting_IsBusyNotOverloaded()
    {
        // A restaurant for 2 serving 2 guests, nobody in line.
        Assert.That(WaveManager.StateForLoad(2f / 2f, settings), Is.EqualTo(ServiceState.Busy));
    }

    [Test]
    public void OneMoreGuestWaiting_IsOverloaded()
    {
        // The same restaurant with a third guest in line.
        Assert.That(WaveManager.StateForLoad(3f / 2f, settings), Is.EqualTo(ServiceState.Overloaded));
    }

    [Test]
    public void LowLoad_IsNormal_NearlyFull_IsBusy()
    {
        Assert.That(WaveManager.StateForLoad(.5f, settings), Is.EqualTo(ServiceState.Normal));
        Assert.That(WaveManager.StateForLoad(.8f, settings), Is.EqualTo(ServiceState.Busy));
    }

    [Test]
    public void RoomsSharingAWaitingGuest_AreOverloaded()
    {
        // Two full rooms of 2 beds share one guest without a room: each gets half of them on top.
        Assert.That(WaveManager.StateForLoad((2f + .5f) / 2f, settings), Is.EqualTo(ServiceState.Overloaded));
    }
}
