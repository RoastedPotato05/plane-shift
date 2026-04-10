using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera camera2D;
    [SerializeField] private Camera camera3D;

    [Header("Level Complete")]
    [SerializeField] private GameObject levelCompleteUI;

    [Header("Level Fail")]
    [SerializeField] private GameObject levelFailUI;

    [Header("Display Settings")]
    [SerializeField] private bool startWith2DView = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField, Range(0.1f, 0.5f)] private float previewWidth = 0.28f;
    [SerializeField, Range(0.1f, 0.5f)] private float previewHeight = 0.28f;
    [SerializeField, Range(0.0f, 0.1f)] private float previewMargin = 0.02f;
    [SerializeField, Range(0f, 1f)] private float previewAlpha = 0.75f;

    private bool is2DPrimary;
    private RenderTexture previewRT;
    private Camera currentPreviewCamera;
    private Canvas previewCanvas;
    private RawImage previewImage;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        if (levelCompleteUI != null) {
            levelCompleteUI.SetActive(false);
        }
        if (levelFailUI != null) {
            levelFailUI.SetActive(false);
        }
        BakeOutlineSmoothNormals();
        ApplyTagOutlines();
        is2DPrimary = startWith2DView;
        CreatePreviewUI();
        ApplyCameraLayout();
    }

    private void CreatePreviewUI()
    {
        GameObject canvasGO = new GameObject("_PreviewCanvas");
        canvasGO.hideFlags = HideFlags.DontSave;
        previewCanvas = canvasGO.AddComponent<Canvas>();
        previewCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        previewCanvas.sortingOrder = -100;

        GameObject imageGO = new GameObject("_PreviewImage");
        imageGO.hideFlags = HideFlags.DontSave;
        imageGO.transform.SetParent(canvasGO.transform, false);
        previewImage = imageGO.AddComponent<RawImage>();
        UpdatePreviewImageTransform();
    }

    private void UpdatePreviewImageTransform()
    {
        if (previewImage == null) return;
        RectTransform rt = previewImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(
            Screen.width * previewMargin,
            Screen.height * (1f - previewMargin - previewHeight)
        );
        rt.sizeDelta = new Vector2(Screen.width * previewWidth, Screen.height * previewHeight);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) {
            is2DPrimary = !is2DPrimary;
            ApplyCameraLayout();
        }

        if (previewImage != null) {
            previewImage.color = new Color(1f, 1f, 1f, previewAlpha);
            Vector2Int currentSize = new Vector2Int(Screen.width, Screen.height);
            if (currentSize != lastScreenSize) {
                lastScreenSize = currentSize;
                UpdatePreviewImageTransform();
            }
        }
    }

    public void ToggleCameraView()
    {
        is2DPrimary = !is2DPrimary;
        ApplyCameraLayout();
    }

    private void ApplyCameraLayout()
    {
        if (camera2D == null || camera3D == null) return;

        Camera primaryCamera = is2DPrimary ? camera2D : camera3D;
        Camera previewCamera = is2DPrimary ? camera3D : camera2D;

        // Release RT from the camera that is no longer the preview.
        if (currentPreviewCamera != null && currentPreviewCamera != previewCamera) {
            currentPreviewCamera.targetTexture = null;
            currentPreviewCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }
        currentPreviewCamera = previewCamera;

        // Primary: normal full-screen render.
        primaryCamera.targetTexture = null;
        primaryCamera.rect = new Rect(0f, 0f, 1f, 1f);
        primaryCamera.depth = 0f;
        primaryCamera.enabled = true;

        // Preview: render into RT; RawImage displays it with alpha.
        EnsurePreviewRT();
        previewCamera.targetTexture = previewRT;
        if (previewImage != null) previewImage.texture = previewRT;
        previewCamera.rect = new Rect(0f, 0f, 1f, 1f);
        previewCamera.depth = 0f;
        previewCamera.enabled = true;

        SetAudioListenerState(primaryCamera, true);
        SetAudioListenerState(previewCamera, false);
    }

    private void EnsurePreviewRT()
    {
        int rtW = Mathf.Max(1, Mathf.RoundToInt(Screen.width  * previewWidth));
        int rtH = Mathf.Max(1, Mathf.RoundToInt(Screen.height * previewHeight));
        if (previewRT != null && previewRT.width == rtW && previewRT.height == rtH) return;
        if (previewRT != null) {
            if (currentPreviewCamera != null) currentPreviewCamera.targetTexture = null;
            previewRT.Release();
            Destroy(previewRT);
        }
        previewRT = new RenderTexture(rtW, rtH, 16);
        previewRT.Create();
        if (previewImage != null) previewImage.texture = previewRT;
    }

    // Returns the preview rect in screen space (y=0 at bottom) for raycasting use.
    public Rect GetPreviewScreenRect()
    {
        return new Rect(
            Screen.width  * previewMargin,
            Screen.height * (1f - previewMargin - previewHeight),
            Screen.width  * previewWidth,
            Screen.height * previewHeight
        );
    }

    private void OnDestroy()
    {
        if (currentPreviewCamera != null) currentPreviewCamera.targetTexture = null;
        if (previewRT != null) { previewRT.Release(); Destroy(previewRT); previewRT = null; }
        if (previewCanvas != null) Destroy(previewCanvas.gameObject);
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
        if (levelCompleteUI == null) {
            GameObject prefab = Resources.Load<GameObject>("LevelCompleteUI");
            if (prefab != null) {
                levelCompleteUI = Instantiate(prefab);
                WireButtons(levelCompleteUI);
            }
        } else {
            levelCompleteUI.SetActive(true);
        }

        if (levelCompleteUI != null) {
            Canvas canvas = levelCompleteUI.GetComponent<Canvas>();
            if (canvas != null) {
                canvas.enabled = false;
                canvas.enabled = true;
            }
        }
    }

    public void ShowLevelFail()
    {
        // Use the scene reference if assigned; otherwise instantiate from Resources/LevelFailUI
        if (levelFailUI == null) {
            GameObject prefab = Resources.Load<GameObject>("LevelFailUI");
            if (prefab != null) {
                levelFailUI = Instantiate(prefab);
                WireButtons(levelFailUI);
            }
        } else {
            levelFailUI.SetActive(true);
        }

        if (levelFailUI != null) {
            Canvas canvas = levelFailUI.GetComponent<Canvas>();
            if (canvas != null) {
                canvas.enabled = false;
                canvas.enabled = true;
            }
        }
    }

    // Finds every Button in the UI and wires its name to a matching method on this script.
    // Button GameObject names: "ReloadButton" -> ReloadScene, "NextLevelButton" -> LoadNextLevel.
    private void WireButtons(GameObject ui)
    {
        foreach (Button btn in ui.GetComponentsInChildren<Button>(true))
        {
            string btnName = btn.gameObject.name;
            btn.onClick.RemoveAllListeners();
            if (btnName == "ReloadButton")      btn.onClick.AddListener(ReloadScene);
            else if (btnName == "NextLevelButton") btn.onClick.AddListener(LoadNextLevel);
        }
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadNextLevel()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings) {
            SceneManager.LoadScene(nextIndex);
        }
    }

    private static string FindMovableTag(GameObject obj)
    {
        Transform t = obj.transform;
        while (t != null) {
            string tag = t.gameObject.tag;
            if (tag == "MoveableX" || tag == "MoveableY" || tag == "MoveableZ" ||
                tag == "MoveableXY" || tag == "MoveableXZ" || tag == "MoveableYZ" ||
                tag == "MoveableXYZ") {
                return tag;
            }
            t = t.parent;
        }
        return null;
    }

    private void ApplyTagOutlines()
    {
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        foreach (Renderer rend in allRenderers) {
            string tag = FindMovableTag(rend.gameObject);
            if (tag == null) continue;
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
                default:            continue;
            }

            // Apply to any existing material that already supports the outline properties.
            bool hasOutlineShader = false;
            Material[] instanced = rend.materials;
            for (int i = 0; i < instanced.Length; i++) {
                if (instanced[i] != null && instanced[i].shader != null &&
                    (instanced[i].shader.name == "Custom/DoubleSidedTexture" ||
                     instanced[i].shader.name == "Custom/DoubleSidedTextureTransparent" ||
                     instanced[i].shader.name == "Custom/Outline")) {
                    instanced[i].SetColor("_OutlineColor", color);
                    instanced[i].SetFloat("_OutlineWidth", width);
                    hasOutlineShader = true;
                }
            }
            rend.materials = instanced;

            // For objects without an outline-capable shader, add a child outline renderer
            // so the original material array is untouched (EzySlice requires materials.Length == submeshCount).
            if (!hasOutlineShader) {
                Shader outlineShader = Shader.Find("Custom/Outline");
                if (outlineShader != null) {
                    MeshFilter mf = rend.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) {
                        GameObject outlineChild = new GameObject("OutlineRenderer");
                        outlineChild.transform.SetParent(rend.transform, false);
                        outlineChild.hideFlags = HideFlags.DontSave;
                        MeshFilter childMf = outlineChild.AddComponent<MeshFilter>();
                        childMf.sharedMesh = mf.sharedMesh;
                        MeshRenderer childRend = outlineChild.AddComponent<MeshRenderer>();
                        Material outlineMat = new Material(outlineShader);
                        outlineMat.SetColor("_OutlineColor", color);
                        outlineMat.SetFloat("_OutlineWidth", width);
                        childRend.sharedMaterial = outlineMat;
                    }
                }
            }
        }
    }

    private void BakeOutlineSmoothNormals()
    {
        MeshFilter[] allFilters = FindObjectsOfType<MeshFilter>();
        foreach (MeshFilter mf in allFilters) {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponent<Renderer>() == null) continue;
            if (FindMovableTag(mf.gameObject) == null) continue;
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
