using UnityEngine;
using UnityEngine.InputSystem;

// Sprays water particles while the trigger button is held.
// Put this on the Washing gun object.
public class WaterGun : MonoBehaviour
{
    // The water spray particle system (a child at the tip of the gun).
    public ParticleSystem waterParticles;

    // The trigger button. Use "XRI Right Interaction/Activate Value".
    public InputActionReference triggerAction;

    void OnEnable()
    {
        // Make sure the trigger button is listening for input.
        triggerAction.action.Enable();
    }

    void Update()
    {
        // The trigger gives a value from 0 (released) to 1 (fully pressed).
        bool isPressed = triggerAction.action.ReadValue<float>() > 0.5f;

        if (isPressed && !waterParticles.isPlaying)
        {
            waterParticles.Play();
        }
        else if (!isPressed && waterParticles.isPlaying)
        {
            waterParticles.Stop();
        }
    }
}
