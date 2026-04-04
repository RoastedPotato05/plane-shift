using UnityEngine;
using System.Collections.Generic;

public enum LocalThrustAxis
{
    Right,
    Left,
    Forward,
    Backward
}

[RequireComponent(typeof(Rigidbody))]
public class SpaceshipController : MonoBehaviour
{
    private const int MaxMeshSamplePoints = 48;
    private const float ConcaveSampleHitToleranceXZ = 0.06f;

    [Header("Movement")]
    [SerializeField] private float thrustForce = 14f;
    [SerializeField] private float turnSpeed = 140f;
    [SerializeField, Range(1f, 20f)] private float turnSmoothing = 8f;
    [SerializeField] private LocalThrustAxis thrustAxis = LocalThrustAxis.Left;
    [SerializeField] private bool flattenThrustToXZ = true;

    [Header("Optional Drift Damping")]
    [SerializeField] private float passiveDrag = 0f;

    [Header("Tag-Based XZ Collision")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private string wallTag = "Wall";
    [SerializeField] private string goalTag = "Goal";
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float verticalDetectionHalfHeight = 50f;
    [SerializeField] private float footprintScale = 1f;

    [Header("Wall Bounce")]
    [SerializeField, Range(0f, 1.5f)] private float wallBounceMultiplier = 0.9f;
    [SerializeField] private float wallSeparationDistance = 0.2f;
    [SerializeField] private float minBounceSpeed = 2f;

    [Header("Goal")]
    [SerializeField] private float goalBrakeDrag = 6f;

    [Header("Debug")]
    [SerializeField] private bool debugCollisionGizmo = false;
    [SerializeField] private float debugGizmoY = 70f;
    [SerializeField] private bool debugDrawTaggedColliders = true;

    private Rigidbody body;
    private Collider shipCollider;
    private float fixedY;
    private float currentTurnRate;
    private bool levelComplete;
    private readonly List<Vector3> shipSampleLocalPoints = new List<Vector3>();
    private Vector3 previousPlanePosition;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        shipCollider = ResolveShipCollider();

        if (body.isKinematic) {
            Debug.LogWarning("SpaceshipController: Rigidbody was kinematic, switching to non-kinematic so thrust forces can move the ship.", this);
            body.isKinematic = false;
        }

        body.useGravity = false;
        body.drag = passiveDrag;
        body.angularDrag = 0f;

        // Keep the ship in the top-down XZ plane and upright.
        body.constraints = RigidbodyConstraints.FreezePositionY
            | RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationZ;

        fixedY = transform.position.y;

        if (shipCollider == null) {
            Debug.LogWarning("SpaceshipController: No collider found on ship. Tag-based overlap collisions will not work.", this);
            return;
        }

        Debug.Log("SpaceshipController: Using ship collider " + shipCollider.GetType().Name + " on " + shipCollider.gameObject.name + ".", this);

        BuildShipSamplePoints();
        previousPlanePosition = body.position;
    }

    private Collider ResolveShipCollider()
    {
        MeshCollider mesh = GetComponentInChildren<MeshCollider>();
        if (mesh != null && mesh.enabled) {
            return mesh;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++) {
            Collider c = colliders[i];
            if (c != null && c.enabled && !c.isTrigger) {
                return c;
            }
        }

        for (int i = 0; i < colliders.Length; i++) {
            Collider c = colliders[i];
            if (c != null && c.enabled) {
                return c;
            }
        }

        return null;
    }

    private void FixedUpdate()
    {
        if (levelComplete) {
            EnforcePlane();
            previousPlanePosition = body.position;
            return;
        }

        float thrustInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
        float turnInput = 0f;

        if (Input.GetKey(KeyCode.A)) {
            turnInput -= 1f;
        }

        if (Input.GetKey(KeyCode.D)) {
            turnInput += 1f;
        }

        if (thrustInput > 0f) {
            Vector3 thrustDirection = GetThrustDirection();

            if (flattenThrustToXZ) {
                thrustDirection.y = 0f;
                thrustDirection.Normalize();
            }

            if (thrustDirection.sqrMagnitude > 0f) {
                body.AddForce(thrustDirection * (thrustForce * thrustInput), ForceMode.Acceleration);
            }
        }

        float targetTurnRate = turnInput * turnSpeed;
        currentTurnRate = Mathf.Lerp(currentTurnRate, targetTurnRate, turnSmoothing * Time.fixedDeltaTime);

        if (!Mathf.Approximately(currentTurnRate, 0f)) {
            float deltaYaw = currentTurnRate * Time.fixedDeltaTime;
            Quaternion targetRotation = Quaternion.Euler(0f, body.rotation.eulerAngles.y + deltaYaw, 0f);
            body.MoveRotation(targetRotation);
        }

        EnforcePlane();
        ResolveProjectedTagCollisions();
        if (debugCollisionGizmo) {
            DrawDebugBox();
        }

        previousPlanePosition = body.position;
    }

