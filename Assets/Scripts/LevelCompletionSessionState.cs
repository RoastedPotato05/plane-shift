using System;
using System.Collections.Generic;

public static class LevelCompletionSessionState
{
    private static readonly HashSet<string> completedScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static void MarkCompleted(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) {
            return;
        }

        completedScenes.Add(sceneName);
    }

    public static bool IsCompleted(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) {
            return false;
        }

        return completedScenes.Contains(sceneName);
    }
}
