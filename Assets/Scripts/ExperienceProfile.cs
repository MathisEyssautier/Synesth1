using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Détecte le mode « Seine Lab » (parcours événement sans Black Room, piano + guitare instantanés, etc.).
/// </summary>
public static class ExperienceProfile
{
    public const string SeineLabSceneName = "Synesthesia_SeineLab";

    public static bool IsSeineLab =>
        SceneManager.GetActiveScene().name == SeineLabSceneName;

    /// <summary>True si la scène est listée et activée dans les Build Settings.</summary>
    public static bool IsSceneInBuild(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path)) continue;
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }

        return false;
    }
}