    private Vector3 GetThrustDirection()
    {
        switch (thrustAxis)
        {
            case LocalThrustAxis.Right:
                return transform.right;
            case LocalThrustAxis.Left:
                return -transform.right;
            case LocalThrustAxis.Forward:
                return transform.forward;
            case LocalThrustAxis.Backward:
                return -transform.forward;
            default:
                return -transform.right;
        }
    }

    private void EnforcePlane()
    {
        Vector3 velocity = body.velocity;
        velocity.y = 0f;
        body.velocity = velocity;

        Vector3 position = body.position;
        position.y = fixedY;
        body.position = position;
    }

    private void ResolveProjectedTagCollisions()
    {
        if (shipCollider == null || !CompareTag(playerTag)) {
            return;
        }

        Bounds shipBounds = shipCollider.bounds;
        Vector3 checkCenter = new Vector3(shipBounds.center.x, fixedY, shipBounds.center.z);
        Vector3 checkHalfExtents = new Vector3(
            Mathf.Max(0.01f, shipBounds.extents.x * Mathf.Max(0.01f, footprintScale)),
            Mathf.Max(0.01f, verticalDetectionHalfHeight),
            Mathf.Max(0.01f, shipBounds.extents.z * Mathf.Max(0.01f, footprintScale))
        );

        Collider[] overlaps = Physics.OverlapBox(
            checkCenter,
            checkHalfExtents,
            Quaternion.identity,
            collisionLayers,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < overlaps.Length; i++) {
            Collider other = overlaps[i];
            if (other == null || other == shipCollider || other.transform == transform) {
                continue;
            }

            if (other.CompareTag(wallTag)) {
                if (IsProjectedWallContact(other)) {
                    BounceOffWall(other);
                    return;
                }
                continue;
            }

            if (other.CompareTag(goalTag)) {
                if (!levelComplete && IsProjectedWallContact(other)) {
                    levelComplete = true;
                    body.drag = goalBrakeDrag;
                    currentTurnRate = 0f;
                    Main main = FindObjectOfType<Main>();
                    if (main != null) {
                        main.ShowLevelComplete();
                    }
                    return;
                }
                continue;
            }

            if (!AreProjectedCollidersIntersecting(other, out Vector3 separationNormal)) {
                continue;
            }

            if (other.CompareTag(obstacleTag)) {
                Destroy(gameObject);
                return;
            }
        }
    }

    private bool IsProjectedWallContact(Collider wallCollider)
    {
        if (wallCollider == null || shipCollider == null) {
            return false;
        }

        float shipRadius = Mathf.Max(
            0.02f,
            Mathf.Min(shipCollider.bounds.extents.x, shipCollider.bounds.extents.z) * Mathf.Max(0.1f, footprintScale)
        );
        float speedPadding = Mathf.Max(0.01f, body.velocity.magnitude * Time.fixedDeltaTime);
        float threshold = shipRadius + speedPadding;

        Vector3 current = body.position;
        if (IsProjectedWallContactAt(wallCollider, current, threshold)) {
            return true;
        }

        const int sweepSteps = 6;
        for (int i = 1; i <= sweepSteps; i++) {
            float t = i / (float)sweepSteps;
            Vector3 sample = Vector3.Lerp(previousPlanePosition, current, t);
            if (IsProjectedWallContactAt(wallCollider, sample, threshold)) {
                return true;
            }
        }

        return false;
    }

