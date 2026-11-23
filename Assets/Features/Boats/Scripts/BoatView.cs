using UnityEngine;

// How a boat looks. Put it on the boat prefab; BoatManager moves the boat and tells this which way it points.
// Give a sprite for each of the 8 ways the boat can point on screen, the closest one to its heading is shown,
// so a turning boat steps through them. A missing sprite is skipped and the next closest one is used.
// Between two sprites the hull tilts a little towards the real heading, so a sprite swap is a smaller jump.
public class BoatView : MonoBehaviour
{
    //Renderer of the hull. Empty: the first SpriteRenderer of the prefab
    [SerializeField] private SpriteRenderer body;

    [Header("Directions, as seen on screen")]
    [SerializeField] private Sprite right;
    [SerializeField] private Sprite topRight;
    [SerializeField] private Sprite top;
    [SerializeField] private Sprite topLeft;
    [SerializeField] private Sprite left;
    [SerializeField] private Sprite bottomLeft;
    [SerializeField] private Sprite bottom;
    [SerializeField] private Sprite bottomRight;

    //Angle of the diagonal directions above the horizontal, 26.57 follows a 2:1 isometric grid, 45 true diagonals
    [SerializeField, Range(10f, 80f)] private float diagonalAngle = 26.57f;

    [Header("Turning")]
    //Degrees a second the drawn heading follows the real one, so a sharp change steps through the sprites. 0: at once
    [SerializeField, Min(0f)] private float turnDegreesPerSecond = 240f;

    //How much of the gap between the heading and the sprite shown the hull is tilted by, 0 turns the tilt off
    [SerializeField, Range(0f, 1f)] private float tiltShare = .5f;

    //Tilt never goes past this many degrees either way
    [SerializeField, Range(0f, 45f)] private float maxTilt = 12f;

    //A sprite is only swapped once another one is closer by this many degrees, so it does not flicker on a border
    [SerializeField, Range(0f, 10f)] private float swapMargin = 3f;

    [Header("Water")]
    //Gentle rocking of the hull up and down, in world units, and the seconds of one rock
    [SerializeField, Min(0f)] private float bobHeight = .015f;
    [SerializeField, Min(.1f)] private float bobSeconds = 2.5f;

    //Foam behind the boat, it only flows while the boat moves
    [SerializeField] private ParticleSystem wake;

    private SpriteRenderer[] renderers;
    private Color[] colors;
    private Transform bobbing;
    private Vector3 bobRest;
    private float bobOffset;
    private float shownAlpha = -1f;

    // Heading drawn now, in degrees, and the angle of the sprite shown for it.
    private float heading;
    private bool hasHeading;
    private Sprite shownSprite;
    private float shownSpriteAngle;

    private void Awake()
    {
        if (body == null) body = GetComponentInChildren<SpriteRenderer>(true);
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].color;

        // Only the hull rocks, and only when it sits below the root, so the boat stays where it is put.
        if (body != null && body.transform != transform)
        {
            bobbing = body.transform;
            bobRest = bobbing.localPosition;
        }
        bobOffset = Random.value * bobSeconds;
    }

    public void Show(Vector3 position, Vector3 facing, bool moving, float alpha)
    {
        transform.position = position;
        Point(facing);

        if (bobbing != null && bobHeight > 0f)
            bobbing.localPosition = bobRest + Vector3.up * (bobHeight * Mathf.Sin((Time.time + bobOffset) / bobSeconds * 2f * Mathf.PI));

        if (wake != null)
        {
            ParticleSystem.EmissionModule emission = wake.emission;
            if (emission.enabled != moving) emission.enabled = moving;
        }

        if (!Mathf.Approximately(alpha, shownAlpha))
        {
            shownAlpha = alpha;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color color = colors[i];
                color.a *= alpha;
                renderers[i].color = color;
            }
        }
    }

    // Turns the drawn heading towards where the boat points, shows the sprite closest to it
    // and tilts the hull by part of what that sprite is off by.
    private void Point(Vector3 facing)
    {
        if (body == null || (facing.x * facing.x + facing.y * facing.y) < 1e-8f) return;
        float target = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;

        // The first time it is drawn, it just points there.
        if (!hasHeading || turnDegreesPerSecond <= 0f) heading = target;
        else heading = Mathf.MoveTowardsAngle(heading, target, turnDegreesPerSecond * Time.deltaTime);
        hasHeading = true;

        Sprite best = null;
        float bestAngle = 0f, bestGap = float.MaxValue;
        Consider(right, 0f, ref best, ref bestAngle, ref bestGap);
        Consider(topRight, diagonalAngle, ref best, ref bestAngle, ref bestGap);
        Consider(top, 90f, ref best, ref bestAngle, ref bestGap);
        Consider(topLeft, 180f - diagonalAngle, ref best, ref bestAngle, ref bestGap);
        Consider(left, 180f, ref best, ref bestAngle, ref bestGap);
        Consider(bottomLeft, 180f + diagonalAngle, ref best, ref bestAngle, ref bestGap);
        Consider(bottom, 270f, ref best, ref bestAngle, ref bestGap);
        Consider(bottomRight, 360f - diagonalAngle, ref best, ref bestAngle, ref bestGap);
        if (best == null) return;

        // The sprite already shown stays until another one is clearly closer.
        bool keep = shownSprite != null && best != shownSprite &&
                    Mathf.Abs(Mathf.DeltaAngle(heading, shownSpriteAngle)) <= bestGap + swapMargin;
        if (!keep)
        {
            shownSprite = best;
            shownSpriteAngle = bestAngle;
        }
        if (body.sprite != shownSprite) body.sprite = shownSprite;

        float tilt = Mathf.Clamp(Mathf.DeltaAngle(shownSpriteAngle, heading) * tiltShare, -maxTilt, maxTilt);
        body.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    private void Consider(Sprite sprite, float angle, ref Sprite best, ref float bestAngle, ref float bestGap)
    {
        if (sprite == null) return;
        float gap = Mathf.Abs(Mathf.DeltaAngle(heading, angle));
        if (gap >= bestGap) return;
        bestGap = gap;
        bestAngle = angle;
        best = sprite;
    }
}