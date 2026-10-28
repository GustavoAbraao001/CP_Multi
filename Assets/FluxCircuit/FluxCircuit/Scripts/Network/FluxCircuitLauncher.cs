using System.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// Connects to a Shared Mode session and creates the local player's network avatar.
/// This component must sit on the same GameObject as NetworkRunner.
/// </summary>
public sealed class FluxCircuitLauncher : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private string defaultSessionName = "FluxCircuit01";

    private NetworkRunner _runner;
    private string _sessionName;
    private string _status = "Digite um nome de sala e conecte.";
    private bool _connecting;
    private GUIStyle _titleStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _smallStyle;

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
        _sessionName = defaultSessionName;

        if (_runner == null)
        {
            Debug.LogError("FluxCircuitLauncher precisa estar no mesmo GameObject que NetworkRunner.");
        }
    }

    private async void ConnectToSession()
    {
        if (_runner == null || _runner.IsRunning || _connecting)
            return;

        string room = string.IsNullOrWhiteSpace(_sessionName)
            ? defaultSessionName
            : _sessionName.Trim();

        if (playerPrefab == null)
        {
            _status = "Prefab FluxPlayer não foi atribuído no Inspector.";
            return;
        }

        _connecting = true;
        _status = "Conectando ao Photon...";

        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = room,
            PlayerCount = 2
        });

        _connecting = false;
        if (result.Ok)
        {
            _sessionName = room;
            _status = "Conectado. Aguardando os jogadores...";
        }
        else
        {
            _status = "Falha na conexão: " + result.ShutdownReason;
            Debug.LogError(_status);
        }
    }

    void IPlayerJoined.PlayerJoined(PlayerRef player)
    {
        if (Runner == null || !Runner.IsRunning)
            return;

        if (player == Runner.LocalPlayer)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("Atribua FluxPlayer ao campo Player Prefab do FluxCircuitLauncher.");
                return;
            }

            if (!Runner.TryGetPlayerObject(player, out _))
            {
                bool isMasterClient = Runner.IsSharedModeMasterClient;
                Vector3 spawnPosition = isMasterClient
                    ? new Vector3(-24f, 1f, -24f)
                    : new Vector3(24f, 1f, -24f);

                NetworkObject playerObject = Runner.Spawn(
                    playerPrefab,
                    spawnPosition,
                    Quaternion.identity);

                Runner.SetPlayerObject(player, playerObject);
            }
        }

        _status = Runner.IsSharedModeMasterClient
            ? "Você criou a sessão. Aguardando o segundo jogador..."
            : "Você entrou na sessão. Aguardando os jogadores...";
    }

    void IPlayerLeft.PlayerLeft(PlayerRef player)
    {
        if (Runner == null || !Runner.IsRunning)
            return;

        _status = CountPlayers() < 2
            ? "O outro jogador saiu. Aguardando uma nova conexão..."
            : "Um jogador saiu da sessão.";
    }

    private int CountPlayers()
    {
        if (_runner == null || !_runner.IsRunning)
            return 0;

        int count = 0;
        foreach (PlayerRef player in _runner.ActivePlayers)
            count++;

        return count;
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (_runner != null && _runner.IsRunning)
            DrawGameHud();
        else
            DrawConnectMenu();
    }

    private void DrawConnectMenu()
    {
        float width = Mathf.Min(470f, Screen.width - 32f);
        float height = Mathf.Min(350f, Screen.height - 32f);
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, GUIContent.none);

        GUILayout.BeginArea(new Rect(panel.x + 28f, panel.y + 22f, panel.width - 56f, panel.height - 44f));
        GUILayout.Label("FLUX CIRCUIT", _titleStyle);
        GUILayout.Space(8f);
        GUILayout.Label("Corrida multiplayer por portais", _bodyStyle);
        GUILayout.Space(18f);
        GUILayout.Label("Nome da sala (os dois clientes usam o mesmo)", _smallStyle);
        _sessionName = GUILayout.TextField(_sessionName, 40, GUILayout.Height(34f));
        GUILayout.Space(14f);

        GUI.enabled = !_connecting;
        string buttonLabel = _connecting ? "CONECTANDO..." : "CRIAR / ENTRAR NA SALA";
        if (GUILayout.Button(buttonLabel, GUILayout.Height(44f)))
            ConnectToSession();
        GUI.enabled = true;

        GUILayout.Space(10f);
        GUILayout.Label(_status, _smallStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label("A sessão Shared Mode aceita até 2 jogadores.", _smallStyle);
        GUILayout.EndArea();
    }

    private void DrawGameHud()
    {
        float panelWidth = Mathf.Min(520f, Screen.width - 28f);
        float panelHeight = Mathf.Min(350f, Screen.height - 28f);
        Rect panel = new Rect(14f, 14f, panelWidth, panelHeight);
        GUI.Box(panel, GUIContent.none);
        GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));

        GUILayout.Label("FLUX CIRCUIT  /  " + _sessionName, _titleStyle);
        int playerCount = CountPlayers();
        GUILayout.Label(playerCount < 2 ? "Aguardando o segundo jogador..." : "WASD ou setas: atravesse os 8 portais", _smallStyle);
        GUILayout.Space(8f);

        bool hasRoute = FluxCircuitPlayer.TryFindSessionMaster(_runner, out FluxCircuitPlayer routeOwner)
                        && routeOwner.RouteCode != 0;
        if (hasRoute)
        {
            GUILayout.Label("ROTA: " + FluxCircuitPlayer.GetRouteLabel(routeOwner.RouteCode), _smallStyle);
            GUILayout.Space(5f);
        }

        bool hasWinner = FluxCircuitPlayer.TryFindWinner(_runner, out PlayerRef winner);
        foreach (PlayerRef player in _runner.ActivePlayers)
        {
            string owner = player == _runner.LocalPlayer ? "VOCÊ" : "ADVERSÁRIO";
            if (!_runner.TryGetPlayerObject(player, out NetworkObject playerObject))
            {
                GUILayout.Label("JOGADOR " + player.PlayerId + "  /  " + owner + "  /  entrando...", _bodyStyle);
                continue;
            }

            FluxCircuitPlayer avatar = playerObject.GetComponent<FluxCircuitPlayer>();
            if (avatar == null)
                continue;

            string progress = avatar.FinishTick > 0
                ? "CHEGOU!"
                : "PORTAIS " + avatar.NextGate + " / " + FluxCircuitPlayer.TotalGates;

            GUILayout.Label("JOGADOR " + player.PlayerId + "  /  " + owner + "  /  " + progress, _bodyStyle);
        }

        GUILayout.Space(7f);
        if (hasWinner)
        {
            string outcome = winner == _runner.LocalPlayer ? "VOCÊ VENCEU!" : "JOGADOR " + winner.PlayerId + " VENCEU";
            GUILayout.Label(outcome, _titleStyle);
            if (GUILayout.Button("REINICIAR CORRIDA", GUILayout.Height(38f)) && hasRoute)
                routeOwner.RequestRestart();
        }
        else if (playerCount >= 2)
        {
            GUILayout.Label("Acerte a ordem mostrada. Erros não contam e tocam um alerta.", _smallStyle);
        }

        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null)
            return;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.35f, 0.95f, 1f) }
        };
        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        _smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = new Color(0.78f, 0.84f, 0.9f) }
        };
    }
}
