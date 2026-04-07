using UnityEngine;

public class WorldSpaceCreditsScroller : MonoBehaviour
{
    [SerializeField] private RectTransform creditsRoot;
    [SerializeField] private float scrollSpeed = 30f;
    [SerializeField] private bool playOnStart = false;

    private bool _isPlaying;

    private void Start()
    {
        if (playOnStart)
            BeginScroll();
        else
            gameObject.SetActive(false);
    }

    public void BeginScroll()
    {
        gameObject.SetActive(true);
        _isPlaying = true;
    }

    public void StopScroll()
    {
        _isPlaying = false;
    }

    private void Update()
    {
        if (!_isPlaying || creditsRoot == null) return;

        Vector2 p = creditsRoot.anchoredPosition;
        p.y += scrollSpeed * Time.deltaTime;
        creditsRoot.anchoredPosition = p;
    }
}