    private bool IsProjectedWallContactAt(Collider wallCollider, Vector3 samplePosition, float threshold)
    {
        Vector3 queryPoint = new Vector3(samplePosition.x, wallCollider.bounds.center.y, samplePosition.z);
        Vector3 closest = wallCollider.ClosestPoint(queryPoint);

        Vector2 deltaXZ = new Vector2(samplePosition.x - closest.x, samplePosition.z - closest.z);
        return deltaXZ.sqrMagnitude <= (threshold * threshold);
    }

    private bool AreProjectedCollidersIntersecting(Collider other, out Vector3 separationNormal)
    {
        separationNormal = Vector3.zero;

        if (shipCollider == null || other == null) {
            return false;
        }

        Vector3 shipProjectedPos = GetProjectedColliderPosition(shipCollider.transform);
        Vector3 otherProjectedPos = GetProjectedColliderPosition(other.transform);

        bool intersects = Physics.ComputePenetration(
            shipCollider,
            shipProjectedPos,
            shipCollider.transform.rotation,
            other,
            otherProjectedPos,
            other.transform.rotation,
            out Vector3 direction,
            out float _
        );

        if (!intersects && other is MeshCollider mesh && !mesh.convex) {
            return IntersectsConcaveTargetBySamples(other, out separationNormal);
        }

        if (!intersects) {
            return false;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f) {
            separationNormal = direction.normalized;
        }
        return true;
    }

    private void BuildShipSamplePoints()
    {
        shipSampleLocalPoints.Clear();

        if (shipCollider is MeshCollider shipMesh && shipMesh.sharedMesh != null) {
            Vector3[] verts = shipMesh.sharedMesh.vertices;
            if (verts != null && verts.Length > 0) {
                int stride = Mathf.Max(1, verts.Length / MaxMeshSamplePoints);
                for (int i = 0; i < verts.Length; i += stride) {
                    shipSampleLocalPoints.Add(verts[i]);
                }
            }
        }

        if (shipSampleLocalPoints.Count == 0 && shipCollider != null) {
            // Fallback for primitive colliders: center + 8 corners from local-space bounds.
            Bounds b = shipCollider.bounds;
            Vector3 localCenter = transform.InverseTransformPoint(b.center);
            Vector3 ex = transform.InverseTransformVector(new Vector3(b.extents.x, 0f, 0f));
            Vector3 ez = transform.InverseTransformVector(new Vector3(0f, 0f, b.extents.z));

            shipSampleLocalPoints.Add(localCenter);
            shipSampleLocalPoints.Add(localCenter + ex + ez);
            shipSampleLocalPoints.Add(localCenter + ex - ez);
            shipSampleLocalPoints.Add(localCenter - ex + ez);
            shipSampleLocalPoints.Add(localCenter - ex - ez);
            shipSampleLocalPoints.Add(localCenter + ex);
            shipSampleLocalPoints.Add(localCenter - ex);
            shipSampleLocalPoints.Add(localCenter + ez);
            shipSampleLocalPoints.Add(localCenter - ez);
        }
    }

    private bool IntersectsConcaveTargetBySamples(Collider other, out Vector3 separationNormal)
    {
        separationNormal = Vector3.zero;

        if (shipSampleLocalPoints.Count == 0) {
            BuildShipSamplePoints();
            if (shipSampleLocalPoints.Count == 0) {
                return false;
            }
        }

        float topY = other.bounds.max.y + 0.5f;
        float bottomY = other.bounds.min.y - 0.5f;
        float rayDistance = Mathf.Max(0.1f, topY - bottomY);

        for (int i = 0; i < shipSampleLocalPoints.Count; i++) {
            Vector3 worldPoint = transform.TransformPoint(shipSampleLocalPoints[i]);
            float sampleX = worldPoint.x;
            float sampleZ = worldPoint.z;

            Vector3 rayOrigin = new Vector3(sampleX, topY, sampleZ);
            Ray ray = new Ray(rayOrigin, Vector3.down);
            if (other.Raycast(ray, out RaycastHit hit, rayDistance)) {
                Vector3 n = hit.normal;
                n.y = 0f;
                if (n.sqrMagnitude > 0.0001f) {
                    separationNormal = n.normalized;
                }
                return true;
            }

            Vector3 query = new Vector3(sampleX, other.bounds.center.y, sampleZ);
            Vector3 closest = other.ClosestPoint(query);
            Vector2 deltaXZ = new Vector2(query.x - closest.x, query.z - closest.z);
            if (deltaXZ.sqrMagnitude <= (ConcaveSampleHitToleranceXZ * ConcaveSampleHitToleranceXZ)) {
                if (deltaXZ.sqrMagnitude > 0.000001f) {
                    Vector3 n = new Vector3(deltaXZ.x, 0f, deltaXZ.y).normalized;
                    separationNormal = n;
                }
                return true;
            }
        }

        return false;
    }

