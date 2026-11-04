using System.Collections;
using UnityEngine;

// Smoke when something is demolished or cleared, the art sinks into it.
public class RemovalEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] particles;
    [SerializeField] private float squashDuration = .3f;

    //Footprint the particle counts in the prefab are made for, bigger ones emit more
    [SerializeField] private int baseCells = 4;
    [SerializeField] private int maxCountMultiplier = 4;

    // visual is taken over and destroyed when it has sunk.
    public void Play(Vector3 footprintCenter, Vector2Int size, GameObject visual)
    {
        transform.position = footprintCenter;

        // The footprint is a diamond, spread the particles over its width and depth.
        float width = (size.x + size.y) * .5f;
        float multiplier = Mathf.Clamp((float)size.x * size.y / baseCells, 1f, maxCountMultiplier);
        foreach (ParticleSystem system in particles)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.scale = new Vector3(width * .7f, width * .35f, 1f);

            ParticleSystem.EmissionModule emission = system.emission;
            for (int i = 0; i < emission.burstCount; i++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(i);
                burst.count = Mathf.Round(burst.count.constant * multiplier);
                emission.SetBurst(i, burst);
            }
            system.Play();
        }

        StartCoroutine(Sink(visual));
    }

    private IEnumerator Sink(GameObject visual)
    {
        if (visual != null)
        {
            Transform target = visual.transform;
            Vector3 startScale = target.localScale;
            for (float time = 0f; time < squashDuration; time += Time.deltaTime)
            {
                float t = time / squashDuration;
                // A short bounce up before it falls flat.
                float height = t < .25f ? 1f + t * .4f : Mathf.Lerp(1.1f, 0f, (t - .25f) / .75f);
                float width = 1f + (1f - height) * .15f;
                target.localScale = new Vector3(startScale.x * width, startScale.y * height, startScale.z);
                yield return null;
            }
            Destroy(visual);
        }

        foreach (ParticleSystem system in particles)
        {
            while (system.IsAlive(true)) yield return null;
        }
        Destroy(gameObject);
    }
}
