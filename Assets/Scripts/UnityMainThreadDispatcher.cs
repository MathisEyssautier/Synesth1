using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<Action> _queue = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance != null) return _instance;

        // Try to find an existing dispatcher in the scene
        _instance = FindFirstObjectByType<UnityMainThreadDispatcher>();
        if (_instance != null) return _instance;

        // Create one if none exists (safe for callbacks / async)
        var go = new GameObject(nameof(UnityMainThreadDispatcher));
        _instance = go.AddComponent<UnityMainThreadDispatcher>();
        DontDestroyOnLoad(go);
        return _instance;
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            var root = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(root);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // Drain queue into a local buffer to avoid holding the lock while invoking actions.
        Action[] actions = null;
        lock (_queue)
        {
            if (_queue.Count > 0)
            {
                actions = _queue.ToArray();
                _queue.Clear();
            }
        }

        if (actions == null) return;
        for (int i = 0; i < actions.Length; i++)
        {
            try { actions[i]?.Invoke(); }
            catch { /* swallow to avoid breaking main thread */ }
        }
    }

    public void Enqueue(Action action)
    {
        if (action == null) return;
        lock (_queue) { _queue.Enqueue(action); }
    }
}