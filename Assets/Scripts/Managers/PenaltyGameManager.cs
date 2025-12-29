using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PenaltyGameManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField]
    private int shotsPerTeam = 5;

    [Header("Game Objects")]
    [SerializeField]
    private Ball ball;

    [SerializeField]
    private GameObject playerKicker;

    [SerializeField]
    private GameObject aiKicker;

    [SerializeField]
    private GameObject playerKeeper;

    [SerializeField]
    private GameObject aiKeeper;

    [Header("Start Positions")]
    [SerializeField]
    private Transform ballStartPos;

    [SerializeField]
    private Transform playerKickerStartPos;

    [SerializeField]
    private Transform aiKickerStartPos;

    [SerializeField]
    private Transform playerKeeperStartPos;

    [SerializeField]
    private Transform aiKeeperStartPos;

    [Header("Timing")]
    [SerializeField]
    private float goalCheckTimeout = 3f;

    [SerializeField]
    private float nextTurnDelay = 2f;

    [Header("UI")]
    [SerializeField]
    private UIManager uiManager; // 🔹 referencia a UIManager

    private int playerScore = 0;
    private int aiScore = 0;
    private int currentPlayerShots = 0;
    private int currentAIShots = 0;

    private int playerShotIndex = 0; // 🔹 tracking para UI
    private int aiShotIndex = 0;

    private Team currentTurn = Team.Player;
    private bool isSuddenDeath = false;

    private bool ballEnteredGoal = false;
    private Team ballKickerTeam;
    private bool turnResolved = false;


    
    [SerializeField]
    private BackendApi backendApi;

    [SerializeField]
    private string shootoutId;

    private readonly List<BackendApi.GameEvent> pendingBackendEvents = new List<BackendApi.GameEvent>();

    private void Start()
    {
        Debug.Log(" ++++++ PenaltyGameManager started (before backendApi check) ++++++ ");

        backendApi = BackendApi.Instance;

        Debug.Log(" ++++++ PenaltyGameManager started (after backendApi check) ++++++ ");

        ResetGame();

        if (EventManager.instance == null)
        {
            return;
        }

        EventManager.instance.OnBallKicked += OnBallKicked;
        EventManager.instance.OnGoalScored += OnGoalScored;
        EventManager.instance.OnMissedShot += OnMissedShot;

        // Inicializar la UI
        if (uiManager != null)
        {
            uiManager.InitializeUI();
        }

        if (backendApi != null)
        {
            StartCoroutine(InitializeShootoutAndStartGame());
        }
        else
        {
            Debug.LogWarning(" ++++++ BackendApi not found, running without persistence ++++++ ");
            SetupTurn();
            EventManager.instance?.EventRoundStart();
        }
    }

    private IEnumerator InitializeShootoutAndStartGame()
    {
        string receivedId = null;

        yield return StartCoroutine(
            backendApi.StartShootout("playerKicker.ID#1", "aiKicker.ID#1", id =>
            {
                receivedId = id;
                Debug.Log($" ++++++ Shootout ID received: {id} ++++++ ");
            })
        );

        shootoutId = receivedId;

        QueueOrSendBackendEvent(BuildBackendEvent("SHOOTOUT_STARTED", null, null));
        FlushPendingBackendEvents();

        SetupTurn();
        EventManager.instance?.EventRoundStart();
    }

    private void ResetGame()
    {
        playerScore = 0;
        aiScore = 0;
        currentPlayerShots = 0;
        currentAIShots = 0;
        playerShotIndex = 0;
        aiShotIndex = 0;
        currentTurn = Team.Player;
        isSuddenDeath = false;

        ballEnteredGoal = false;
        turnResolved = false;
    }

    private void OnDestroy()
    {
        if (EventManager.instance == null)
            return;

        EventManager.instance.OnBallKicked -= OnBallKicked;
        EventManager.instance.OnGoalScored -= OnGoalScored;
        EventManager.instance.OnMissedShot -= OnMissedShot;
    }

    private void SetupTurn()
    {
        turnResolved = false;

        bool isPlayerTurn = currentTurn == Team.Player;

        uiManager.ResetTexts();

        Debug.Log(
            $"--------- RESULT: Player {playerScore} - AI {aiScore} --------- TURNO: {currentTurn}"
        );

        if (isPlayerTurn)
            uiManager?.ShowPowerBar();
        else
            uiManager?.HidePowerBar();

        // Kicker
        if (playerKicker != null)
        {
            playerKicker.SetActive(isPlayerTurn);
            playerKicker.transform.position = playerKickerStartPos.position;
            playerKicker.transform.rotation = playerKickerStartPos.rotation;
        }

        // AI Kicker
        if (aiKicker != null)
        {
            aiKicker.SetActive(!isPlayerTurn);
            aiKicker.transform.position = aiKickerStartPos.position;
            aiKicker.transform.rotation = aiKickerStartPos.rotation;
        }

        // Keeper
        if (playerKeeper != null)
        {
            playerKeeper.SetActive(!isPlayerTurn);
            playerKeeper.transform.position = playerKeeperStartPos.position;
            playerKeeper.transform.rotation = playerKeeperStartPos.rotation;

            KeeperMovement km = playerKeeper.GetComponent<KeeperMovement>();
            KeeperKeepable kk = playerKeeper.GetComponent<KeeperKeepable>();
            if (km != null && kk != null)
                km.ResetKeeper(kk, playerKeeperStartPos.position);

            if (!isPlayerTurn)
            {
                var keeperInput = playerKeeper.GetComponent<KeeperInputManager>();
                keeperInput?.EnableInputForNextKick();
            }
        }

        // AI Keeper
        if (aiKeeper != null)
        {
            aiKeeper.SetActive(isPlayerTurn);
            aiKeeper.transform.position = aiKeeperStartPos.position;
            aiKeeper.transform.rotation = aiKeeperStartPos.rotation;
            aiKeeper.GetComponent<AIKeeper>().ResetPosition();
        }

        // Reset pelota
        if (ball != null)
        {
            ball.transform.position = ballStartPos.position;
            ball.transform.rotation = ballStartPos.rotation;
            ball.ResetBall();
        }

        if (!isPlayerTurn)
        {
            EventManager.instance?.EventAIKickingTurn();
        }

        QueueOrSendBackendEvent(BuildBackendEvent("ROUND_SETUP", currentTurn, null));
    }

    private void OnBallKicked(IKickable kicker)
    {
        if (kicker == null)
            return;

        string actorName = null;
        var kickerComponent = kicker as Component;
        if (kickerComponent != null)
        {
            actorName = kickerComponent.gameObject != null ? kickerComponent.gameObject.name : null;
        }

        ballKickerTeam = currentTurn;
        ballEnteredGoal = false;

        QueueOrSendBackendEvent(BuildBackendEvent("BALL_KICKED", currentTurn, actorName));

        StartCoroutine(WaitForGoalOrTimeout(ballKickerTeam));
    }

    private IEnumerator WaitForGoalOrTimeout(Team kickerTeam)
    {
        if (turnResolved)
            yield break;
        turnResolved = true;
        ballEnteredGoal = false;

        float timer = 0f;
        while (timer < goalCheckTimeout)
        {
            if (ballEnteredGoal)
                break;
            timer += Time.deltaTime;
            yield return null;
        }

        if (currentTurn == kickerTeam)
        {
            if (ballEnteredGoal)
            {
                Debug.Log($"⚽ ¡GOL del equipo {kickerTeam}!");
                EventManager.instance?.EventGoalScored(kickerTeam);
            }
            else
            {
                Debug.Log($"❌ Tiro fallado del equipo {kickerTeam}");
                EventManager.instance?.EventMissedShot(kickerTeam);
            }
        }

        ballEnteredGoal = false;
    }

    public void BallEnteredGoal() => ballEnteredGoal = true;

    private void OnGoalScored(Team kickingTeam)
    {
        if (kickingTeam == Team.Player)
            playerScore++;
        else
            aiScore++;

        // 🔹 Actualizar UI
        if (uiManager != null)
        {
            if (kickingTeam == Team.Player)
                uiManager.SetPlayerAttempt(playerShotIndex++, true);
            else
                uiManager.SetAIAttempt(aiShotIndex++, true);
        }

        IncrementShotCounter(kickingTeam);

        QueueOrSendBackendEvent(BuildBackendEvent("GOAL", kickingTeam, null));

        CheckEndCondition();
        EndTurnWithDelay();
    }

    private void OnMissedShot(Team kickingTeam)
    {
        // 🔹 Actualizar UI
        if (uiManager != null)
        {
            if (kickingTeam == Team.Player)
                uiManager.SetPlayerAttempt(playerShotIndex++, false);
            else
                uiManager.SetAIAttempt(aiShotIndex++, false);
        }

        IncrementShotCounter(kickingTeam);

        QueueOrSendBackendEvent(BuildBackendEvent("MISS", kickingTeam, null));

        CheckEndCondition();
        EndTurnWithDelay();
    }

    private void IncrementShotCounter(Team kickingTeam)
    {
        if (kickingTeam == Team.Player)
            currentPlayerShots++;
        else
            currentAIShots++;
    }

    private void EndTurnWithDelay()
    {
        StartCoroutine(DelayedSetupTurn());
    }

    private IEnumerator DelayedSetupTurn()
    {
        yield return new WaitForSeconds(nextTurnDelay);

        currentTurn = (currentTurn == Team.Player) ? Team.AI : Team.Player;

        // 🔹 En Sudden Death, resetear los slots para mostrar sólo 1 dash
        if (isSuddenDeath && currentPlayerShots == currentAIShots)
        {
            if (uiManager != null)
            {
                uiManager.ResetPenaltyScores();
                playerShotIndex = 0;
                aiShotIndex = 0;
            }
        }

        SetupTurn();
        EventManager.instance?.EventRoundStart();
    }

    private void CheckEndCondition()
    {
        int remainingPlayerShots = shotsPerTeam - currentPlayerShots;
        int remainingAIShots = shotsPerTeam - currentAIShots;

        if (!isSuddenDeath)
        {
            if (playerScore > (aiScore + remainingAIShots))
            {
                Debug.Log(" -------- GANADOR: Player --------");
                LoadEndScene(true);
                return;
            }

            if (aiScore > (playerScore + remainingPlayerShots))
            {
                Debug.Log(" -------- GANADOR: AI --------");
                LoadEndScene(false);
                return;
            }

            if (currentPlayerShots >= shotsPerTeam && currentAIShots >= shotsPerTeam)
            {
                if (playerScore == aiScore)
                {
                    isSuddenDeath = true;
                    shotsPerTeam++;

                    uiManager?.EnableSuddenDeathMode();

                    QueueOrSendBackendEvent(BuildBackendEvent("SUDDEN_DEATH_STARTED", null, null));
                }
                else
                {
                    bool playerWon = playerScore > aiScore;
                    Debug.Log($" -------- GANADOR: {(playerWon ? Team.Player : Team.AI)} --------");
                    LoadEndScene(playerWon);
                }
            }
        }
        else if (isSuddenDeath && currentPlayerShots == currentAIShots)
        {
            if (playerScore != aiScore)
            {
                bool playerWon = playerScore > aiScore;
                Debug.Log($" -------- GANADOR: {(playerWon ? Team.Player : Team.AI)} --------");
                LoadEndScene(playerWon);
            }
        }
    }

    public Team GetCurrentTurn() => currentTurn;

    private void LoadEndScene(bool playerWon)
    {
        StartCoroutine(LoadEndSceneCoroutine(playerWon));
    }

    private IEnumerator LoadEndSceneCoroutine(bool playerWon)
    {
        Debug.Log(" ++++++ backendApi Loading end scene... ++++++ ");

        Team winner = playerWon ? Team.Player : Team.AI;
        QueueOrSendBackendEvent(BuildBackendEvent("GAME_OVER", winner, null));
        FlushPendingBackendEvents();

        Debug.Log($" ++++++ backendApi Calling FinishShootout with shootoutId: {shootoutId}, winner: {winner}, playerScore: {playerScore}, aiScore: {aiScore} ++++++ ");
        if (backendApi != null && !string.IsNullOrEmpty(shootoutId))
        {
            Debug.Log(" ++++++ backendApi Waiting FinishShootout... ++++++ ");
            yield return StartCoroutine(
                backendApi.FinishShootout(
                    shootoutId,
                    winner,
                    playerScore,
                    aiScore
                )
            );
        }

        Debug.Log(" ++++++ backendApi Finished API call, loading scene... ++++++ ");
        string sceneName = playerWon ? "VictoryScene" : "DefeatScene";
        SceneManager.LoadScene(sceneName);
    }

    private BackendApi.GameEvent BuildBackendEvent(string type, Team? team, string actor)
    {
        return new BackendApi.GameEvent
        {
            type = type,
            turn = currentTurn.ToString(),
            team = team.HasValue ? team.Value.ToString() : null,
            actor = actor,
            playerScore = playerScore,
            aiScore = aiScore,
            playerShots = currentPlayerShots,
            aiShots = currentAIShots,
            isSuddenDeath = isSuddenDeath,
        };
    }

    private void QueueOrSendBackendEvent(BackendApi.GameEvent gameEvent)
    {
        if (backendApi == null)
            return;

        if (string.IsNullOrEmpty(shootoutId))
        {
            pendingBackendEvents.Add(gameEvent);
            return;
        }

        StartCoroutine(backendApi.PersistEvent(shootoutId, gameEvent));
    }

    private void FlushPendingBackendEvents()
    {
        if (backendApi == null)
            return;
        if (string.IsNullOrEmpty(shootoutId))
            return;
        if (pendingBackendEvents.Count == 0)
            return;

        for (int i = 0; i < pendingBackendEvents.Count; i++)
        {
            StartCoroutine(backendApi.PersistEvent(shootoutId, pendingBackendEvents[i]));
        }

        pendingBackendEvents.Clear();
    }
}