    private Vector3 GetProjectedColliderPosition(Transform colliderTransform)
    {
        if (colliderTransform == null) {
            return Vector3.zero;
        }

        Transform root = colliderTransform.root;
        float rootToPlaneOffset = root != null ? (fixedY - root.position.y) : 0f;
        Vector3 projected = colliderTransform.position;
        projected.y += rootToPlaneOffset;
        return projected;
    }

    private void BounceOffWall(Vector3 projectedNormal)
    {
        Vector3 normal = new Vector3(projectedNormal.x, 0f, projectedNormal.z);
        if (normal.sqrMagnitude < 0.0001f) {
            return;
        }
        normal.Normalize();

        Vector3 shipPosition = body.position;
        Vector3 incoming = new Vector3(body.velocity.x, 0f, body.velocity.z);
        Vector3 reflected = Vector3.Reflect(incoming, normal) * wallBounceMultiplier;

        if (reflected.sqrMagnitude < (minBounceSpeed * minBounceSpeed)) {
            reflected = normal * minBounceSpeed;
        }

        body.velocity = new Vector3(reflected.x, 0f, reflected.z);
        body.position = new Vector3(
            shipPosition.x + (normal.x * wallSeparationDistance),
            fixedY,
            shipPosition.z + (normal.z * wallSeparationDistance)
        );
    }

    private void BounceOffWall(Collider wallCollider)
    {
        Vector3 shipPosition = body.position;

        // Project the query point onto the wall's own Y so ClosestPoint
        // returns a meaningful XZ result even when wall and ship differ greatly in Y.
        Vector3 queryPoint = new Vector3(shipPosition.x, wallCollider.bounds.center.y, shipPosition.z);
        Vector3 closest = wallCollider.ClosestPoint(queryPoint);

        Vector3 normal = new Vector3(shipPosition.x - closest.x, 0f, shipPosition.z - closest.z);

        if (normal.sqrMagnitude < 0.0001f) {
            Vector3 velocityXZ = new Vector3(body.velocity.x, 0f, body.velocity.z);
            normal = velocityXZ.sqrMagnitude > 0.0001f ? -velocityXZ.normalized : transform.forward;
        }

        normal.Normalize();

        Vector3 incoming = new Vector3(body.velocity.x, 0f, body.velocity.z);
        Vector3 reflected = Vector3.Reflect(incoming, normal) * wallBounceMultiplier;

        if (reflected.sqrMagnitude < (minBounceSpeed * minBounceSpeed)) {
            reflected = normal * minBounceSpeed;
        }

        body.velocity = new Vector3(reflected.x, 0f, reflected.z);
        body.position = new Vector3(
            shipPosition.x + (normal.x * wallSeparationDistance),
            fixedY,
            shipPosition.z + (normal.z * wallSeparationDistance)
        );
    }

    private void OnDrawGizmos()
    {
        if (!debugCollisionGizmo) {
            return;
        }

        Collider col = GetComponent<Collider>();
        if (col == null) {
            return;
        }

        Bounds b = col.bounds;
        Vector3 center = new Vector3(b.center.x, debugGizmoY, b.center.z);
        Vector3 size = new Vector3(
            b.size.x * Mathf.Max(0.01f, footprintScale),
            0.05f,
            b.size.z * Mathf.Max(0.01f, footprintScale)
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);
    }

