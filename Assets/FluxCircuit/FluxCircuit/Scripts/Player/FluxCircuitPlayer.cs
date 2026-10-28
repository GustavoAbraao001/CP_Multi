using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns local movement and route progress for one player's avatar.
/// Shared Mode State Authority writes this object's networked state.
/// The session master's avatar also carries the shared route and round index.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class FluxCircuitPlayer : NetworkBehaviour
{
    public const int TotalGates = 8;
    private const int TotalRoutes = 40320; // 8! different orders.

    [SerializeField, Min(1f)] private float moveSpeed = 10f;

    [Networked] public int NextGate { get; set; }
    [Networked] public int FinishTick { get; set; }
    [Networked] public NetworkBool IsSessionMaster { get; set; }
    [Networked] public int RoundId { get; set; }
    [Networked] public int RouteCode { get; set; }
    [Networked] public int RouteMultiplier { get; set; }
    [Networked] public int RouteOffset { get; set; }
    [Networked] public int RoundIndex { get; set; }

    private CharacterController _controller;
    private Renderer _bodyRenderer;
    private TrailRenderer _trail;
    private int _observedRoundId = int.MinValue;
    private bool _restartRequested;

    public override void Spawned()
    {
        _controller = GetComponent<CharacterController>();
        _bodyRenderer = GetComponent<Renderer>();
        _trail = GetComponent<TrailRenderer>();

        // Only the owning peer moves its controller. Proxies are visual replicas
        // driven by NetworkTransform and must not block the local player's course.
        if (_controller != null && !Object.HasStateAuthority)
            _controller.enabled = false;

        if (_trail != null)
            _trail.Clear();

        ApplyPlayerColor();

        if (Runner != null && Runner.IsSharedModeMasterClient && Object.HasStateAuthority)
            InitializeMasterRoute();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || Runner == null || _controller == null)
            return;

        // If the Shared Mode master changes, its local avatar becomes the new
        // session route owner and starts a fresh sequence for the remaining peers.
        if (Runner.IsSharedModeMasterClient && !IsSessionMaster)
            InitializeMasterRoute();

        if (IsSessionMaster && _restartRequested)
        {
            _restartRequested = false;
            StartNextRound();
        }

        if (!TryFindSessionMaster(Runner, out FluxCircuitPlayer master) ||
            master.RoundId <= 0 || master.RouteCode == 0)
            return;

        if (_observedRoundId != master.RoundId)
        {
            _observedRoundId = master.RoundId;
            NextGate = 0;
            FinishTick = 0;
        }

        if (CountPlayers(Runner) < 2 || TryFindWinner(Runner, out _))
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                  - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
        float z = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                  - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);

        Vector3 direction = new Vector3(x, 0f, z);
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        _controller.Move(direction * moveSpeed * Runner.DeltaTime);
        if (direction.sqrMagnitude > 0.001f)
            transform.forward = direction;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Object || !Object.HasStateAuthority || Runner == null)
            return;

        if (CountPlayers(Runner) < 2 || TryFindWinner(Runner, out _))
            return;

        CheckpointGate gate = other.GetComponent<CheckpointGate>();
        if (gate == null || !TryFindSessionMaster(Runner, out FluxCircuitPlayer master))
            return;

        if (_observedRoundId != master.RoundId || master.RouteCode == 0)
            return;

        int expectedGate = GetGateAt(master.RouteCode, NextGate);
        if (gate.GateIndex != expectedGate)
        {
            FluxCircuitAudio.PlayWrongGate();
            return;
        }

        FluxCircuitAudio.PlayCorrectGate();
        NextGate++;
        if (NextGate == TotalGates)
            FinishTick = Runner.Tick.Raw;
    }

    /// <summary>Can be called from either client; the session master performs the shared reset.</summary>
    public void RequestRestart()
    {
        if (Runner == null || !Runner.IsRunning || !IsSessionMaster)
            return;

        if (Object.HasStateAuthority)
            _restartRequested = true;
        else
            RPC_RequestRestart();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRestart()
    {
        if (Object.HasStateAuthority && IsSessionMaster)
            _restartRequested = true;
    }

    private void InitializeMasterRoute()
    {
        if (!Object.HasStateAuthority || Runner == null || !Runner.IsSharedModeMasterClient)
            return;

        IsSessionMaster = true;
        RouteMultiplier = ChooseRouteMultiplier();
        RouteOffset = UnityEngine.Random.Range(0, TotalRoutes);
        RoundIndex = 0;
        RouteCode = PackRoute(UnrankRoute(GetRouteRank(RoundIndex)));
        RoundId = RoundId <= 0 ? 1 : RoundId + 1;
        _observedRoundId = RoundId;
        NextGate = 0;
        FinishTick = 0;
    }

    private void StartNextRound()
    {
        if (!IsSessionMaster || !TryFindWinner(Runner, out _))
            return;

        RoundIndex++;
        if (RoundIndex >= TotalRoutes)
            RoundIndex = 0;

        RouteCode = PackRoute(UnrankRoute(GetRouteRank(RoundIndex)));
        RoundId++;
        if (RoundId <= 0)
            RoundId = 1;

        NextGate = 0;
        FinishTick = 0;
        _observedRoundId = RoundId;
    }

    private int GetRouteRank(int roundIndex)
    {
        long rank = ((long)RouteMultiplier * roundIndex + RouteOffset) % TotalRoutes;
        return (int)rank;
    }

    private static int ChooseRouteMultiplier()
    {
        int multiplier;
        do
        {
            multiplier = UnityEngine.Random.Range(1, TotalRoutes);
        }
        while (GreatestCommonDivisor(multiplier, TotalRoutes) != 1);

        return multiplier;
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            int remainder = a % b;
            a = b;
            b = remainder;
        }

        return a;
    }

    private static int[] UnrankRoute(int rank)
    {
        var available = new List<int>(TotalGates);
        for (int i = 0; i < TotalGates; i++)
            available.Add(i);

        var route = new int[TotalGates];
        for (int position = 0; position < TotalGates; position++)
        {
            int factorial = Factorial(TotalGates - position - 1);
            int selectedIndex = rank / factorial;
            rank %= factorial;
            route[position] = available[selectedIndex];
            available.RemoveAt(selectedIndex);
        }

        return route;
    }

    private static int Factorial(int value)
    {
        int result = 1;
        for (int i = 2; i <= value; i++)
            result *= i;
        return result;
    }

    private static int PackRoute(int[] route)
    {
        int code = 0;
        for (int i = 0; i < route.Length; i++)
            code |= (route[i] & 7) << (i * 3);
        return code;
    }

    public static int GetGateAt(int routeCode, int position)
    {
        if (position < 0 || position >= TotalGates)
            return -1;
        return (routeCode >> (position * 3)) & 7;
    }

    public static string GetRouteLabel(int routeCode)
    {
        string[] names = { "ÂMBAR", "CIANO", "VIOLETA", "LARANJA", "VERDE", "VERMELHO", "AZUL", "AMARELO" };
        var parts = new string[TotalGates];
        for (int i = 0; i < TotalGates; i++)
            parts[i] = names[GetGateAt(routeCode, i)];
        return string.Join(" → ", parts);
    }

    public static bool TryFindSessionMaster(NetworkRunner runner, out FluxCircuitPlayer master)
    {
        master = null;
        if (runner == null || !runner.IsRunning)
            return false;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject))
                continue;

            FluxCircuitPlayer candidate = playerObject.GetComponent<FluxCircuitPlayer>();
            if (candidate != null && candidate.IsSessionMaster)
            {
                master = candidate;
                return true;
            }
        }

        return false;
    }

    private void ApplyPlayerColor()
    {
        if (_bodyRenderer == null || Object == null)
            return;

        Color color = Object.StateAuthority.PlayerId % 2 == 1
            ? new Color(0.18f, 0.82f, 1f)
            : new Color(1f, 0.35f, 0.31f);

        _bodyRenderer.material.color = color;
        if (_bodyRenderer.material.HasProperty("_EmissionColor"))
        {
            _bodyRenderer.material.EnableKeyword("_EMISSION");
            _bodyRenderer.material.SetColor("_EmissionColor", color * 0.35f);
        }

        if (_trail != null)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.72f, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = gradient;
            _trail.Clear();
        }
    }

    public static bool TryFindWinner(NetworkRunner runner, out PlayerRef winner)
    {
        winner = default;
        if (runner == null || !runner.IsRunning)
            return false;

        bool found = false;
        int bestTick = int.MaxValue;
        int bestPlayerId = int.MaxValue;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject))
                continue;

            FluxCircuitPlayer avatar = playerObject.GetComponent<FluxCircuitPlayer>();
            if (avatar == null || avatar.FinishTick <= 0)
                continue;

            if (!found || avatar.FinishTick < bestTick ||
                (avatar.FinishTick == bestTick && player.PlayerId < bestPlayerId))
            {
                found = true;
                bestTick = avatar.FinishTick;
                bestPlayerId = player.PlayerId;
                winner = player;
            }
        }

        return found;
    }

    private static int CountPlayers(NetworkRunner runner)
    {
        int count = 0;
        foreach (PlayerRef _ in runner.ActivePlayers)
            count++;
        return count;
    }
}
