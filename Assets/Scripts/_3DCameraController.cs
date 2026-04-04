using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections.Generic;

[System.Serializable]
public struct SelectionMeshSource
{
    public Mesh mesh;
    public Matrix4x4 toSelectedLocal;
}

[System.Serializable]
public class MovableTagRule
{
    public string tagName = "MovableXYZ";
    public bool allowX = true;
    public bool allowY = true;
    public bool allowZ = true;

    public Vector3 Apply(Vector3 worldDelta)
    {
        return new Vector3(
            allowX ? worldDelta.x : 0f,
            allowY ? worldDelta.y : 0f,
            allowZ ? worldDelta.z : 0f
        );
    }
}

[RequireComponent(typeof(Camera))]
[AddComponentMenu("Camera/_3DCameraController")]
public class _3DCameraController : MonoBehaviour
{
    [Header("Orbit Target")]
    [SerializeField] private Transform orbitCenter;

    [Header("Distance")]
    [SerializeField] private float distance = 8f;
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private float zoomSpeed = 1f;
    [SerializeField] private float zoomInputDeadzone = 0.01f;
    [SerializeField] private bool clampDistanceOnStart = false;

    [Header("Rotation")]
    [SerializeField] private float yawSpeed = 180f;
    [SerializeField] private float pitchSpeed = 140f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool requireRightMouseButton = true;
    [SerializeField] private float initialYawOffset = 0f;

    [Header("Input Scope")]
    [SerializeField] private bool onlyWhenPrimaryView = false;

    [Header("Object Movement")]
    [SerializeField] private LayerMask selectionMask = ~0;
    [SerializeField] private float selectionRayDistance = 500f;
    [SerializeField] private bool deselectWhenClickEmpty = true;
    [SerializeField] private MovableTagRule[] movableTagRules = new MovableTagRule[] {
        new MovableTagRule { tagName = "MovableXYZ", allowX = true, allowY = true, allowZ = true },
        new MovableTagRule { tagName = "MovableXY", allowX = true, allowY = true, allowZ = false },
        new MovableTagRule { tagName = "MovableXZ", allowX = true, allowY = false, allowZ = true },
        new MovableTagRule { tagName = "MovableYZ", allowX = false, allowY = true, allowZ = true },
        new MovableTagRule { tagName = "MovableX", allowX = true, allowY = false, allowZ = false },
        new MovableTagRule { tagName = "MovableY", allowX = false, allowY = true, allowZ = false },
        new MovableTagRule { tagName = "MovableZ", allowX = false, allowY = false, allowZ = true }
    };

    [Header("Movement Arrows")]
    [SerializeField] private GameObject arrowXPrefab;
    [SerializeField] private GameObject arrowZPrefab;
    [SerializeField] private GameObject arrowYPrefab;
    [SerializeField] private float arrowSurfaceOffset = 0.05f;
    [SerializeField] private float arrowPivotOffset = 0f;

    [Header("Selection Visual")]
    [SerializeField] private bool showSelectionHighlight = true;
    [SerializeField] private Color selectionHighlightColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private float selectionHighlightLineWidth = 0.02f;
    [SerializeField] private int maxHighlightEdges = 1500;
    [SerializeField] private float selectionHighlightNormalOffset = 0.02f;

    private Camera attachedCamera;
    private float yaw;
    private float pitch;
    private bool hasValidOrbitCenter;
    private bool isDraggingOrbit;
    private bool isDraggingObject;
    private Vector2 lastDragMousePosition;
    private Vector3 lastDragWorldPoint;
    private Plane objectDragPlane;
    private Transform selectedObject;
    private MovableTagRule selectedTagRule;
    private GameObject selectionHighlightRoot;
    private LineRenderer[] selectionHighlightLines;
    private Transform highlightedObject;
    private Material selectionHighlightMaterial;
    private readonly List<GameObject> spawnedArrows = new List<GameObject>();
    private GameObject arrowHolder;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        hasValidOrbitCenter = ValidateOrbitCenter();
        if (!hasValidOrbitCenter) {
            enabled = false;
            return;
        }

