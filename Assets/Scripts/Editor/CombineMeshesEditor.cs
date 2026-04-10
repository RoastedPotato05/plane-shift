using UnityEngine;
using UnityEditor;

public class CombineMeshesEditor
{
    [MenuItem("GameObject/Combine Meshes into One", false, 0)]
    static void CombineMeshes()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length < 2)
        {
            EditorUtility.DisplayDialog("Combine Meshes", "Select at least 2 GameObjects to combine.", "OK");
            return;
        }

        // Collect all MeshFilters from selected objects and their children
        var combineInstances = new System.Collections.Generic.List<CombineInstance>();
        var firstTransform = selected[0].transform;

        foreach (GameObject go in selected)
        {
            MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                CombineInstance ci = new CombineInstance();
                ci.mesh = mf.sharedMesh;
                // Transform each mesh into the local space of the first selected object
                ci.transform = firstTransform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                combineInstances.Add(ci);
            }
        }

        if (combineInstances.Count == 0)
        {
            EditorUtility.DisplayDialog("Combine Meshes", "No meshes found in the selected GameObjects.", "OK");
            return;
        } 

        // Build the combined mesh
        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // supports large meshes
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);
        combinedMesh.RecalculateBounds();
        combinedMesh.RecalculateNormals();

        // Create a new GameObject at the position/rotation of the first selected object
        GameObject result = new GameObject("CombinedMesh");
        result.transform.position = firstTransform.position;
        result.transform.rotation = firstTransform.rotation;
        result.transform.localScale = Vector3.one;

        MeshFilter resultFilter = result.AddComponent<MeshFilter>();
        resultFilter.sharedMesh = combinedMesh;

        MeshRenderer resultRenderer = result.AddComponent<MeshRenderer>();
        // Use the material from the first MeshRenderer found
        MeshRenderer firstRenderer = selected[0].GetComponentInChildren<MeshRenderer>();
        if (firstRenderer != null)
            resultRenderer.sharedMaterials = firstRenderer.sharedMaterials;

        // Save the mesh as an asset so it persists
        string path = EditorUtility.SaveFilePanelInProject("Save Combined Mesh", "CombinedMesh", "asset", "Choose where to save the combined mesh");
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(combinedMesh, path);
            AssetDatabase.SaveAssets();
        }

        Undo.RegisterCreatedObjectUndo(result, "Combine Meshes");
        Selection.activeGameObject = result;

        Debug.Log($"Combined {combineInstances.Count} mesh(es) into '{result.name}'.");
    }

    [MenuItem("GameObject/Combine Meshes into One", true)]
    static bool ValidateCombineMeshes()
    {
        return Selection.gameObjects.Length >= 2;
    }
}
