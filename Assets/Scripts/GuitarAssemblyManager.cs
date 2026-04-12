using UnityEngine;
using UnityEngine.Events;

public class GuitarAssemblyManager : MonoBehaviour
{
    [Header("Strings visuals on guitar (size = 6)")]
    [SerializeField] private GameObject[] guitarStringVisuals = new GameObject[6];

    [Header("Narration")]
    [SerializeField] private UnityEvent onAllStringsPlaced;

    private bool[] _placed;
    private int _placedCount;
    private bool _allStringsEventFired;

    public bool AreAllStringsPlaced => _placed != null && _placedCount >= _placed.Length && _placed.Length > 0;

    private void Awake()
    {
        int n = guitarStringVisuals != null ? guitarStringVisuals.Length : 0;
        _placed = new bool[Mathf.Max(0, n)];
        _placedCount = 0;

        // Les cordes "montées" sur la guitare démarrent cachées.
        for (int i = 0; i < n; i++)
        {
            if (guitarStringVisuals[i] != null)
                guitarStringVisuals[i].SetActive(false);
        }
    }

    /// <summary>
    /// Appelé quand une corde pickup touche la guitare en étant tenue.
    /// Retourne true si placement validé.
    /// </summary>
    public bool TryPlaceString(int stringIndex, GameObject pickupStringObject)
    {
        if (_placed == null || _placed.Length == 0) return false;
        if (stringIndex < 0 || stringIndex >= _placed.Length) return false;
        if (_placed[stringIndex]) return false;

        _placed[stringIndex] = true;
        _placedCount++;

        if (guitarStringVisuals != null && stringIndex < guitarStringVisuals.Length && guitarStringVisuals[stringIndex] != null)
            guitarStringVisuals[stringIndex].SetActive(true);

        if (pickupStringObject != null)
            pickupStringObject.SetActive(false);

        if (!_allStringsEventFired && _placed.Length > 0 && _placedCount >= _placed.Length)
        {
            _allStringsEventFired = true;
            onAllStringsPlaced?.Invoke();
        }

        return true;
    }
}

