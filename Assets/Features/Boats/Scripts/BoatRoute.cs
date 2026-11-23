using System.Collections.Generic;
using UnityEngine;

// A smooth line on the water that a boat follows, curving through a few points in order.
// Holds no Unity objects, so it runs in tests.
public class BoatRoute
{
    // Points of the curve, close together, and how far each lies from the start.
    public readonly List<Vector3> points = new List<Vector3>();
    public readonly List<float> along = new List<float>();

    public float Length => along.Count > 0 ? along[along.Count - 1] : 0f;

    // A curve through the points in order, null when there are fewer than two different ones.
    // Centripetal Catmull-Rom, so it neither loops nor overshoots when the points are spaced unevenly.
    // Step is roughly the spacing of the points the curve is made of.
    public static BoatRoute Through(IReadOnlyList<Vector3> controls, float step)
    {
        var clean = new List<Vector3>();
        foreach (Vector3 control in controls)
        {
            var point = new Vector3(control.x, control.y, 0f);
            if (clean.Count == 0 || (point - clean[clean.Count - 1]).sqrMagnitude > 1e-8f) clean.Add(point);
        }
        if (clean.Count < 2) return null;

        step = Mathf.Max(.01f, step);
        var route = new BoatRoute();
        route.Add(clean[0]);
        for (int i = 0; i < clean.Count - 1; i++)
        {
            Vector3 p1 = clean[i], p2 = clean[i + 1];

            // The ends carry on straight, so the boat starts and docks heading along its first and last stretch.
            Vector3 p0 = i > 0 ? clean[i - 1] : p1 + (p1 - p2);
            Vector3 p3 = i + 2 < clean.Count ? clean[i + 2] : p2 + (p2 - p1);

            int samples = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude / step));
            for (int s = 1; s <= samples; s++) route.Add(CatmullRom(p0, p1, p2, p3, s / (float)samples));
        }
        return route;
    }

    // The point this far from the start, and the way the route heads there, length 1.
    public Vector3 PointAt(float distance, out Vector3 direction)
    {
        if (points.Count < 2)
        {
            direction = Vector3.right;
            return points.Count == 1 ? points[0] : Vector3.zero;
        }

        distance = Mathf.Clamp(distance, 0f, Length);
        int i = along.BinarySearch(distance);
        if (i < 0) i = ~i;
        i = Mathf.Clamp(i, 1, points.Count - 1);

        Vector3 a = points[i - 1], b = points[i];
        float span = along[i] - along[i - 1];
        float t = span > 0f ? (distance - along[i - 1]) / span : 0f;
        direction = (b - a).normalized;
        return Vector3.LerpUnclamped(a, b, t);
    }

    private void Add(Vector3 point)
    {
        if (points.Count > 0)
        {
            float gap = (point - points[points.Count - 1]).magnitude;
            if (gap < 1e-5f) return;
            along.Add(Length + gap);
        }
        else along.Add(0f);
        points.Add(point);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t0 = 0f;
        float t1 = t0 + Knot(p0, p1);
        float t2 = t1 + Knot(p1, p2);
        float t3 = t2 + Knot(p2, p3);
        float u = Mathf.Lerp(t1, t2, t);

        Vector3 a1 = Blend(p0, p1, t0, t1, u);
        Vector3 a2 = Blend(p1, p2, t1, t2, u);
        Vector3 a3 = Blend(p2, p3, t2, t3, u);
        Vector3 b1 = Blend(a1, a2, t0, t2, u);
        Vector3 b2 = Blend(a2, a3, t1, t3, u);
        return Blend(b1, b2, t1, t2, u);
    }

    private static float Knot(Vector3 a, Vector3 b) => Mathf.Max(Mathf.Sqrt((b - a).magnitude), 1e-4f);

    private static Vector3 Blend(Vector3 a, Vector3 b, float ta, float tb, float u) =>
        tb - ta < 1e-6f ? a : (a * (tb - u) + b * (u - ta)) / (tb - ta);
}