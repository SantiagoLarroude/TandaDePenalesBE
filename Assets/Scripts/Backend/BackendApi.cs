using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class BackendApi : MonoBehaviour
{
    public static BackendApi Instance { get; private set; }

    [SerializeField]
    private string baseUrl = "http://127.0.0.1:5001/tandadepenalesbe/us-central1";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =====================
    // PUBLIC API
    // =====================

    public IEnumerator StartShootout(string playerId, string opponentId, System.Action<string> onSuccess)
    {
        var payload = JsonUtility.ToJson(new StartShootoutRequest
        {
            playerId = playerId,
            opponentId = opponentId
        });

        yield return Post(
            "/startShootout",
            payload,
            response =>
            {
                var data = JsonUtility.FromJson<ShootoutResponse>(response);
                onSuccess?.Invoke(data.shootoutId);
            }
        );
    }

    public IEnumerator PersistEvent(string shootoutId, GameEvent gameEvent)
    {
        if (string.IsNullOrEmpty(shootoutId) || gameEvent == null)
            yield break;

        var payload = JsonUtility.ToJson(new PersistEventRequest
        {
            shootoutId = shootoutId,
            @event = gameEvent
        });

        yield return Post("/persistEvent", payload, null);
    }

    public IEnumerator FinishShootout(string shootoutId, Team winner, int playerScore, int aiScore)
    {
        var payload = JsonUtility.ToJson(new FinishShootoutRequest
        {
            shootoutId = shootoutId,
            winnerId = winner.ToString(),
            playerScore = playerScore,
            aiScore = aiScore
        });

        Debug.Log($"[BackendApi] Calling /finishShootout with shootoutId={shootoutId}");

        yield return Post("/finishShootout", payload, null);
    }

    // =====================
    // HTTP
    // =====================

    private IEnumerator Post(
        string path,
        string jsonBody,
        System.Action<string> onSuccess,
        System.Action<string> onError = null
    )
    {
        var request = new UnityWebRequest(baseUrl + path, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(request.downloadHandler.text);
        }
        else
        {
            var responseText = request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;

            Debug.LogError($"Backend error {request.responseCode}: {responseText}");
            onError?.Invoke(responseText);
        }
    }

    // =====================
    // DTOs
    // =====================

    [System.Serializable]
    private class StartShootoutRequest
    {
        public string playerId;
        public string opponentId;
    }

    [System.Serializable]
    private class FinishShootoutRequest
    {
        public string shootoutId;
        public string winnerId;
        public int playerScore;
        public int aiScore;
    }

    [System.Serializable]
    private class PersistEventRequest
    {
        public string shootoutId;
        public GameEvent @event;
    }

    [System.Serializable]
    private class ShootoutResponse
    {
        public string shootoutId;
    }

    [System.Serializable]
    public class GameEvent
    {
        public string type;
        public string turn;
        public string team;
        public string actor;
        public int playerScore;
        public int aiScore;
        public int playerShots;
        public int aiShots;
        public bool isSuddenDeath;
    }
}
