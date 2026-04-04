using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Main : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera camera2D;
    [SerializeField] private Camera camera3D;

    [Header("Level Complete")]
    [SerializeField] private GameObject levelCompleteUI;

    [Header("Display Settings")]
    [SerializeField] private bool startWith2DView = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField, Range(0.1f, 0.5f)] private float previewWidth = 0.28f;
    [SerializeField, Range(0.1f, 0.5f)] private float previewHeight = 0.28f;
    [SerializeField, Range(0.0f, 0.1f)] private float previewMargin = 0.02f;

    private bool is2DPrimary;

    private void Awake()
    {
        if (levelCompleteUI != null) {
            levelCompleteUI.SetActive(false);
        }
        BakeOutlineSmoothNormals();
        ApplyTagOutlines();
        is2DPrimary = startWith2DView;
        ApplyCameraLayout();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) {
            is2DPrimary = !is2DPrimary;
            ApplyCameraLayout();
        }
    }

    private void ApplyCameraLayout()
    {
        if (camera2D == null || camera3D == null) {
            return;
        }

        Camera primaryCamera = is2DPrimary ? camera2D : camera3D;
        Camera previewCamera = is2DPrimary ? camera3D : camera2D;

        primaryCamera.enabled = true;
        previewCamera.enabled = true;

        primaryCamera.rect = new Rect(0f, 0f, 1f, 1f);
        previewCamera.rect = new Rect(
            1f - previewWidth - previewMargin,
            previewMargin,
            previewWidth,
            previewHeight
        );

        primaryCamera.depth = 0f;
        previewCamera.depth = 1f;

        SetAudioListenerState(primaryCamera, true);
        SetAudioListenerState(previewCamera, false);
    }

    private void SetAudioListenerState(Camera targetCamera, bool isEnabled)
    {
        AudioListener listener = targetCamera.GetComponent<AudioListener>();
        if (listener != null) {
            listener.enabled = isEnabled;
        }
    }

    public void ShowLevelComplete()
    {
        if (levelCompleteUI != null) {
            levelCompleteUI.SetActive(true);
        }
    }

    public void LoadNextLevel()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings) {
            SceneManager.LoadScene(nextIndex);
        }
    }

    private void ApplyTagOutlines()
    {
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        foreach (Renderer rend in allRenderers) {
            bool usesShader = false;
            foreach (Material m in rend.sharedMaterials) {
                if (m != null && m.shader != null && m.shader.name == "Custom/DoubleSidedTexture") {
                    usesShader = true;
                    break;
                }
            }
            if (!usesShader) continue;

            string tag = rend.gameObject.tag;
            Color color;
            float width;

            switch (tag) {
                case "MoveableX":   color = new Color(1.00f, 0.208f, 0.000f); width = 0.1f; break;
                case "MoveableY":   color = new Color(0.541f, 1.00f, 0.000f); width = 0.1f; break;
                case "MoveableZ":   color = new Color(0.000f, 0.561f, 1.00f); width = 0.1f; break;
                case "MoveableXY":  color = new Color(1.00f, 0.878f, 0.000f); width = 0.1f; break;
                case "MoveableXZ":  color = new Color(0.855f, 0.200f, 0.925f); width = 0.1f; break;
                case "MoveableYZ":  color = new Color(0.000f, 0.702f, 0.541f); width = 0.1f; break;
                case "MoveableXYZ": color = Color.white;                       width = 0.1f; break;
                default:            color = Color.black;                       width = 0f; break;
            }

            // Use instanced materials so shared assets are not modified.
            Material[] instanced = rend.materials;
            for (int i = 0; i < instanced.Length; i++) {
                if (instanced[i] != null && instanced[i].shader != null &&
                    instanced[i].shader.name == "Custom/DoubleSidedTexture") {
                    instanced[i].SetColor("_OutlineColor", color);
                    instanced[i].SetFloat("_OutlineWidth", width);
                }
            }
            rend.materials = instanced;
        }
    }

    private void BakeOutlineSmoothNormals()
    {
        MeshFilter[] allFilters = FindObjectsOfType<MeshFilter>();
        foreach (MeshFilter mf in allFilters) {
            if (mf.sharedMesh == null) continue;
            Renderer rend = mf.GetComponent<Renderer>();
            if (rend == null) continue;
            bool usesOutlineShader = false;
            foreach (Material mat in rend.sharedMaterials) {
                if (mat != null && mat.shader != null && mat.shader.name == "Custom/DoubleSidedTexture") {
                    usesOutlineShader = true;
                    break;
                }
            }
            if (!usesOutlineShader) continue;
            Mesh mesh = Instantiate(mf.sharedMesh);
            BakeSmoothNormalsIntoUV3(mesh);
            mf.mesh = mesh;
        }
    }

    private static void BakeSmoothNormalsIntoUV3(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals  = mesh.normals;
        Dictionary<Vector3, List<int>> groups = new Dictionary<Vector3, List<int>>();
        for (int i = 0; i < vertices.Length; i++) {
            Vector3 key = RoundVec(vertices[i]);
            if (!groups.TryGetValue(key, out List<int> g)) { g = new List<int>(); groups[key] = g; }
            g.Add(i);
        }
        Vector3[] smooth = new Vector3[vertices.Length];
        foreach (List<int> g in groups.Values) {
            Vector3 avg = Vector3.zero;
            for (int i = 0; i < g.Count; i++) avg += normals[g[i]];
            avg.Normalize();
            for (int i = 0; i < g.Count; i++) smooth[g[i]] = avg;
        }
        mesh.SetUVs(3, smooth);
    }

    private static Vector3 RoundVec(Vector3 v)
    {
        const float p = 1000f;
        return new Vector3(Mathf.Round(v.x * p) / p, Mathf.Round(v.y * p) / p, Mathf.Round(v.z * p) / p);
    }
}
