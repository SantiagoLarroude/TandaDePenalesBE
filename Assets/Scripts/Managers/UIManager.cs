using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Player UI")]
    [SerializeField] private TextMeshProUGUI playerMessage;
    [SerializeField] private Image[] playerPenaltyScores;

    [Header("AI UI")]
    [SerializeField] private TextMeshProUGUI aiMessage;
    [SerializeField] private Image[] aiPenaltyScores;

    [Header("Sprites")]
    [SerializeField] private Sprite emptyAttempt;
    [SerializeField] private Sprite goalAttempt;
    [SerializeField] private Sprite missAttempt;

    [Header("Power Bar")]
    [SerializeField] private GameObject playerPowerBarGroup; 
    [SerializeField] private Image playerPowerBar;

    public void InitializeUI()
    {
        ResetPenaltyScores();
        playerMessage.text = "";
        aiMessage.text = "";
        ShowPowerBar();
    }

    public void ResetPenaltyScores()
    {
        foreach (var img in playerPenaltyScores)
            img.sprite = emptyAttempt;

        foreach (var img in aiPenaltyScores)
            img.sprite = emptyAttempt;
    }

    public void UpdatePlayerMessage(string message)
    {
        playerMessage.text = message;
    }

    public void UpdateAIMessage(string message)
    {
        aiMessage.text = message;
    }

    public void SetPlayerAttempt(int index, bool isGoal)
    {
        if (index < 0 || index >= playerPenaltyScores.Length)
            return;

        playerPenaltyScores[index].sprite = isGoal ? goalAttempt : missAttempt;
        playerMessage.text = isGoal ? "Player Scores!" : "Player Misses!";
    }

    public void SetAIAttempt(int index, bool isGoal)
    {
        if (index < 0 || index >= aiPenaltyScores.Length)
            return;

        aiPenaltyScores[index].sprite = isGoal ? goalAttempt : missAttempt;
        aiMessage.text = isGoal ? "AI Scores!" : "AI Misses!";
    }

    public void EnableSuddenDeathMode()
    {
        ResetPenaltyScores();

        for (int i = 1; i < playerPenaltyScores.Length; i++)
            playerPenaltyScores[i].gameObject.SetActive(false);

        for (int i = 1; i < aiPenaltyScores.Length; i++)
            aiPenaltyScores[i].gameObject.SetActive(false);
    }

    public void ResetTexts()
    {
        playerMessage.text = "";
        aiMessage.text = "";
    }

    // --- 🟢 POWER BAR CONTROL ---
    public void ShowPowerBar()
    {
        if (playerPowerBarGroup != null)
            playerPowerBarGroup.SetActive(true);

        UpdatePowerBar(0);
    }

    public void HidePowerBar()
    {
        if (playerPowerBarGroup != null)
            playerPowerBarGroup.SetActive(false);
    }

    public void UpdatePowerBar(float normalizedPower)
    {
        if (playerPowerBar == null) return;
        playerPowerBar.fillAmount = Mathf.Clamp01(normalizedPower);
        Debug.Log($"Power Bar Updated: {playerPowerBar.fillAmount}");
    }
}
