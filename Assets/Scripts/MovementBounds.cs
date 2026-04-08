using UnityEngine;

public class MovementBounds : MonoBehaviour
{
    [Header("Collision")]
    [Tooltip("Enable convex on all MeshColliders in this object at runtime (required for collision detection against moving objects).")]
    public bool enableConvexAtRuntime = false;

    // All min/max values are offsets relative to the object's position at Start
    public bool constrainX;
    public float minX = -5f, maxX = 5f;

    public bool constrainY;
    public float minY = -5f, maxY = 5f;

    public bool constrainZ;
    public float minZ = -5f, maxZ = 5f;

    // Computed at Start from initial position + offsets
    private float _worldMinX, _worldMaxX;
    private float _worldMinY, _worldMaxY;
    private float _worldMinZ, _worldMaxZ;

    private void Start()
    {
        if (enableConvexAtRuntime) {
            foreach (MeshCollider mc in GetComponentsInChildren<MeshCollider>()) {
                mc.convex = true;
            }
        }
        Vector3 p = transform.position;
        _worldMinX = p.x + minX; _worldMaxX = p.x + maxX;
        _worldMinY = p.y + minY; _worldMaxY = p.y + maxY;
        _worldMinZ = p.z + minZ; _worldMaxZ = p.z + maxZ;
    }

    public Vector3 Clamp(Vector3 pos)
    {
        if (constrainX) pos.x = Mathf.Clamp(pos.x, _worldMinX, _worldMaxX);
        if (constrainY) pos.y = Mathf.Clamp(pos.y, _worldMinY, _worldMaxY);
        if (constrainZ) pos.z = Mathf.Clamp(pos.z, _worldMinZ, _worldMaxZ);
        return pos;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        const float fallback = 1f;
        Vector3 p = transform.position;

        float cx = constrainX ? p.x + (minX + maxX) * 0.5f : p.x;
        float cy = constrainY ? p.y + (minY + maxY) * 0.5f : p.y;
        float cz = constrainZ ? p.z + (minZ + maxZ) * 0.5f : p.z;

        float sx = constrainX ? (maxX - minX) : fallback;
        float sy = constrainY ? (maxY - minY) : fallback;
        float sz = constrainZ ? (maxZ - minZ) : fallback;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawCube(new Vector3(cx, cy, cz), new Vector3(sx, sy, sz));
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireCube(new Vector3(cx, cy, cz), new Vector3(sx, sy, sz));
    }
#endif
}
