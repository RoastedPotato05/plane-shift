using UnityEngine;
using UnityEditor;

public class BakeScaleEditor
{
    [MenuItem("GameObject/Bake Scale into Mesh", false, 1)]
    static void BakeScale()
    {
        GameObject go = Selection.activeGameObject;

        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Bake Scale", "Selected object must have a MeshFilter with a mesh.", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject("Save Baked Mesh", go.name + "_baked", "asset", "Choose where to save the baked mesh");
        if (string.IsNullOrEmpty(path))
            return;

        Mesh original = mf.sharedMesh;
        Mesh baked = new Mesh();
        baked.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        // Bake the local rotation and scale into vertex positions, normals, and tangents
        Matrix4x4 bakeMatrix = Matrix4x4.TRS(Vector3.zero, go.transform.localRotation, go.transform.localScale);

        Vector3[] verts = original.vertices;
        for (int i = 0; i < verts.Length; i++)
            verts[i] = bakeMatrix.MultiplyPoint3x4(verts[i]);

        // Normals need the inverse-transpose to stay correct after non-uniform scale
        Matrix4x4 normalMatrix = bakeMatrix.inverse.transpose;
        Vector3[] normals = original.normals;
        for (int i = 0; i < normals.Length; i++)
            normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;

        Vector4[] tangents = original.tangents;
        for (int i = 0; i < tangents.Length; i++)
        {
            Vector3 t = normalMatrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
            tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
        }

        baked.vertices = verts;
        baked.normals = normals;
        baked.tangents = tangents;
        baked.uv = original.uv;
        baked.uv2 = original.uv2;
        baked.colors = original.colors;
        baked.subMeshCount = original.subMeshCount;
        for (int i = 0; i < original.subMeshCount; i++)
            baked.SetTriangles(original.GetTriangles(i), i);

        baked.RecalculateBounds();

        AssetDatabase.CreateAsset(baked, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(mf, "Bake Scale into Mesh");
        Undo.RecordObject(go.transform, "Bake Scale into Mesh");

        mf.sharedMesh = baked;
        go.transform.localScale = Vector3.one;
        go.transform.localRotation = Quaternion.identity;

        // Also update MeshCollider if present
        MeshCollider mc = go.GetComponent<MeshCollider>();
        if (mc != null)
        {
            Undo.RecordObject(mc, "Bake Scale into Mesh");
            mc.sharedMesh = baked;
        }

        Debug.Log($"Scale and rotation baked into '{path}'. Object scale reset to (1,1,1) and rotation to identity.");
    }

    [MenuItem("GameObject/Bake Scale into Mesh", true)]
    static bool ValidateBakeScale()
    {
        return Selection.activeGameObject != null
            && Selection.activeGameObject.GetComponent<MeshFilter>() != null;
    }
}
