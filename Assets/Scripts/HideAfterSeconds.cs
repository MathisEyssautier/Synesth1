using System.Collections;
using UnityEngine;

/// <summary>
/// Désactive un GameObject (lui-même ou une cible) après un délai au démarrage / à l'activation.
/// </summary>
public class HideAfterSeconds : MonoBehaviour
{
    [SerializeField] private float delaySeconds = 10f;
    [Tooltip("Si vide, désactive ce GameObject.")]
    [SerializeField] private GameObject target;
    [SerializeField] private bool useUnscaledTime;

    private Coroutine _hideRoutine;

    private void OnEnable()
    {
        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(HideRoutine());
    }

    private void OnDisable()
    {
        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
    }

    private IEnumerator HideRoutine()
    {
        float wait = Mathf.Max(0f, delaySeconds);
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(wait);
        else
            yield return new WaitForSeconds(wait);

        GameObject go = target != null ? target : gameObject;
        if (go != null)
            go.SetActive(false);

        _hideRoutine = null;
    }
}
