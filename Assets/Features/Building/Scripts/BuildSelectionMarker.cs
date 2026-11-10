using UnityEngine;
using UnityEngine.Tilemaps;

// Outline around the selected cells and a short pop of whatever was tapped.
public class BuildSelectionMarker : MonoBehaviour
{
    [SerializeField] private LineRenderer outline;
    [SerializeField] private Tilemap obstacleLayer;
    [SerializeField] private float popScale = 1.12f;
    [SerializeField] private float popDuration = .25f;

    private readonly Vector3[] corners = new Vector3[4];
    private readonly Vector3[] scaledCorners = new Vector3[4];
    private Vector3 center;
    private float popTime;
    private bool popping;

    private Transform target;
    private Vector3 targetScale;

    private bool hasObstacle;
    private Vector3Int obstacleCell;
    private Tile obstacleTile;
    private Vector3 obstacleCenter;

    public void Show(BuildGrid grid, Vector2Int origin, Vector2Int size, Transform popTarget, bool isObstacle)
    {
        StopPop();

        // Front corner of the footprint, then along the grid axes.
        Vector3 stepX = (grid.CellToWorld(origin + Vector2Int.right) - grid.CellToWorld(origin));
        Vector3 stepY = (grid.CellToWorld(origin + Vector2Int.up) - grid.CellToWorld(origin));
        Vector3 front = grid.CellToWorld(origin) - (stepX + stepY) * .5f;
        corners[0] = front;
        corners[1] = front + stepX * size.x;
        corners[2] = corners[1] + stepY * size.y;
        corners[3] = front + stepY * size.y;
        center = (corners[0] + corners[2]) * .5f;

        target = popTarget;
        if (target != null) targetScale = target.localScale;

        hasObstacle = isObstacle;
        if (hasObstacle)
        {
            obstacleCell = (Vector3Int)origin;
            obstacleTile = obstacleLayer.GetTile<Tile>(obstacleCell);
            obstacleCenter = center - grid.CellToWorld(origin);
            obstacleLayer.SetTileFlags(obstacleCell, TileFlags.None);
        }

        popTime = 0f;
        popping = true;
        outline.enabled = true;
        ApplyPop(1f);
    }

    public void Hide()
    {
        StopPop();
        outline.enabled = false;
    }

    private void Awake()
    {
        outline.enabled = false;
    }

    private void Update()
    {
        if (!popping) return;

        popTime += Time.unscaledDeltaTime;
        if (popTime >= popDuration)
            StopPop();
        else
            ApplyPop(1f + (popScale - 1f) * Mathf.Sin(popTime / popDuration * Mathf.PI));
    }

    private void StopPop()
    {
        if (!popping) return;

        popping = false;
        ApplyPop(1f);
        if (hasObstacle && obstacleLayer.HasTile(obstacleCell))
            obstacleLayer.SetTileFlags(obstacleCell, obstacleTile.flags);
    }

    private void ApplyPop(float scale)
    {
        for (int i = 0; i < corners.Length; i++)
            scaledCorners[i] = center + (corners[i] - center) * scale;
        outline.SetPositions(scaledCorners);

        if (target != null) target.localScale = targetScale * scale;

        if (hasObstacle && obstacleLayer.HasTile(obstacleCell))
        {
            Matrix4x4 aroundCenter = Matrix4x4.Translate(obstacleCenter) * Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(-obstacleCenter);
            obstacleLayer.SetTransformMatrix(obstacleCell, aroundCenter * obstacleTile.transform);
        }
    }
}
