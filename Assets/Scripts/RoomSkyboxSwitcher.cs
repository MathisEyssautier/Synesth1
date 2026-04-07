using UnityEngine;

public class RoomSkyboxSwitcher : MonoBehaviour
{
    public enum RoomType
    {
        Salon,
        Cuisine,
        Bureau
    }

    [Header("Player")]
    [SerializeField] private Transform playerHead;

    [Header("Room triggers (BoxCollider isTrigger)")]
    [SerializeField] private BoxCollider cuisineTrigger;
    [SerializeField] private BoxCollider bureauTrigger;

    [Header("Skyboxes")]
    [SerializeField] private Material salonSkybox;
    [SerializeField] private Material cuisineSkybox;
    [SerializeField] private Material bureauSkybox;
    [SerializeField] private bool updateEnvironmentAfterSwitch = true;
    [Header("Blend")]
    [SerializeField] private bool useBlend = true;
    [SerializeField] private float blendDuration = 0.6f;
    [Tooltip("Exposure property used for fade (default for 6 Sided skybox).")]
    [SerializeField] private string exposureProperty = "_Exposure";

    private RoomType _currentRoom = RoomType.Salon;
    private Coroutine _blendRoutine;
    private readonly System.Collections.Generic.Dictionary<Material, float> _originalExposure =
        new System.Collections.Generic.Dictionary<Material, float>();

    private void Awake()
    {
        CacheOriginalState(salonSkybox);
        CacheOriginalState(cuisineSkybox);
        CacheOriginalState(bureauSkybox);
    }

    private void Start()
    {
        ApplySkyboxForRoom(ComputeRoomFromPosition());
    }

    private void Update()
    {
        RoomType targetRoom = ComputeRoomFromPosition();
        if (targetRoom == _currentRoom) return;
        ApplySkyboxForRoom(targetRoom);
    }

    private RoomType ComputeRoomFromPosition()
    {
        if (playerHead == null) return RoomType.Salon;

        Vector3 p = playerHead.position;

        if (bureauTrigger != null && bureauTrigger.bounds.Contains(p))
            return RoomType.Bureau;

        if (cuisineTrigger != null && cuisineTrigger.bounds.Contains(p))
            return RoomType.Cuisine;

        return RoomType.Salon;
    }

    private void ApplySkyboxForRoom(RoomType room)
    {
        _currentRoom = room;

        Material target = salonSkybox;
        if (room == RoomType.Cuisine) target = cuisineSkybox;
        else if (room == RoomType.Bureau) target = bureauSkybox;

        if (target == null) return;

        if (!useBlend || blendDuration <= 0f)
        {
            if (_blendRoutine != null)
            {
                StopCoroutine(_blendRoutine);
                _blendRoutine = null;
                RestoreAllSkyboxesFadeState();
            }
            if (RenderSettings.skybox != target)
                RenderSettings.skybox = target;
            if (updateEnvironmentAfterSwitch)
                DynamicGI.UpdateEnvironment();
            return;
        }

        if (_blendRoutine != null)
        {
            StopCoroutine(_blendRoutine);
            _blendRoutine = null;
            RestoreAllSkyboxesFadeState();
        }
        _blendRoutine = StartCoroutine(BlendToSkybox(target));
    }

    private System.Collections.IEnumerator BlendToSkybox(Material target)
    {
        Material current = RenderSettings.skybox;
        float d = Mathf.Max(0.01f, blendDuration);

        if (current == null || target == null)
        {
            RenderSettings.skybox = target;
            if (updateEnvironmentAfterSwitch)
                DynamicGI.UpdateEnvironment();
            _blendRoutine = null;
            yield break;
        }

        // Exposure-only blend; if unsupported, fallback instant switch.
        if (!HasExposure(current) || !HasExposure(target))
        {
            RestoreAllSkyboxesFadeState();
            RenderSettings.skybox = target;
            if (updateEnvironmentAfterSwitch)
                DynamicGI.UpdateEnvironment();
            _blendRoutine = null;
            yield break;
        }

        float curBase = GetBaseExposure(current);
        float targetBase = GetBaseExposure(target);
        float half = d * 0.5f;

        // Fade out current skybox.
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            SetExposure(current, Mathf.Lerp(curBase, 0f, k));
            if (updateEnvironmentAfterSwitch)
                DynamicGI.UpdateEnvironment();
            yield return null;
        }
        SetExposure(current, curBase);

        // Switch and fade in target skybox.
        RenderSettings.skybox = target;
        SetExposure(target, 0f);
        if (updateEnvironmentAfterSwitch)
            DynamicGI.UpdateEnvironment();

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            SetExposure(target, Mathf.Lerp(0f, targetBase, k));
            if (updateEnvironmentAfterSwitch)
                DynamicGI.UpdateEnvironment();
            yield return null;
        }
        SetExposure(target, targetBase);
        if (updateEnvironmentAfterSwitch)
            DynamicGI.UpdateEnvironment();
        _blendRoutine = null;
    }

    private void OnDisable()
    {
        if (_blendRoutine != null)
        {
            StopCoroutine(_blendRoutine);
            _blendRoutine = null;
        }
        RestoreAllSkyboxesFadeState();
    }

    private void CacheOriginalState(Material mat)
    {
        if (mat == null || _originalExposure.ContainsKey(mat)) return;
        _originalExposure.Add(mat, GetExposure(mat));
    }

    private void RestoreAllSkyboxesFadeState()
    {
        foreach (var kv in _originalExposure)
        {
            var mat = kv.Key;
            if (mat == null) continue;
            if (!HasExposure(mat)) continue;
            SetExposure(mat, kv.Value);
        }
    }

    private bool HasExposure(Material mat)
    {
        return mat != null && (mat.HasProperty(exposureProperty) || mat.HasProperty("_Exposure"));
    }

    private float GetBaseExposure(Material mat)
    {
        if (mat == null) return 1f;
        if (_originalExposure.TryGetValue(mat, out float v)) return v;
        return GetExposure(mat);
    }

    private float GetExposure(Material mat)
    {
        if (mat == null) return 1f;
        if (mat.HasProperty(exposureProperty)) return mat.GetFloat(exposureProperty);
        if (mat.HasProperty("_Exposure")) return mat.GetFloat("_Exposure");
        return 1f;
    }

    private void SetExposure(Material mat, float exposure)
    {
        if (mat == null) return;
        if (mat.HasProperty(exposureProperty))
            mat.SetFloat(exposureProperty, exposure);
        else if (mat.HasProperty("_Exposure"))
            mat.SetFloat("_Exposure", exposure);
    }
}
