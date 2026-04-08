using UnityEngine;

public class MovementBounds : MonoBehaviour
{
    [Header("Collision")]
    [Tooltip("Enable convex on all MeshColliders in this object at runtime (required for collision detection against moving objects).")]
    public bool enableConvexAtRuntime = false;

    [Header("Direction")]
    [Tooltip("Pitch (X-axis rotation) for the movement frame in degrees.")]
    public float pitchDeg = 0f;
    [Tooltip("Yaw (Y-axis rotation) for the movement frame in degrees.")]
    public float yawDeg = 0f;

    public Quaternion DirectionRotation => Quaternion.Euler(pitchDeg, yawDeg, 0f);

    // All min/max values are offsets relative to the object's position at Start, in the rotated frame
    public bool constrainX;
    public float minX = -5f, maxX = 5f;

    public bool constrainY;
    public float minY = -5f, maxY = 5f;

    public bool constrainZ;
    public float minZ = -5f, maxZ = 5f;

    private Vector3 _origin;

    private void Start()
    {
        if (enableConvexAtRuntime) {
            foreach (MeshCollider mc in GetComponentsInChildren<MeshCollider>()) {
                mc.convex = true;
            }
        }
        _origin = transform.position;
    }

    public Vector3 Clamp(Vector3 worldPos)
    {
        if (!constrainX && !constrainY && !constrainZ) return worldPos;
        Quaternion rot = DirectionRotation;
        Vector3 local = Quaternion.Inverse(rot) * (worldPos - _origin);
        if (constrainX) local.x = Mathf.Clamp(local.x, minX, maxX);
        if (constrainY) local.y = Mathf.Clamp(local.y, minY, maxY);
        if (constrainZ) local.z = Mathf.Clamp(local.z, minZ, maxZ);
        return _origin + rot * local;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Quaternion rot = Quaternion.Euler(pitchDeg, yawDeg, 0f);
        const float fallback = 1f;
        Vector3 p = transform.position;

        float cx = constrainX ? (minX + maxX) * 0.5f : 0f;
        float cy = constrainY ? (minY + maxY) * 0.5f : 0f;
        float cz = constrainZ ? (minZ + maxZ) * 0.5f : 0f;

        float sx = constrainX ? (maxX - minX) : fallback;
        float sy = constrainY ? (maxY - minY) : fallback;
        float sz = constrainZ ? (maxZ - minZ) : fallback;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(p, rot, Vector3.one);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawCube(new Vector3(cx, cy, cz), new Vector3(sx, sy, sz));
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireCube(new Vector3(cx, cy, cz), new Vector3(sx, sy, sz));
        Gizmos.matrix = oldMatrix;
    }
#endif
}
