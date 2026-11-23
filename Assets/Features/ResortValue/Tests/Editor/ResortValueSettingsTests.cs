using NUnit.Framework;
using UnityEngine;

public class ResortValueSettingsTests
{
    private ResortValueSettings settings;

    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<ResortValueSettings>();
        settings.lowestMultiplier = .7f;
        settings.highestMultiplier = 1.3f;
        settings.waveWeight = .5f;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(settings);

    [Test]
    public void Multiplier_ThreeStarsChangesNothing()
    {
        Assert.That(settings.MultiplierFor(3f), Is.EqualTo(1f));
    }

    [Test]
    public void Multiplier_RunsFromLowestToHighest()
    {
        Assert.That(settings.MultiplierFor(1f), Is.EqualTo(.7f).Within(1e-5f));
        Assert.That(settings.MultiplierFor(2f), Is.EqualTo(.85f).Within(1e-5f));
        Assert.That(settings.MultiplierFor(4f), Is.EqualTo(1.15f).Within(1e-5f));
        Assert.That(settings.MultiplierFor(5f), Is.EqualTo(1.3f).Within(1e-5f));
    }

    [Test]
    public void ScoreAfterWave_MovesPartWayToTheWaveRating()
    {
        Assert.That(settings.ScoreAfterWave(3f, 5f), Is.EqualTo(4f).Within(1e-5f));
        Assert.That(settings.ScoreAfterWave(4f, 1f), Is.EqualTo(2.5f).Within(1e-5f));
    }

    [Test]
    public void ScoreAfterWave_FullWeightReplacesTheScore()
    {
        settings.waveWeight = 1f;

        Assert.That(settings.ScoreAfterWave(2f, 4.5f), Is.EqualTo(4.5f).Within(1e-5f));
    }
}
