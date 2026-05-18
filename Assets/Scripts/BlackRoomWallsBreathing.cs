using UnityEngine;

/// <summary>
/// Fait « respirer » les murs listés : glissement indépendant sur l'axe X ou Z local de chaque cube.
/// Les murs non listés ne bougent pas.
/// </summary>
public class BlackRoomWallsBreathing : MonoBehaviour
{
    private struct WallEntry
    {
        public Transform Transform;
        public Vector3 BaseLocalPosition;
        public Vector3 SlideLocal;
        public float Phase;
    }

    [Header("Murs (glissement local)")]
    [Tooltip("Murs latéraux : bougent sur l'axe X local.")]
    [SerializeField] private Transform[] wallsSlideLocalX;
    [Tooltip("Murs du fond / face : bougent sur l'axe Z local.")]
    [SerializeField] private Transform[] wallsSlideLocalZ;

    [Header("Respiration")]
    [SerializeField] private float positionAmplitude = 0.1f;
    [Tooltip("Cycles par seconde (lent = ~0.15–0.35).")]
    [SerializeField] private float breatheSpeed = 0.22f;
    [SerializeField] private float phaseSpread = 1.7f;
    [SerializeField] private bool useDualSine = true;
    [SerializeField] [Range(0f, 1f)] private float secondaryWaveWeight = 0.35f;

    private WallEntry[] _walls;

    private void Awake()
    {
        CacheWalls();
    }

    private void OnEnable()
    {
        if (_walls == null || _walls.Length == 0)
            CacheWalls();
    }

    private void CacheWalls()
    {
        int count = 0;
        if (wallsSlideLocalX != null) count += wallsSlideLocalX.Length;
        if (wallsSlideLocalZ != null) count += wallsSlideLocalZ.Length;

        var list = new System.Collections.Generic.List<WallEntry>(count);

        AddWalls(list, wallsSlideLocalX, Vector3.right);
        AddWalls(list, wallsSlideLocalZ, Vector3.forward);

        _walls = list.ToArray();
    }

    private void AddWalls(
        System.Collections.Generic.List<WallEntry> list,
        Transform[] walls,
        Vector3 slideLocalAxis)
    {
        if (walls == null) return;

        for (int i = 0; i < walls.Length; i++)
        {
            Transform t = walls[i];
            if (t == null) continue;

            float phase = (t.GetInstanceID() % 997) * 0.01f * phaseSpread;

            list.Add(new WallEntry
            {
                Transform = t,
                BaseLocalPosition = t.localPosition,
                SlideLocal = slideLocalAxis,
                Phase = phase
            });
        }
    }

    private void Update()
    {
        if (_walls == null || _walls.Length == 0)
            return;

        float t = Time.time * breatheSpeed * Mathf.PI * 2f;

        for (int i = 0; i < _walls.Length; i++)
        {
            WallEntry w = _walls[i];
            if (w.Transform == null) continue;

            float wave = Mathf.Sin(t + w.Phase);
            if (useDualSine)
            {
                float wave2 = Mathf.Sin(t * 1.41f + w.Phase * 2.13f);
                wave = Mathf.Lerp(wave, (wave + wave2) * 0.5f, secondaryWaveWeight);
            }

            w.Transform.localPosition = w.BaseLocalPosition + w.SlideLocal * (wave * positionAmplitude);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        positionAmplitude = Mathf.Max(0f, positionAmplitude);
        breatheSpeed = Mathf.Max(0.01f, breatheSpeed);
    }
#endif
}
