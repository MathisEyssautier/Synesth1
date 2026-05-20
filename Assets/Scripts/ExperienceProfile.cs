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
}
