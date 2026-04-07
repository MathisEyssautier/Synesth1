using UnityEngine;

public class ClampPlayerMaxY : MonoBehaviour
{
    [Header("Rig root to clamp (XR Origin)")]
    [SerializeField] private Transform playerRigRoot;
    [SerializeField] private bool useStartYAsMax = true;
    [SerializeField] private float maxY = 0f;
    [SerializeField] private bool blockHorizontalMoveWhenSteppingUp = true;
    [SerializeField] private float allowedRiseEpsilon = 0.005f;

    private float _runtimeMaxY;
    private Vector3 _lastValidPosition;

    private void Start()
    {
        if (playerRigRoot == null) return;
        _runtimeMaxY = useStartYAsMax ? playerRigRoot.position.y : maxY;
        _lastValidPosition = playerRigRoot.position;
    }

    private void LateUpdate()
    {
        if (playerRigRoot == null) return;

        Vector3 p = playerRigRoot.position;
        float clampY = useStartYAsMax ? _runtimeMaxY : maxY;
        bool steppedUp = p.y > clampY + Mathf.Max(0f, allowedRiseEpsilon);

        if (steppedUp)
        {
            if (blockHorizontalMoveWhenSteppingUp)
            {
                // Restore last valid X/Z to prevent climbing onto low objects.
                playerRigRoot.position = new Vector3(_lastValidPosition.x, clampY, _lastValidPosition.z);
            }
            else
            {
                // Clamp only vertical axis.
                playerRigRoot.position = new Vector3(p.x, clampY, p.z);
                _lastValidPosition = playerRigRoot.position;
            }
            return;
        }

        // Keep a clean valid position cache.
        if (p.y > clampY)
            p.y = clampY;
        _lastValidPosition = p;
    }
}
