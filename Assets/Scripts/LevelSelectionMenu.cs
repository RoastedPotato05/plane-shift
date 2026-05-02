using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("UI/Level Selection Menu")]
public class LevelSelectionMenu : MonoBehaviour
{
    [Header("Auto Binding")]
    [SerializeField] private bool bindOnEnable = true;
    [SerializeField] private bool replaceExistingListeners = true;

    [Header("Buttons In Order")]
    [SerializeField] private Button[] buttons;

    [Header("Completed Level Tint")]
    [SerializeField] private Color completedButtonColor = Color.green;

    private void OnEnable()
    {
        if (bindOnEnable) {
            BindButtons();
        }
    }

    [ContextMenu("Bind Buttons")]
    public void BindButtons()
    {
        if (buttons == null || buttons.Length == 0) {
            Debug.LogWarning("LevelSelectionMenu: No buttons assigned.", this);
            return;
        }

        for (int i = 0; i < buttons.Length; i++) {
            Button button = buttons[i];
            if (button == null) {
                continue;
            }

            string sceneName = ResolveSceneName(i);
            if (string.IsNullOrWhiteSpace(sceneName)) {
                Debug.LogWarning($"LevelSelectionMenu: No scene name found for button '{button.name}'.", button);
                continue;
            }

            if (!IsSceneInBuildSettings(sceneName)) {
                Debug.LogWarning($"LevelSelectionMenu: Scene '{sceneName}' is not in Build Settings, so button '{button.name}' was not wired.", button);
                continue;
            }

            string capturedSceneName = sceneName;
            if (replaceExistingListeners) {
                button.onClick.RemoveAllListeners();
            }

            SceneLoadOnPointerDown pointerDownLoader = button.GetComponent<SceneLoadOnPointerDown>();
            if (pointerDownLoader == null) {
                pointerDownLoader = button.gameObject.AddComponent<SceneLoadOnPointerDown>();
            }
            pointerDownLoader.SetSceneName(capturedSceneName);

            button.onClick.AddListener(() => LoadScene(capturedSceneName));
            ApplyCompletionVisual(button, capturedSceneName);
        }
    }

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) {
            Debug.LogWarning("LevelSelectionMenu: Cannot load an empty scene name.", this);
            return;
        }

        if (!IsSceneInBuildSettings(sceneName)) {
            Debug.LogWarning($"LevelSelectionMenu: Scene '{sceneName}' is not in Build Settings.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    public void LoadSceneByBuildIndex(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings) {
            Debug.LogWarning($"LevelSelectionMenu: Build index {buildIndex} is out of range.", this);
            return;
        }

        SceneManager.LoadScene(buildIndex);
    }

    private static string ResolveSceneName(int index)
    {
        return $"Level_{index + 1}";
    }

    private void ApplyCompletionVisual(Button button, string sceneName)
    {
        if (button == null || string.IsNullOrWhiteSpace(sceneName)) {
            return;
        }

        if (!LevelCompletionSessionState.IsCompleted(sceneName)) {
            return;
        }

        Image image = button.image;
        if (image != null) {
            image.color = completedButtonColor;
        }
    }

    private static bool IsSceneInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++) {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string builtSceneName = Path.GetFileNameWithoutExtension(scenePath);
            if (string.Equals(builtSceneName, sceneName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private sealed class SceneLoadOnPointerDown : MonoBehaviour, IPointerDownHandler
    {
        private string sceneName;

        public void SetSceneName(string newSceneName)
        {
            sceneName = newSceneName;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!string.IsNullOrWhiteSpace(sceneName)) {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
