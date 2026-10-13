using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Sprays while the gun is held and the trigger is pressed.
// Without a head it shoots its normal water jet. If a SprayHead is clicked onto
// the tip (the head socket), it sprays through that head instead.
// Put this on the Washing gun object, next to the XR Grab Interactable.
[RequireComponent(typeof(XRGrabInteractable))]
public class WaterGun : MonoBehaviour
{
    // The normal water jet (a child particle system at the tip of the gun).
    public ParticleSystem waterParticles;

    // The socket on the tip where heads click on (XR Socket Interactor).
    public XRSocketInteractor headSocket;

    XRGrabInteractable grabInteractable;
    ParticleSystem currentSpray;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        // "Activated" = trigger pressed while holding the gun.
        grabInteractable.activated.AddListener(StartSpray);
        // "Deactivated" = trigger released.
        grabInteractable.deactivated.AddListener(StopSpray);
        // "Select Exited" = gun let go, so stop spraying too.
        grabInteractable.selectExited.AddListener(StopSpray);

        // Putting a head on or taking it off stops the spray (press the trigger again).
        headSocket.selectEntered.AddListener(HeadChanged);
        headSocket.selectExited.AddListener(HeadChanged);
    }

    void StartSpray(ActivateEventArgs args)
    {
        currentSpray = GetSpray();
        currentSpray.Play();
    }

    void StopSpray(DeactivateEventArgs args)
    {
        StopSpray();
    }

    void StopSpray(SelectExitEventArgs args)
    {
        StopSpray();
    }

    void HeadChanged(BaseInteractionEventArgs args)
    {
        StopSpray();
    }

    void StopSpray()
    {
        if (currentSpray != null)
            currentSpray.Stop();
    }

    // The head's spray if a head is on the tip, otherwise the normal water jet.
    ParticleSystem GetSpray()
    {
        if (headSocket.hasSelection)
        {
            SprayHead head = headSocket.firstInteractableSelected.transform.GetComponent<SprayHead>();
            if (head != null)
                return head.spray;
        }
        return waterParticles;
    }
}
