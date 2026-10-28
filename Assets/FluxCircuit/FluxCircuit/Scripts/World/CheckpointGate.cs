using UnityEngine;

/// <summary>
/// Identifies one local, static checkpoint trigger in the shared course scene.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class CheckpointGate : MonoBehaviour
{
    [SerializeField, Range(0, FluxCircuitPlayer.TotalGates - 1)] private int gateIndex;

    public int GateIndex => gateIndex;

    private void Reset()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3f, 3f, 1.3f);
    }

    private void OnValidate()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }
}
