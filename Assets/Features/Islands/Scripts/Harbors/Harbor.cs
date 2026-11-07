using UnityEngine;

// One harbor on the shore. HarborManager creates it and swaps its art per level.
public class Harbor : MonoBehaviour
{
    public string Id { get; private set; }
    public int Level { get; private set; }

    private Transform anchor;
    private GameObject art;
    private SpriteRenderer sprite;

    public Vector3 Center => anchor.position;

    // The sprite itself, it pops when tapped like a building.
    public Transform Art => sprite != null ? sprite.transform : transform;

    // A mirrored harbor lies along the other grid axis.
    public bool Mirrored => anchor.lossyScale.x < 0f;

    public void Initialize(string id, Transform siteAnchor)
    {
        Id = id;
        anchor = siteAnchor;
    }

    // The level prefabs keep their art off the root, so the art is moved onto the anchor.
    public void SetArt(GameObject prefab, int level)
    {
        Level = level;
        if (art != null) Destroy(art);

        art = Instantiate(prefab, transform);
        art.name = prefab.name;
        art.transform.localPosition = Vector3.zero;
        sprite = art.GetComponentInChildren<SpriteRenderer>();
        if (sprite == null) return;

        // The anchor's mirroring comes in through the parent, the art is moved in world space.
        art.transform.position += anchor.position - sprite.transform.position;
    }

    public bool Contains(Vector3 world)
    {
        if (sprite == null) return false;
        Bounds bounds = sprite.bounds;
        return bounds.Contains(new Vector3(world.x, world.y, bounds.center.z));
    }
}