        // Preserve the camera's scene placement by deriving orbit angles from position relative to orbit center.
        Vector3 offset = transform.position - orbitCenter.position;
        distance = offset.magnitude;

        if (distance > 0.0001f) {
            Vector3 dir = offset / distance;
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        } else {
            Vector3 startEuler = transform.rotation.eulerAngles;
            yaw = startEuler.y;
            pitch = NormalizePitch(startEuler.x);
            distance = Mathf.Max(minDistance, 0.01f);
        }

        yaw += initialYawOffset;

        if (clampDistanceOnStart) {
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        UpdateCameraTransform();
    }

    private void LateUpdate()
    {
        if (!hasValidOrbitCenter || orbitCenter == null) {
            return;
        }

        if (onlyWhenPrimaryView && !IsPrimaryView()) {
            UpdateCameraTransform();
            return;
        }

        HandleObjectSelectionAndMovement();
        UpdateSelectionVisual();
        HandleRotationInput();
        HandleZoomInput();

        UpdateCameraTransform();
    }

    private void OnDisable()
    {
        DespawnArrows();

        if (selectionHighlightRoot != null) {
            Destroy(selectionHighlightRoot);
            selectionHighlightRoot = null;
            selectionHighlightLines = null;
        }

        if (selectionHighlightMaterial != null) {
            Destroy(selectionHighlightMaterial);
            selectionHighlightMaterial = null;
        }
    }

