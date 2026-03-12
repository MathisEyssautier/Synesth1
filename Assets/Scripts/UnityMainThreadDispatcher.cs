using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<Action> _queue = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance() => _instance;

    void Awake()
    {
        if (_instance == null) { _instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    void Update()
    {
        while (_queue.Count > 0)
            _queue.Dequeue().Invoke();
    }

    public void Enqueue(Action action)
    {
        lock (_queue) { _queue.Enqueue(action); }
    }
}