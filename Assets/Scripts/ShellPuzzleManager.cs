using UnityEngine;

public class ShellPuzzleManager : MonoBehaviour
{
    [Header("Zones (4)")]
    [SerializeField] private ShellPlacementZone[] zones;

    [Header("Black cube -> white on success")]
    [SerializeField] private Renderer blackCubeRenderer;
    [SerializeField] private Color solvedColor = Color.white;

    [Header("Reward")]
    [Tooltip("GameObject à activer quand tout est correct (ex: 2e fader)")]
    [SerializeField] private GameObject rewardObjectToActivate;

    [Header("State")]
    [SerializeField] private bool setRewardInactiveOnStart = true;

    private bool _solved = false;
    public bool IsSolved => _solved;

    private void Awake()
    {
        if (setRewardInactiveOnStart && rewardObjectToActivate != null)
            rewardObjectToActivate.SetActive(false);
    }

    private void Start()
    {
        NotifyZoneChanged();
    }

    public void NotifyZoneChanged()
    {
        if (_solved) return;
        if (zones == null || zones.Length == 0) return;

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] == null) return;
            if (!zones[i].IsCorrectlyOccupied) return;
        }

        Solve();
    }

    private void Solve()
    {
        _solved = true;

        if (blackCubeRenderer != null)
            blackCubeRenderer.material.color = solvedColor;

        if (rewardObjectToActivate != null)
            rewardObjectToActivate.SetActive(true);
    }
}

