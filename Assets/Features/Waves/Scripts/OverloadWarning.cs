// Decides when the resort counts as overloaded. A short spike does not show the warning
// and a short calm does not hide it, so it does not blink while loads hover around full.
// Holds no Unity objects, so it runs in tests.
public class OverloadWarning
{
    private readonly float showAfterSeconds;
    private readonly float hideAfterSeconds;
    private float overloadedFor;
    private float calmFor;

    public bool Visible { get; private set; }

    public OverloadWarning(float showAfterSeconds, float hideAfterSeconds)
    {
        this.showAfterSeconds = showAfterSeconds;
        this.hideAfterSeconds = hideAfterSeconds;
    }

    // Moves on by seconds with what the services report now. True when Visible changed.
    public bool Update(bool overloaded, float seconds)
    {
        bool before = Visible;
        if (overloaded)
        {
            overloadedFor += seconds;
            calmFor = 0f;
            if (!Visible && overloadedFor >= showAfterSeconds) Visible = true;
        }
        else
        {
            calmFor += seconds;
            overloadedFor = 0f;
            if (Visible && calmFor >= hideAfterSeconds) Visible = false;
        }
        return Visible != before;
    }

    // Another island or no wave, start over.
    public void Reset()
    {
        Visible = false;
        overloadedFor = 0f;
        calmFor = 0f;
    }
}
