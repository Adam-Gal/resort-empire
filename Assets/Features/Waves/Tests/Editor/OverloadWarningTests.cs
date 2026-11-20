using NUnit.Framework;

public class OverloadWarningTests
{
    [Test]
    public void ShortSpike_DoesNotShowTheWarning()
    {
        var warning = new OverloadWarning(1f, 2f);

        warning.Update(true, .5f);
        warning.Update(false, .25f);
        warning.Update(true, .5f);

        Assert.That(warning.Visible, Is.False);
    }

    [Test]
    public void LastingOverload_ShowsTheWarning()
    {
        var warning = new OverloadWarning(1f, 2f);

        bool changed = false;
        for (int i = 0; i < 4; i++) changed |= warning.Update(true, .25f);

        Assert.That(warning.Visible, Is.True);
        Assert.That(changed, Is.True);
    }

    [Test]
    public void ShortCalm_KeepsTheWarning_LongCalmHidesIt()
    {
        var warning = new OverloadWarning(0f, 2f);
        warning.Update(true, .1f);

        warning.Update(false, 1f);
        Assert.That(warning.Visible, Is.True, "1 s of calm is not enough");

        warning.Update(true, .1f);
        warning.Update(false, 1.5f);
        Assert.That(warning.Visible, Is.True, "calm starts over after another overload");

        warning.Update(false, .5f);
        Assert.That(warning.Visible, Is.False);
    }

    [Test]
    public void Reset_HidesAndForgets()
    {
        var warning = new OverloadWarning(1f, 2f);
        warning.Update(true, 2f);

        warning.Reset();
        warning.Update(true, .5f);

        Assert.That(warning.Visible, Is.False);
    }
}
