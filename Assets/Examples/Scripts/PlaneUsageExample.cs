using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EzySlice;

/**
 * This class is an example of how to setup a cutting Plane from a GameObject
 * and how to work with coordinate systems.
 * 
 * When a Place slices a Mesh, the Mesh is in local coordinates whilst the Plane
 * is in world coordinates. The first step is to bring the Plane into the coordinate system
 * of the mesh we want to slice. This script shows how to do that.
 */
[ExecuteAlways]
public class PlaneUsageExample : MonoBehaviour {	
	private const string LiveCrossSectionName = "Live_CrossSection";
	private static readonly string[] FallbackIgnoreTags = { "SlicerIgnore", "IgnoreSlicer" };

	[Header("Live Cross-Section Preview")]
	
	public bool autoLive = false;
	public bool showLiveInEditMode = false;
	// public GameObject source; // removed, now using sliceTargets exclusively
	public GameObject targetDisplayPlane; // assign your Display plane here
	public Material crossSectionMaterial;
	public Vector3 liveCrossPosition = new Vector3(0, 3, 0);
	public Vector3 liveCrossEuler = new Vector3(0, 0, 0);
	public Vector3 liveCrossScale = Vector3.one;

	[Header("Slice Targets")]
	public GameObject[] sliceTargets; // assign objects to include in the live cross-section here

	[Header("Filters")]
	public string ignoreSliceTag = "SlicerIgnore";

	[Header("Diagnostics")]
	public bool logSliceDiagnostics = false;

	[Header("Generated Cross-Section Collision")]
	public bool markCrossSectionsAsObstacle = true;
	public string generatedObstacleTag = "Obstacle";
	public bool addColliderToCrossSections = true;
	public float minGeneratedColliderThickness = 0.15f;

	private List<GameObject> liveCrossSections = new List<GameObject>();
	private HashSet<string> loggedDiagnosticKeys = new HashSet<string>();

	private void Awake() {
		DestroyStaleLiveCrossSections();
	}

	private void OnDisable() {
		DestroyLiveCrossSections();
	}

	private void Update() {
		if (!Application.isPlaying && IsInPrefabEditingMode()) {
			DestroyLiveCrossSections();
			return;
		}

		if (Application.isPlaying) {
			if (autoLive) {
				RefreshLiveCrossSections();
			} else {
				DestroyLiveCrossSections();
			}
			return;
		}

		if (showLiveInEditMode) {
			RefreshLiveCrossSections();
		} else {
			DestroyLiveCrossSections();
		}
	}

