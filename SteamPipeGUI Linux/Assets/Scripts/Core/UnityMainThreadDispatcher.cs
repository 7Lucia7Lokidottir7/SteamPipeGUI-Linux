using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allows calling code on Unity's main thread from background threads.
/// Add this component to any persistent GameObject (e.g. GameManager).
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<Action> _queue = new Queue<Action>();
    private readonly object _lock = new object();

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        lock (_lock)
        {
            while (_queue.Count > 0)
                _queue.Dequeue().Invoke();
        }
    }

    /// <summary>
    /// Enqueue an action to be executed on the main thread.
    /// Can be called from any thread.
    /// </summary>
    public static void Enqueue(Action action)
    {
        lock (Instance._lock)
        {
            Instance._queue.Enqueue(action);
        }
    }
}
