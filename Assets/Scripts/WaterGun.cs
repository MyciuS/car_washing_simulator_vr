using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Sprays water particles while the gun is held and the trigger is pressed.
// Put this on the Washing gun object, next to the XR Grab Interactable.
[RequireComponent(typeof(XRGrabInteractable))]
public class WaterGun : MonoBehaviour
{
    // The water spray particle system (a child at the tip of the gun).
    public ParticleSystem waterParticles;

    XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        // "Activated" = trigger pressed while holding the gun.
        grabInteractable.activated.AddListener(StartSpray);
        // "Deactivated" = trigger released.
        grabInteractable.deactivated.AddListener(StopSpray);
        // "Select Exited" = gun let go, so stop spraying too.
        grabInteractable.selectExited.AddListener(StopSpray);
    }

    void StartSpray(ActivateEventArgs args)
    {
        waterParticles.Play();
    }

    void StopSpray(DeactivateEventArgs args)
    {
        waterParticles.Stop();
    }

    void StopSpray(SelectExitEventArgs args)
    {
        waterParticles.Stop();
    }
}