	private void RefreshLiveCrossSections() {
		DestroyLiveCrossSections();
		loggedDiagnosticKeys.Clear();
		BoxCollider slicerBox = GetComponent<BoxCollider>();
		BoxCollider targetBox = targetDisplayPlane != null ? targetDisplayPlane.GetComponent<BoxCollider>() : null;
		Bounds? boxBounds = slicerBox != null ? slicerBox.bounds : (Bounds?)null;
		Vector3 planePoint = transform.position;
		Vector3 planeNormal = transform.up;
		foreach (MeshFilter mf in Object.FindObjectsOfType<MeshFilter>()) {
			if (mf == null || mf.gameObject == this.gameObject) continue;
			GameObject obj = mf.gameObject;
			if (obj.name == LiveCrossSectionName) continue;
			if (ShouldIgnoreObject(obj)) continue;
			if (!obj.activeInHierarchy) continue;
			Renderer rend = obj.GetComponent<Renderer>();
			if (rend == null) continue;
			MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
			if (meshFilter == null || meshFilter.sharedMesh == null) {
				LogDiagnosticOnce(obj, "Missing MeshFilter/sharedMesh on candidate object.");
				continue;
			}
			if (!meshFilter.sharedMesh.isReadable) {
				LogDiagnosticOnce(obj, "Mesh is not readable. Enable Read/Write on the model import settings.");
				continue;
			}
			if (!BoundsIntersectsPlane(rend.bounds, planePoint, planeNormal)) continue;
			if (boxBounds != null && !boxBounds.Value.Intersects(rend.bounds)) continue;
			// Build a per-object cross-section material: use crossSectionMaterial's shader
			// but inherit the object's own _Color so the cut face matches the object.
			Material mat;
			if (crossSectionMaterial != null) {
				mat = new Material(crossSectionMaterial);
				Material objMat = rend.sharedMaterial;
				if (objMat != null && objMat.HasProperty("_Color")) {
					mat.color = objMat.color;
				}
			} else {
				mat = rend.sharedMaterial;
			}
			SlicedHull slice = obj.Slice(transform.position, transform.up, mat);
			if (slice == null) {
				LogDiagnosticOnce(obj, "Slice returned null. Common causes: mesh is not closed/manifold, invalid topology, or not intersecting as expected.");
				continue;
			}
			GameObject cross = slice.CreateCrossSection(obj, mat);
			if (cross == null) {
				LogDiagnosticOnce(obj, "Cross section creation returned null after successful slice.");
				continue;
			}
			cross.name = LiveCrossSectionName;
			Vector3 crossPos = liveCrossPosition;
			if (slicerBox != null && targetBox != null) {
				Vector3 objWorld = obj.GetComponent<Renderer>().bounds.center;
				Bounds slicerBounds = slicerBox.bounds;
				Bounds targetBounds = targetBox.bounds;
				Vector3 rel = new Vector3(
					Mathf.InverseLerp(slicerBounds.min.x, slicerBounds.max.x, objWorld.x),
					Mathf.InverseLerp(slicerBounds.min.y, slicerBounds.max.y, objWorld.y),
					Mathf.InverseLerp(slicerBounds.min.z, slicerBounds.max.z, objWorld.z)
				);
				Vector3 crossWorld = new Vector3(
					Mathf.Lerp(targetBounds.min.x, targetBounds.max.x, rel.x),
					Mathf.Lerp(targetBounds.min.y, targetBounds.max.y, rel.y),
					Mathf.Lerp(targetBounds.min.z, targetBounds.max.z, rel.z)
				);
				crossPos = crossWorld + liveCrossPosition;
			}
			cross.transform.position = crossPos;
			Vector3 srcEuler = obj.transform.rotation.eulerAngles;
			Vector3 crossEuler = liveCrossEuler;
			crossEuler.x += srcEuler.x;
			crossEuler.y += srcEuler.y;
			crossEuler.z += srcEuler.z;
			cross.transform.rotation = Quaternion.Euler(crossEuler);
			// Scale cross-section to match object's local scale, with user multiplier
			Vector3 naturalScale = obj.transform.localScale;
			cross.transform.localScale = new Vector3(
				naturalScale.x * liveCrossScale.x,
				naturalScale.y * liveCrossScale.y,
				naturalScale.z * liveCrossScale.z
			);
			cross.transform.SetParent(null, true);
			ConfigureGeneratedCrossSection(cross);
			liveCrossSections.Add(cross);
		}
	}
	

	private void DestroyLiveCrossSections() {
		if (liveCrossSections == null || liveCrossSections.Count == 0) return;
		foreach (var cross in liveCrossSections) {
			if (cross == null) continue;
			if (Application.isPlaying) GameObject.Destroy(cross);
			else GameObject.DestroyImmediate(cross);
		}
		liveCrossSections.Clear();
	}

	public void ResetLiveCrossSection() {
		DestroyLiveCrossSections();
		DestroyStaleLiveCrossSections();
	}

	private void DestroyStaleLiveCrossSections() {
		GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
		foreach (GameObject obj in allObjects) {
			if (obj == null || obj.name != LiveCrossSectionName) continue;
			if (Application.isPlaying) GameObject.Destroy(obj);
			else GameObject.DestroyImmediate(obj);
		}
	}

	private bool ShouldIgnoreObject(GameObject obj) {
		if (obj == null) {
			return false;
		}

		Transform current = obj.transform;
		while (current != null) {
			if (TagMatchesIgnore(current.gameObject.tag)) {
				return true;
			}
			current = current.parent;
		}

		return false;
	}

