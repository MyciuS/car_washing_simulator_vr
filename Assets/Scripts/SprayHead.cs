using UnityEngine;

// A nozzle head that clicks onto the tip of the water gun (for example a wide
// fan head or a soap head). While it is on the gun, the gun sprays through
// this head's particle system instead of its normal jet.
// Put this on the head, next to its XR Grab Interactable.
public class SprayHead : MonoBehaviour
{
    // What comes out of this head (a child particle system at the front of the head).
    public ParticleSystem spray;
}