    private void HandleObjectSelectionAndMovement()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            ClearSelection();
        }

        if (GetLeftMouseDown()) {
            TrySelectObjectUnderCursor();
            BeginObjectDragIfPossible();
        }

        if (GetLeftMouseHeld() && isDraggingObject) {
            DragSelectedObject();
        }

        if (GetLeftMouseUp()) {
            isDraggingObject = false;
        }
    }

    private void TrySelectObjectUnderCursor()
    {
        Ray ray = attachedCamera.ScreenPointToRay(GetMousePosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, selectionRayDistance, selectionMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) {
            if (deselectWhenClickEmpty) {
                ClearSelection();
            }
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits) {
            if (TryFindMovableTarget(hit.transform, out Transform movableTarget, out MovableTagRule tagRule)) {
                selectedObject = movableTarget;
                selectedTagRule = tagRule;
                SpawnArrows(movableTarget, tagRule);
                return;
            }
        }

        if (deselectWhenClickEmpty) {
            ClearSelection();
        }
    }

    private void BeginObjectDragIfPossible()
    {
        if (selectedObject == null || selectedTagRule == null) {
            isDraggingObject = false;
            return;
        }

        objectDragPlane = new Plane(-transform.forward, selectedObject.position);
        if (TryGetMousePointOnPlane(objectDragPlane, out Vector3 startWorldPoint)) {
            isDraggingObject = true;
            lastDragWorldPoint = startWorldPoint;
        } else {
            isDraggingObject = false;
        }
    }

    private void DragSelectedObject()
    {
        if (selectedObject == null || selectedTagRule == null) {
            isDraggingObject = false;
            return;
        }

        if (!TryGetMousePointOnPlane(objectDragPlane, out Vector3 currentWorldPoint)) {
            return;
        }

        Vector3 rawDelta = currentWorldPoint - lastDragWorldPoint;
        Vector3 constrainedDelta = selectedTagRule.Apply(rawDelta);
        Vector3 newPos = selectedObject.position + constrainedDelta;
        MovementBounds bounds = selectedObject.GetComponent<MovementBounds>();
        if (bounds != null) newPos = bounds.Clamp(newPos);
        constrainedDelta = newPos - selectedObject.position;
        selectedObject.position = newPos;
        if (arrowHolder != null) {
            arrowHolder.transform.position += constrainedDelta;
        }
        lastDragWorldPoint += constrainedDelta;
    }

    private bool TryFindMovableTarget(Transform hitTransform, out Transform movableTarget, out MovableTagRule tagRule)
    {
        Transform current = hitTransform;
        while (current != null) {
            if (TryGetTagRuleFor(current.gameObject, out tagRule)) {
                movableTarget = current;
                return true;
            }
            current = current.parent;
        }

        movableTarget = null;
        tagRule = null;
        return false;
    }

    private bool TryGetTagRuleFor(GameObject target, out MovableTagRule tagRule)
    {
        string objectTag = target.tag;
        string normalizedObjectTag = NormalizeMovableTagName(objectTag);
        for (int i = 0; i < movableTagRules.Length; i++) {
            MovableTagRule rule = movableTagRules[i];
            if (rule == null || string.IsNullOrWhiteSpace(rule.tagName)) {
                continue;
            }
            string normalizedRuleTag = NormalizeMovableTagName(rule.tagName);
            if (objectTag == rule.tagName || normalizedObjectTag == normalizedRuleTag) {
                tagRule = rule;
                return true;
            }
        }

        tagRule = null;
        return false;
    }

    private static string NormalizeMovableTagName(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) {
            return string.Empty;
        }

        if (tagName.StartsWith("Moveable")) {
            return "Movable" + tagName.Substring("Moveable".Length);
        }

        return tagName;
    }

    private bool TryGetMousePointOnPlane(Plane plane, out Vector3 worldPoint)
    {
        Ray ray = attachedCamera.ScreenPointToRay(GetMousePosition());
        if (plane.Raycast(ray, out float enterDistance)) {
            worldPoint = ray.GetPoint(enterDistance);
            return true;
        }

        worldPoint = Vector3.zero;
        return false;
    }

    private void SpawnArrows(Transform target, MovableTagRule rule)
    {
        DespawnArrows();

        arrowHolder = new GameObject("ArrowHolder");
        arrowHolder.hideFlags = HideFlags.DontSave;

        Bounds bounds = new Bounds(target.position, Vector3.zero);
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers) {
            bounds.Encapsulate(r.bounds);
        }

        if (rule.allowX) {
            SpawnArrowOnSurface(arrowXPrefab, target, bounds, Vector3.right,    Vector3.right);
            SpawnArrowOnSurface(arrowXPrefab, target, bounds, Vector3.left,     Vector3.right);
        }
        if (rule.allowZ) {
            SpawnArrowOnSurface(arrowZPrefab, target, bounds, Vector3.forward,  Vector3.forward);
            SpawnArrowOnSurface(arrowZPrefab, target, bounds, Vector3.back,     Vector3.forward);
        }
        if (rule.allowY) {
            SpawnArrowOnSurface(arrowYPrefab, target, bounds, Vector3.up,       Vector3.up);
            SpawnArrowOnSurface(arrowYPrefab, target, bounds, Vector3.down,     Vector3.up);
        }
    }

    private void SpawnArrowOnSurface(GameObject prefab, Transform target, Bounds bounds, Vector3 worldDir, Vector3 prefabNaturalDir)
    {
        if (prefab == null) {
            return;
        }

        float extent = Vector3.Dot(bounds.extents, new Vector3(
            Mathf.Abs(worldDir.x), Mathf.Abs(worldDir.y), Mathf.Abs(worldDir.z)));
        Vector3 rayOrigin = bounds.center + worldDir * (extent + 0.5f);
        Ray ray = new Ray(rayOrigin, -worldDir);

        Vector3 spawnPoint;
        RaycastHit[] hits = Physics.RaycastAll(ray, extent + 1f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        bool foundSurface = false;
        for (int i = 0; i < hits.Length; i++) {
            if (IsPartOfTarget(hits[i].collider, target)) {
                spawnPoint = hits[i].point + worldDir * (arrowSurfaceOffset + arrowPivotOffset);
                foundSurface = true;
                Quaternion rotation = Quaternion.FromToRotation(prefabNaturalDir, worldDir) * prefab.transform.rotation;
                GameObject arrow = Instantiate(prefab, spawnPoint, rotation);
                if (arrowHolder != null) arrow.transform.SetParent(arrowHolder.transform, true);
                spawnedArrows.Add(arrow);
                return;
            }
        }
        if (!foundSurface) {
            spawnPoint = bounds.center + worldDir * (extent + arrowSurfaceOffset + arrowPivotOffset);
            Quaternion rotation = Quaternion.FromToRotation(prefabNaturalDir, worldDir) * prefab.transform.rotation;
            GameObject arrow = Instantiate(prefab, spawnPoint, rotation);
            if (arrowHolder != null) arrow.transform.SetParent(arrowHolder.transform, true);
            spawnedArrows.Add(arrow);
        }
    }

    private void DespawnArrows()
    {
        for (int i = 0; i < spawnedArrows.Count; i++) {
            if (spawnedArrows[i] != null) {
                DestroyImmediate(spawnedArrows[i]);
            }
        }
        spawnedArrows.Clear();
        if (arrowHolder != null) {
            DestroyImmediate(arrowHolder);
            arrowHolder = null;
        }
    }

    private void ClearSelection()
    {
        selectedObject = null;
        selectedTagRule = null;
        isDraggingObject = false;
        DespawnArrows();
    }

    private void UpdateSelectionVisual()
    {
        if (!showSelectionHighlight || selectedObject == null) {
            if (selectionHighlightRoot != null) {
                selectionHighlightRoot.SetActive(false);
            }
            highlightedObject = null;
            return;
        }

        List<SelectionMeshSource> meshSources = GatherSelectionMeshes(selectedObject);
        if (meshSources.Count == 0) {
            if (selectionHighlightRoot != null) {
                selectionHighlightRoot.SetActive(false);
            }
            highlightedObject = null;
            return;
        }

        if (selectionHighlightRoot == null || highlightedObject != selectedObject) {
            BuildMeshSelectionHighlight(selectedObject, meshSources);
        }

        if (selectionHighlightRoot == null || selectionHighlightLines == null) {
            return;
        }

        selectionHighlightRoot.SetActive(true);

        for (int i = 0; i < selectionHighlightLines.Length; i++) {
            if (selectionHighlightLines[i] == null) continue;
            selectionHighlightLines[i].startWidth = selectionHighlightLineWidth;
            selectionHighlightLines[i].endWidth = selectionHighlightLineWidth;
            Color opaqueColor = new Color(selectionHighlightColor.r, selectionHighlightColor.g, selectionHighlightColor.b, 1f);
            selectionHighlightLines[i].startColor = opaqueColor;
            selectionHighlightLines[i].endColor = opaqueColor;
            selectionHighlightLines[i].material.color = opaqueColor;
        }
    }

    private void BuildMeshSelectionHighlight(Transform target, List<SelectionMeshSource> meshSources)
    {
        if (selectionHighlightRoot != null) {
            Destroy(selectionHighlightRoot);
        }

        selectionHighlightRoot = new GameObject("SelectionHighlightWire");
        selectionHighlightRoot.hideFlags = HideFlags.DontSave;

        selectionHighlightRoot.transform.SetParent(target, false);
        selectionHighlightRoot.transform.localPosition = Vector3.zero;
        selectionHighlightRoot.transform.localRotation = Quaternion.identity;
        selectionHighlightRoot.transform.localScale = Vector3.one;

        List<(Vector3, Vector3)> allEdges = new List<(Vector3, Vector3)>();
        for (int sourceIndex = 0; sourceIndex < meshSources.Count; sourceIndex++) {
            if (allEdges.Count >= maxHighlightEdges) {
                break;
            }

            SelectionMeshSource source = meshSources[sourceIndex];
            Mesh mesh = source.mesh;
            if (mesh == null) {
                continue;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            bool hasNormals = normals != null && normals.Length == vertices.Length;
            List<Vector2Int> edges = ExtractUniqueEdges(mesh, maxHighlightEdges - allEdges.Count);

            for (int i = 0; i < edges.Count; i++) {
                Vector2Int e = edges[i];
                Vector3 v0 = vertices[e.x];
                Vector3 v1 = vertices[e.y];

                if (selectionHighlightNormalOffset > 0f && hasNormals) {
                    v0 += normals[e.x] * selectionHighlightNormalOffset;
                    v1 += normals[e.y] * selectionHighlightNormalOffset;
                }

                v0 = source.toSelectedLocal.MultiplyPoint3x4(v0);
                v1 = source.toSelectedLocal.MultiplyPoint3x4(v1);
                allEdges.Add((v0, v1));

                if (allEdges.Count >= maxHighlightEdges) {
                    break;
                }
            }
        }

        selectionHighlightLines = new LineRenderer[allEdges.Count];
        for (int i = 0; i < allEdges.Count; i++) {
            GameObject edge = new GameObject("Edge_" + i);
            edge.transform.SetParent(selectionHighlightRoot.transform, false);

            LineRenderer lr = edge.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.positionCount = 2;
            lr.startWidth = selectionHighlightLineWidth;
            lr.endWidth = selectionHighlightLineWidth;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            EnsureSelectionHighlightMaterial();
            lr.material = selectionHighlightMaterial;
            Color opaqueColor = new Color(selectionHighlightColor.r, selectionHighlightColor.g, selectionHighlightColor.b, 1f);
            lr.startColor = opaqueColor;
            lr.endColor = opaqueColor;
            lr.material.color = opaqueColor;

            lr.SetPosition(0, allEdges[i].Item1);
            lr.SetPosition(1, allEdges[i].Item2);

            selectionHighlightLines[i] = lr;
        }

        highlightedObject = target;
    }

    private void EnsureSelectionHighlightMaterial()
    {
        if (selectionHighlightMaterial != null) {
            return;
        }

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) {
            shader = Shader.Find("Standard");
        }

        selectionHighlightMaterial = new Material(shader);
        Color opaqueColor = new Color(selectionHighlightColor.r, selectionHighlightColor.g, selectionHighlightColor.b, 1f);
        selectionHighlightMaterial.color = opaqueColor;
    }

    private List<SelectionMeshSource> GatherSelectionMeshes(Transform selectedRoot)
    {
        List<SelectionMeshSource> sources = new List<SelectionMeshSource>();
        Transform[] transforms = selectedRoot.GetComponentsInChildren<Transform>(true);
        Matrix4x4 worldToSelected = selectedRoot.worldToLocalMatrix;

        for (int i = 0; i < transforms.Length; i++) {
            Transform t = transforms[i];
            if (t == null) {
                continue;
            }

            // Skip meshes belonging to spawned arrows.
            bool isArrow = false;
            for (int j = 0; j < spawnedArrows.Count; j++) {
                if (spawnedArrows[j] != null && (t == spawnedArrows[j].transform || t.IsChildOf(spawnedArrows[j].transform))) {
                    isArrow = true;
                    break;
                }
            }
            if (isArrow) {
                continue;
            }

            Mesh sourceMesh = null;
            MeshCollider meshCollider = t.GetComponent<MeshCollider>();
            if (meshCollider != null && meshCollider.sharedMesh != null) {
                sourceMesh = meshCollider.sharedMesh;
            } else {
                MeshFilter meshFilter = t.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null) {
                    sourceMesh = meshFilter.sharedMesh;
                }
            }

            if (sourceMesh == null) {
                continue;
            }

            SelectionMeshSource source = new SelectionMeshSource {
                mesh = sourceMesh,
                toSelectedLocal = worldToSelected * t.localToWorldMatrix
            };
            sources.Add(source);
        }

        return sources;
    }

    private List<Vector2Int> ExtractUniqueEdges(Mesh mesh, int edgeLimit)
    {
        int[] triangles = mesh.triangles;
        HashSet<EdgeKey> uniqueEdges = new HashSet<EdgeKey>();
        List<Vector2Int> result = new List<Vector2Int>();

        for (int i = 0; i <= triangles.Length - 3; i += 3) {
            if (TryAddEdge(triangles[i], triangles[i + 1], uniqueEdges, result, edgeLimit)) break;
            if (TryAddEdge(triangles[i + 1], triangles[i + 2], uniqueEdges, result, edgeLimit)) break;
            if (TryAddEdge(triangles[i + 2], triangles[i], uniqueEdges, result, edgeLimit)) break;
        }

        return result;
    }

    private static bool IsPartOfTarget(Collider col, Transform target)
    {
        Transform t = col.transform;
        while (t != null) {
            if (t == target) return true;
            t = t.parent;
        }
        return false;
    }

    private static bool TryAddEdge(int a, int b, HashSet<EdgeKey> set, List<Vector2Int> result, int limit)
    {
        EdgeKey edge = new EdgeKey(a, b);
        if (set.Add(edge)) {
            result.Add(new Vector2Int(edge.a, edge.b));
            if (result.Count >= limit) {
                return true;
            }
        }
        return false;
    }

    private struct EdgeKey
    {
        public readonly int a;
        public readonly int b;

        public EdgeKey(int i0, int i1)
        {
            if (i0 < i1) {
                a = i0;
                b = i1;
            } else {
                a = i1;
                b = i0;
            }
        }

        public override int GetHashCode()
        {
            return (a * 397) ^ b;
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey other && a == other.a && b == other.b;
        }
    }

    private void HandleRotationInput()
    {
        if (requireRightMouseButton && !IsOrbitDragHeld()) {
            isDraggingOrbit = false;
            return;
        }

        Vector2 currentMousePosition = GetMousePosition();
        if (!isDraggingOrbit) {
            // Avoid initial jump when starting a drag from a different cursor position.
            isDraggingOrbit = true;
            lastDragMousePosition = currentMousePosition;
            return;
        }

        Vector2 lookDelta = currentMousePosition - lastDragMousePosition;
        lastDragMousePosition = currentMousePosition;
        if (lookDelta.sqrMagnitude <= 0f) {
            return;
        }

        yaw += lookDelta.x * yawSpeed * 0.01f;
        pitch -= lookDelta.y * pitchSpeed * 0.01f;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleZoomInput()
    {
        float scroll = GetScrollDelta();
        if (Mathf.Abs(scroll) < zoomInputDeadzone) {
            return;
        }

        float zoomStep = Mathf.Clamp(scroll, -1f, 1f) * zoomSpeed;
        distance -= zoomStep;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    private bool ValidateOrbitCenter()
    {
        if (orbitCenter == null) {
            Debug.LogWarning("_3DCameraController: Orbit Center is not assigned.", this);
            return false;
        }

        if (orbitCenter == transform || orbitCenter.IsChildOf(transform)) {
            Debug.LogError("_3DCameraController: Orbit Center cannot be this camera or a child of it. Use a separate stationary object in the scene.", this);
            return false;
        }

        return true;
    }

    private bool IsOrbitDragHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null) {
            return mouse.rightButton.isPressed;
        }
#endif
        return Input.GetMouseButton(1);
    }

    private bool GetLeftMouseDown()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null) {
            return mouse.leftButton.wasPressedThisFrame;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }

    private bool GetLeftMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null) {
            return mouse.leftButton.isPressed;
        }
#endif
        return Input.GetMouseButton(0);
    }

    private bool GetLeftMouseUp()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null) {
            return mouse.leftButton.wasReleasedThisFrame;
        }
#endif
        return Input.GetMouseButtonUp(0);
    }

    private Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null) {
            return mouse.position.ReadValue();
        }
#endif
        return Input.mousePosition;
    }

    private float GetScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null) {
            Vector2 scroll = mouse.scroll.ReadValue();
            return scroll.y / 120f;
        }
#endif
        return Input.mouseScrollDelta.y;
    }

    private void UpdateCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 forward = rotation * Vector3.forward;

        transform.position = orbitCenter.position - forward * distance;
        transform.rotation = rotation;
    }

    private bool IsPrimaryView()
    {
        Rect r = attachedCamera.rect;
        return r.width >= 0.99f && r.height >= 0.99f;
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.001f;
    }

    private static float NormalizePitch(float rawPitch)
    {
        return rawPitch > 180f ? rawPitch - 360f : rawPitch;
    }
}