    private void DrawDebugBox()
    {
        if (shipCollider == null) {
            return;
        }

        Bounds b = shipCollider.bounds;
        Vector3 c = new Vector3(b.center.x, debugGizmoY, b.center.z);
        Vector3 he = new Vector3(
            b.extents.x * Mathf.Max(0.01f, footprintScale),
            0.025f,
            b.extents.z * Mathf.Max(0.01f, footprintScale)
        );

        // 8 corners
        Vector3 p000 = c + new Vector3(-he.x, -he.y, -he.z);
        Vector3 p100 = c + new Vector3( he.x, -he.y, -he.z);
        Vector3 p110 = c + new Vector3( he.x, -he.y,  he.z);
        Vector3 p010 = c + new Vector3(-he.x, -he.y,  he.z);
        Vector3 p001 = c + new Vector3(-he.x,  he.y, -he.z);
        Vector3 p101 = c + new Vector3( he.x,  he.y, -he.z);
        Vector3 p111 = c + new Vector3( he.x,  he.y,  he.z);
        Vector3 p011 = c + new Vector3(-he.x,  he.y,  he.z);

        Color col = Color.cyan;
        float dur = Time.fixedDeltaTime;

        // bottom face
        Debug.DrawLine(p000, p100, col, dur);
        Debug.DrawLine(p100, p110, col, dur);
        Debug.DrawLine(p110, p010, col, dur);
        Debug.DrawLine(p010, p000, col, dur);
        // top face
        Debug.DrawLine(p001, p101, col, dur);
        Debug.DrawLine(p101, p111, col, dur);
        Debug.DrawLine(p111, p011, col, dur);
        Debug.DrawLine(p011, p001, col, dur);
        // verticals
        Debug.DrawLine(p000, p001, col, dur);
        Debug.DrawLine(p100, p101, col, dur);
        Debug.DrawLine(p110, p111, col, dur);
        Debug.DrawLine(p010, p011, col, dur);

        if (!debugDrawTaggedColliders) {
            return;
        }

        DrawTaggedColliderSet(obstacleTag, Color.red, dur);
        DrawTaggedColliderSet(wallTag, Color.yellow, dur);
    }

    private void DrawTaggedColliderSet(string tagName, Color color, float duration)
    {
        if (string.IsNullOrWhiteSpace(tagName)) {
            return;
        }

        GameObject[] tagged;
        try {
            tagged = GameObject.FindGameObjectsWithTag(tagName);
        }
        catch (UnityException) {
            return;
        }

        for (int i = 0; i < tagged.Length; i++) {
            GameObject go = tagged[i];
            if (go == null || go == gameObject) {
                continue;
            }

            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int cIdx = 0; cIdx < cols.Length; cIdx++) {
                Collider col = cols[cIdx];
                if (col == null || !col.enabled) {
                    continue;
                }

                Bounds b = col.bounds;
                Vector3 center = new Vector3(b.center.x, debugGizmoY, b.center.z);
                Vector3 halfExtents = new Vector3(
                    Mathf.Max(0.01f, b.extents.x),
                    0.03f,
                    Mathf.Max(0.01f, b.extents.z)
                );

                DrawWireBoxLines(center, halfExtents, color, duration);
            }
        }
    }

    private void DrawWireBoxLines(Vector3 center, Vector3 halfExtents, Color color, float duration)
    {
        Vector3 p000 = center + new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
        Vector3 p100 = center + new Vector3( halfExtents.x, -halfExtents.y, -halfExtents.z);
        Vector3 p110 = center + new Vector3( halfExtents.x, -halfExtents.y,  halfExtents.z);
        Vector3 p010 = center + new Vector3(-halfExtents.x, -halfExtents.y,  halfExtents.z);
        Vector3 p001 = center + new Vector3(-halfExtents.x,  halfExtents.y, -halfExtents.z);
        Vector3 p101 = center + new Vector3( halfExtents.x,  halfExtents.y, -halfExtents.z);
        Vector3 p111 = center + new Vector3( halfExtents.x,  halfExtents.y,  halfExtents.z);
        Vector3 p011 = center + new Vector3(-halfExtents.x,  halfExtents.y,  halfExtents.z);

        Debug.DrawLine(p000, p100, color, duration);
        Debug.DrawLine(p100, p110, color, duration);
        Debug.DrawLine(p110, p010, color, duration);
        Debug.DrawLine(p010, p000, color, duration);

        Debug.DrawLine(p001, p101, color, duration);
        Debug.DrawLine(p101, p111, color, duration);
        Debug.DrawLine(p111, p011, color, duration);
        Debug.DrawLine(p011, p001, color, duration);

        Debug.DrawLine(p000, p001, color, duration);
        Debug.DrawLine(p100, p101, color, duration);
        Debug.DrawLine(p110, p111, color, duration);
        Debug.DrawLine(p010, p011, color, duration);
    }
}
