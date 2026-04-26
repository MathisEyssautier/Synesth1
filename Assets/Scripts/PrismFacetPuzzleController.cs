using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PrismFacetPuzzleController : MonoBehaviour
{
    [Serializable]
    private class PrismFacetTrack
    {
        [Tooltip("Transform de la facette à déplacer (mesh/objet visible).")]
        public Transform facetTransform;
        [Tooltip("Les 5 positions du rail pour cette facette (1..5).")]
        public Transform[] railPositions = new Transform[5];
        [Tooltip("Position initiale (0..4).")]
        public int startPosition = 0;
    }

    private class RendererMatBackup
    {
        public Renderer renderer;
        public Material[] sharedSnapshot;
    }

    [Header("Facettes (taille attendue = 5)")]
    [SerializeField] private PrismFacetTrack[] facetTracks = new PrismFacetTrack[5];
    [SerializeField] private int targetPosition = 2; // Position 3 (index 2)

    [Header("Mouvement")]
    [SerializeField] private float facetMoveDuration = 0.45f;

    [Header("Flash à chaque accord (matériaux)")]
    [Tooltip("Matériau blanc (partagé en source). Une copie est créée à l'exécution. Si vide, clone du 1er matériau de facette teint en blanc.")]
    [SerializeField] private Material whiteChordFlashMaterial;
    [SerializeField] private float chordFlashDuration = 1f;

    [Header("Prisme reconstruit")]
    [SerializeField] private GameObject rebuiltPrismRoot;
    [SerializeField] private Rigidbody rebuiltPrismRigidbody;
    [SerializeField] private XRGrabInteractable rebuiltPrismGrabInteractable;

    [Header("Callbacks")]
    [SerializeField] private UnityEvent onPrismSolved;

    [Header("Guitare (optionnel si enregistré par GuitarSoundZone)")]
    [SerializeField] private GuitarCapoCrankController guitarCapoWhenPuzzleCompletes;

    private int[] _facetPositions;
    private bool _solved;
    private Coroutine[] _facetMoveCoroutines;
    private Coroutine _flashCoroutine;
    private List<RendererMatBackup>[] _facetRendererBackups;
    private GuitarCapoCrankController _guitarCapoRegistered;
    private Material _flashMatWhiteInstance;
    private Material _flashMatCapoInstance;

    public bool IsSolved => _solved;

    /// <summary>
    /// Appelé par GuitarSoundZone pour fournir la guitare à résoudre
    /// quand le puzzle prismatique est complété.
    /// </summary>
    public void SetGuitarCapoForSolve(GuitarCapoCrankController capo)
    {
        _guitarCapoRegistered = capo;
    }

    private GuitarCapoCrankController ResolvedGuitarCapo =>
        _guitarCapoRegistered != null ? _guitarCapoRegistered : guitarCapoWhenPuzzleCompletes;

    private void Awake()
    {
        int count = facetTracks != null ? facetTracks.Length : 0;
        _facetPositions = new int[Mathf.Max(0, count)];
        _facetMoveCoroutines = new Coroutine[count];
        _solved = false;

        CacheFacetRendererMaterialBackups();

        for (int i = 0; i < count; i++)
        {
            int start = Mathf.Clamp(facetTracks[i].startPosition, 0, 4);
            _facetPositions[i] = start;
            ApplyFacetPoseImmediate(i);
        }

        SetRebuiltPrismActive(false);
    }

    /// <summary>
    /// À chaque accord : toutes les facettes passent en blanc sauf celle du cran capot,
    /// qui prend l'apparence du capot pour ce cran (pendant <see cref="chordFlashDuration"/>).
    /// </summary>
    public void PlayChordMaterialFlashForCapoIndex(int capoIndex)
    {
        if (_solved)
            return;
        if (_facetRendererBackups == null || _facetRendererBackups.Length == 0)
            return;

        CancelChordFlashCleanupTempMaterials();
        capoIndex = Mathf.Clamp(capoIndex, 0, Mathf.Max(0, _facetRendererBackups.Length - 1));
        _flashCoroutine = StartCoroutine(ChordMaterialFlashRoutine(capoIndex));
    }

    /// <summary>
    /// Avance la facette liée à l'index capot (0..4), avec boucle 5->1.
    /// </summary>
    public void AdvanceFacetFromCapoIndex(int capoIndex)
    {
        if (_solved || _facetPositions == null || facetTracks == null)
            return;
        if (capoIndex < 0 || capoIndex >= _facetPositions.Length)
            return;

        _facetPositions[capoIndex] = (_facetPositions[capoIndex] + 1) % 5;

        if (_facetMoveCoroutines != null && capoIndex < _facetMoveCoroutines.Length && _facetMoveCoroutines[capoIndex] != null)
        {
            StopCoroutine(_facetMoveCoroutines[capoIndex]);
            _facetMoveCoroutines[capoIndex] = null;
        }

        _facetMoveCoroutines[capoIndex] = StartCoroutine(MoveFacetRoutine(capoIndex));
    }

    private void CacheFacetRendererMaterialBackups()
    {
        int count = facetTracks != null ? facetTracks.Length : 0;
        _facetRendererBackups = new List<RendererMatBackup>[count];
        for (int i = 0; i < count; i++)
        {
            _facetRendererBackups[i] = new List<RendererMatBackup>();
            var track = facetTracks[i];
            if (track == null || track.facetTransform == null)
                continue;

            var renderers = track.facetTransform.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                var shared = r.sharedMaterials;
                var snap = shared != null ? (Material[])shared.Clone() : Array.Empty<Material>();
                _facetRendererBackups[i].Add(new RendererMatBackup { renderer = r, sharedSnapshot = snap });
            }
        }
    }

    private void CancelChordFlashCleanupTempMaterials()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        RestoreAllFacetMaterialsFromCache();

        if (_flashMatWhiteInstance != null)
        {
            Destroy(_flashMatWhiteInstance);
            _flashMatWhiteInstance = null;
        }

        if (_flashMatCapoInstance != null)
        {
            Destroy(_flashMatCapoInstance);
            _flashMatCapoInstance = null;
        }
    }

    private IEnumerator ChordMaterialFlashRoutine(int capoIndex)
    {
        _flashMatWhiteInstance = CreateWhiteFlashMaterialInstance();
        if (_flashMatWhiteInstance == null)
        {
            _flashCoroutine = null;
            yield break;
        }

        _flashMatCapoInstance = ResolvedGuitarCapo != null
            ? ResolvedGuitarCapo.CreateCapoVisualMaterialForCrankIndex(capoIndex)
            : null;

        ApplyChordFlashMaterials(capoIndex, _flashMatWhiteInstance, _flashMatCapoInstance);

        float dur = Mathf.Max(0.01f, chordFlashDuration);
        yield return new WaitForSeconds(dur);

        RestoreAllFacetMaterialsFromCache();

        if (_flashMatWhiteInstance != null)
        {
            Destroy(_flashMatWhiteInstance);
            _flashMatWhiteInstance = null;
        }

        if (_flashMatCapoInstance != null)
        {
            Destroy(_flashMatCapoInstance);
            _flashMatCapoInstance = null;
        }

        _flashCoroutine = null;
    }

    private void ApplyChordFlashMaterials(int activeFacetIndex, Material whiteMat, Material capoMat)
    {
        for (int f = 0; f < _facetRendererBackups.Length; f++)
        {
            var list = _facetRendererBackups[f];
            if (list == null)
                continue;

            Material slotMat = f == activeFacetIndex && capoMat != null ? capoMat : whiteMat;
            foreach (var backup in list)
            {
                if (backup.renderer == null || backup.sharedSnapshot == null || backup.sharedSnapshot.Length == 0)
                    continue;

                int n = backup.sharedSnapshot.Length;
                var applied = new Material[n];
                for (int i = 0; i < n; i++)
                    applied[i] = slotMat;
                backup.renderer.materials = applied;
            }
        }
    }

    private void RestoreAllFacetMaterialsFromCache()
    {
        if (_facetRendererBackups == null)
            return;

        foreach (var list in _facetRendererBackups)
        {
            if (list == null)
                continue;
            foreach (var b in list)
            {
                if (b.renderer == null || b.sharedSnapshot == null)
                    continue;
                b.renderer.sharedMaterials = b.sharedSnapshot;
            }
        }
    }

    private Material CreateWhiteFlashMaterialInstance()
    {
        if (whiteChordFlashMaterial != null)
            return new Material(whiteChordFlashMaterial);

        Material template = FindFirstFacetSharedMaterial();
        if (template == null)
            return null;

        var m = new Material(template);
        Color w = Color.white;
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", w);
        else
            m.color = w;
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", w);
        }

        return m;
    }

    private Material FindFirstFacetSharedMaterial()
    {
        if (facetTracks == null)
            return null;
        foreach (var track in facetTracks)
        {
            if (track == null || track.facetTransform == null)
                continue;
            var renderers = track.facetTransform.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                var mats = r.sharedMaterials;
                if (mats == null)
                    continue;
                foreach (var mat in mats)
                {
                    if (mat != null)
                        return mat;
                }
            }
        }

        return null;
    }

    private IEnumerator MoveFacetRoutine(int i)
    {
        if (facetTracks == null || i < 0 || i >= facetTracks.Length)
            yield break;

        var track = facetTracks[i];
        if (track == null || track.facetTransform == null)
            yield break;
        if (track.railPositions == null || track.railPositions.Length == 0)
            yield break;

        int pos = Mathf.Clamp(_facetPositions[i], 0, track.railPositions.Length - 1);
        var marker = track.railPositions[pos];
        if (marker == null)
            yield break;

        Transform tr = track.facetTransform;
        Vector3 startPos = tr.position;
        Quaternion startRot = tr.rotation;
        Vector3 endPos = marker.position;
        Quaternion endRot = marker.rotation;

        float dur = Mathf.Max(0.01f, facetMoveDuration);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            if (_solved)
            {
                if (_facetMoveCoroutines != null && i < _facetMoveCoroutines.Length)
                    _facetMoveCoroutines[i] = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / dur);
            u = u * u * (3f - 2f * u);
            tr.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, u), Quaternion.Slerp(startRot, endRot, u));
            yield return null;
        }

        tr.SetPositionAndRotation(endPos, endRot);
        if (_facetMoveCoroutines != null && i < _facetMoveCoroutines.Length)
            _facetMoveCoroutines[i] = null;

        if (!_solved && CheckSolved())
            SolvePuzzle();
    }

    private void ApplyFacetPoseImmediate(int i)
    {
        if (facetTracks == null || i < 0 || i >= facetTracks.Length)
            return;

        var track = facetTracks[i];
        if (track == null || track.facetTransform == null)
            return;
        if (track.railPositions == null || track.railPositions.Length == 0)
            return;

        int pos = Mathf.Clamp(_facetPositions[i], 0, track.railPositions.Length - 1);
        var marker = track.railPositions[pos];
        if (marker == null)
            return;

        track.facetTransform.SetPositionAndRotation(marker.position, marker.rotation);
    }

    private bool CheckSolved()
    {
        if (_facetPositions == null || _facetPositions.Length == 0)
            return false;

        for (int i = 0; i < _facetPositions.Length; i++)
        {
            if (_facetPositions[i] != targetPosition)
                return false;
        }

        return true;
    }

    private void SolvePuzzle()
    {
        if (_solved)
            return;

        _solved = true;

        if (_facetMoveCoroutines != null)
        {
            for (int i = 0; i < _facetMoveCoroutines.Length; i++)
            {
                if (_facetMoveCoroutines[i] != null)
                {
                    StopCoroutine(_facetMoveCoroutines[i]);
                    _facetMoveCoroutines[i] = null;
                }
            }
        }

        CancelChordFlashCleanupTempMaterials();

        // Cache les facettes disséminées.
        if (facetTracks != null)
        {
            for (int i = 0; i < facetTracks.Length; i++)
            {
                var t = facetTracks[i];
                if (t != null && t.facetTransform != null)
                    t.facetTransform.gameObject.SetActive(false);
            }
        }

        SetRebuiltPrismActive(true);
        onPrismSolved?.Invoke();

        ResolvedGuitarCapo?.TrySolveFromPrismPuzzle();
    }

    private void SetRebuiltPrismActive(bool active)
    {
        if (rebuiltPrismRoot != null)
            rebuiltPrismRoot.SetActive(active);

        if (rebuiltPrismGrabInteractable != null)
            rebuiltPrismGrabInteractable.enabled = active;

        if (rebuiltPrismRigidbody != null)
        {
            rebuiltPrismRigidbody.isKinematic = !active;
            rebuiltPrismRigidbody.useGravity = active;
        }
    }
}
