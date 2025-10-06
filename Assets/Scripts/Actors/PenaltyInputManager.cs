using UnityEngine;

public class PenaltyInputManager : MonoBehaviour
{
    [SerializeField]
    private MonoBehaviour _playerKickable; // IKickable
    private IKickable playerKick => _playerKickable as IKickable;

    [Header("Kick Controls")]
    [SerializeField]
    private KeyCode _kickLeft = KeyCode.A;

    [SerializeField]
    private KeyCode _kickRight = KeyCode.D;

    [SerializeField]
    private KeyCode _kickCenter = KeyCode.S;

    [SerializeField]
    private KeyCode _kick = KeyCode.Space;

    [Header("Kick Power")]
    [SerializeField]
    private float _minPower = 5f;

    [SerializeField]
    private float _maxPower = 20f;

    [SerializeField]
    private float _chargeSpeed = 10f; // velocidad de carga de poder
    private float _currentPower;
    private bool _charging;
    private bool canKick = true;

    [Header("UI")]
    [SerializeField]
    private UIManager uiManager;

    private void Start()
    {
        if (EventManager.instance == null)
            return;

        _currentPower = _minPower;
        EventManager.instance.OnRoundStart += EnableKick;

        if (uiManager != null)
            uiManager.ShowPowerBar();
    }

    private void OnDestroy()
    {
        if (EventManager.instance == null)
            return;

        EventManager.instance.OnRoundStart -= EnableKick;
    }

    private void Update()
    {
        PenaltyGameManager manager = FindObjectOfType<PenaltyGameManager>();
        if (manager.GetCurrentTurn() != Team.Player)
            return;

        if (!canKick)
            return;

        HandlePlayerInput();
    }

    private void HandlePlayerInput()
    {
        if (playerKick == null)
            return;

        // Dirección del tiro
        if (Input.GetKey(_kickLeft))
            playerKick.KickDirection = -1;
        else if (Input.GetKey(_kickRight))
            playerKick.KickDirection = 1;
        else if (Input.GetKey(_kickCenter))
            playerKick.KickDirection = 0;

        // 🔹 Si empieza a cargar (apretar espacio)
        if (Input.GetKeyDown(_kick))
        {
            _charging = true;
            _currentPower = _minPower;

            if (uiManager != null)
                uiManager.ShowPowerBar();
        }

        // 🔹 Mientras se mantiene presionada la barra espaciadora → carga de poder
        if (_charging && Input.GetKey(_kick))
        {
            _currentPower += _chargeSpeed * Time.deltaTime;
            _currentPower = Mathf.Clamp(_currentPower, _minPower, _maxPower);
            playerKick.CurrentPower = _currentPower;

            // actualizar visualmente la barra
            if (uiManager != null)
            {
                float normalizedPower = (_currentPower - _minPower) / (_maxPower - _minPower);

                uiManager.UpdatePowerBar(normalizedPower);
            }
        }

        // 🔹 Cuando se suelta la barra espaciadora → ejecutar el kick
        if (Input.GetKeyUp(_kick))
        {
            _charging = false;

            var kickCmd = new KickCommand(playerKick);
            EventQueueManager.Instance.AddCommand(kickCmd);

            canKick = false; // deshabilitar input hasta el siguiente turno
        }
    }

    // 🔹 Método público para habilitar el input al inicio de un turno
    public void EnableKick()
    {
        canKick = true;
        playerKick.CurrentPower = 0;
        playerKick.KickDirection = 0;
    }
}