	private bool TagMatchesIgnore(string objectTag) {
		if (string.IsNullOrWhiteSpace(objectTag)) {
			return false;
		}

		if (!string.IsNullOrWhiteSpace(ignoreSliceTag)
			&& string.Equals(objectTag, ignoreSliceTag, System.StringComparison.OrdinalIgnoreCase)) {
			return true;
		}

		for (int i = 0; i < FallbackIgnoreTags.Length; i++) {
			if (string.Equals(objectTag, FallbackIgnoreTags[i], System.StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
		}

		return false;
	}

	private bool BoundsIntersectsPlane(Bounds bounds, Vector3 planePoint, Vector3 planeNormal) {
		Vector3 center = bounds.center;
		Vector3 extents = bounds.extents;

		float r = extents.x * Mathf.Abs(planeNormal.x) + extents.y * Mathf.Abs(planeNormal.y) + extents.z * Mathf.Abs(planeNormal.z);
		float s = Vector3.Dot(planeNormal, center - planePoint);

		return Mathf.Abs(s) <= r;
	}

	private void ConfigureGeneratedCrossSection(GameObject cross) {
		if (cross == null) {
			return;
		}

		if (markCrossSectionsAsObstacle) {
			TryAssignTag(cross, generatedObstacleTag);
		}

		if (!addColliderToCrossSections) {
			return;
		}

		DestroyExistingGeneratedColliders(cross);

		if (TrySetupMeshCollider(cross)) {
			return;
		}

		BoxCollider collider = cross.GetComponent<BoxCollider>();
		if (collider == null) {
			collider = cross.AddComponent<BoxCollider>();
		}

		collider.isTrigger = false;

		MeshFilter filter = cross.GetComponent<MeshFilter>();
		if (filter == null || filter.sharedMesh == null) {
			return;
		}

		Bounds meshBounds = filter.sharedMesh.bounds;
		Vector3 size = meshBounds.size;
		size.y = Mathf.Max(size.y, minGeneratedColliderThickness);
		collider.center = meshBounds.center;
		collider.size = size;
	}

	private bool TrySetupMeshCollider(GameObject cross) {
		MeshFilter filter = cross.GetComponent<MeshFilter>();
		if (filter == null || filter.sharedMesh == null) {
			return false;
		}

		MeshCollider meshCollider = cross.GetComponent<MeshCollider>();
		if (meshCollider == null) {
			meshCollider = cross.AddComponent<MeshCollider>();
		}

		meshCollider.sharedMesh = filter.sharedMesh;
		meshCollider.convex = false;
		meshCollider.isTrigger = false;
		return true;
	}

	private void DestroyExistingGeneratedColliders(GameObject cross) {
		Collider[] existing = cross.GetComponents<Collider>();
		for (int i = 0; i < existing.Length; i++) {
			if (Application.isPlaying) {
				Destroy(existing[i]);
			} else {
				DestroyImmediate(existing[i]);
			}
		}
	}

	private void TryAssignTag(GameObject obj, string tagName) {
		if (obj == null || string.IsNullOrWhiteSpace(tagName)) {
			return;
		}

		try {
			obj.tag = tagName;
		}
		catch (UnityException) {
			LogDiagnosticOnce(obj, "Tag '" + tagName + "' is not defined in Project Settings > Tags and Layers.");
		}
	}

	private void LogDiagnosticOnce(GameObject obj, string reason) {
		if (!logSliceDiagnostics || obj == null) {
			return;
		}

		string key = obj.GetInstanceID().ToString() + "|" + reason;
		if (!loggedDiagnosticKeys.Add(key)) {
			return;
		}

		Debug.LogWarning("PlaneUsageExample: Skipping object '" + obj.name + "'. " + reason, obj);
	}

	private bool IsInPrefabEditingMode() {
		#if UNITY_EDITOR
		return UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null;
		#else
		return false;
		#endif
	}

	#if UNITY_EDITOR
	/**
	 * This is for Visual debugging purposes in the editor 
	 */
	public void OnDrawGizmos() {
		EzySlice.Plane cuttingPlane = new EzySlice.Plane();
		cuttingPlane.Compute(transform);

		Vector3 gizmoSize = new Vector3(1.0f, 0.0f, 1.0f);
		BoxCollider box = GetComponent<BoxCollider>();
		if (box != null) {
			gizmoSize = box.size;
		}
		cuttingPlane.OnDebugDraw(gizmoSize);
	}

	#endif
}
