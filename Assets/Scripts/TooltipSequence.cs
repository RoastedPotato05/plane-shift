using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TooltipSequence : MonoBehaviour
{
    [SerializeField] private GameObject[] tooltips;

    // Tracks which scenes have already shown their tooltips this play session.
    // Static so it survives scene reloads.
    private static readonly HashSet<string> shownScenes = new HashSet<string>();

    private int currentIndex = -1;

    private void Start()
    {
        // Hide all tooltips regardless — keeps editor state from mattering
        foreach (GameObject t in tooltips)
            if (t != null) t.SetActive(false);

        if (tooltips == null || tooltips.Length == 0)
            return;

        string sceneKey = SceneManager.GetActiveScene().name;
        if (shownScenes.Contains(sceneKey))
            return;

        shownScenes.Add(sceneKey);
        ShowIndex(0);
    }

    private void ShowIndex(int index)
    {
        // Hide the current one
        if (currentIndex >= 0 && currentIndex < tooltips.Length && tooltips[currentIndex] != null)
            tooltips[currentIndex].SetActive(false);

        currentIndex = index;

        if (currentIndex >= tooltips.Length)
        {
            currentIndex = -1;
            return;
        }

        if (tooltips[currentIndex] != null)
        {
            tooltips[currentIndex].SetActive(true);
            Canvas canvas = tooltips[currentIndex].GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = false;
                canvas.enabled = true;
            }
        }
    }

    // Wire this to a plain Next/Close button
    public void NextTooltip()
    {
        ShowIndex(currentIndex + 1);
    }

    // Wire this to a Next button that should also switch camera view
    public void NextTooltipAndSwitchCamera()
    {
        Main main = FindObjectOfType<Main>();
        if (main != null) main.ToggleCameraView();
        ShowIndex(currentIndex + 1);
    }
}
