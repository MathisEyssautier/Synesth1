using System.Collections;
using UnityEngine;

public class RoomTransition : MonoBehaviour
{
    [Header("Destination")]
    public Transform spawnPoint;

    [Header("Porte")]
    public DoorController doorController;
    public float openAngleThreshold = 45f;

    [Header("Fondu")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.3f;

    [Header("Références")]
    public Transform xrOrigin;
    public Transform xrCamera;

    [Header("Trigger opposé (pour éviter la boucle)")]
    public RoomTransition otherTrigger;

    private bool _transitioning = false;


    private bool IsDoorOpen()
    {
        if (doorController == null) { Debug.Log($"[{gameObject.name}] doorController est NULL !"); return false; }
        return Mathf.Abs(doorController.currentYAngle) >= openAngleThreshold;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[RoomTransition] Trigger touché par : {other.name} sur {gameObject.name}");
        if (_transitioning) { Debug.Log("Bloqué : transitioning"); return; }
        if (!IsDoorOpen()) { Debug.Log($"Bloqué : porte pas assez ouverte, angle = {doorController.currentYAngle}"); return; }
        if (!other.CompareTag("Player")) { Debug.Log($"Bloqué : pas le tag Player, tag = {other.tag}"); return; }


        StartCoroutine(DoTransition());
    }

    private IEnumerator DoTransition()
    {
        Debug.Log($"[RoomTransition] Transition déclenchée depuis {gameObject.name} vers {spawnPoint.name}");
        _transitioning = true;
        if (otherTrigger != null) otherTrigger._transitioning = true; // bloque l'autre trigger
        yield return StartCoroutine(Fade(0f, 1f));

        if (doorController != null) doorController.ForceClose();

        // Téléportation
        xrOrigin.position = spawnPoint.position;
        xrOrigin.rotation = spawnPoint.rotation;

        // Correction offset caméra (optionnel mais recommandé)
        Vector3 cameraOffset = xrCamera.position - xrOrigin.position;
        cameraOffset.y = 0f;
        xrOrigin.position -= cameraOffset;

        yield return null; // Une frame pour appliquer les transformations

        yield return StartCoroutine(Fade(1f, 0f));

        // Cooldown avant de pouvoir retransiter
        yield return new WaitForSeconds(2f);

        _transitioning = false;
        if (otherTrigger != null) otherTrigger._transitioning = false;
    }


    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            t += Time.deltaTime;
            yield return null;
        }
        fadeCanvasGroup.alpha = to;
    }
}