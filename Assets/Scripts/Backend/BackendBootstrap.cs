using UnityEngine;

public static class BackendBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (BackendApi.Instance != null)
            return;

        var go = new GameObject("BackendApi");
        go.AddComponent<BackendApi>();
    }
}
