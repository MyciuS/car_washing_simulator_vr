using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A hand spray bottle (foam soap, grime remover, wax...). Hold it and pull the trigger to spray.
// Each bottle holds a limited amount: the liquid inside goes down while spraying, and an empty
// bottle doesn't spray any more (buy a new one in the shop).
// What the spray does is set on its particle system's SprayCleaner (a CleaningTool asset).
// Put this on the bottle, next to its XR Grab Interactable.
[RequireComponent(typeof(XRGrabInteractable))]
public class SprayBottle : MonoBehaviour
{
    // The spray (a child particle system at the nozzle, with a SprayCleaner on it).
    public ParticleSystem spray;
    // How many seconds of spraying a full bottle holds.
    public float sprayTime = 25f;
    // The liquid inside the bottle (its origin at the bottom); it gets shorter as the bottle empties.
    public Transform liquid;
    // The trigger lever, pulled in while spraying (optional).
    public Transform trigger;

    XRGrabInteractable grab;
    float secondsLeft;
    bool triggerHeld;
    Vector3 liquidSize;
    Quaternion triggerRest;

    // How full the bottle is (1 = full, 0 = empty).
    public float Fullness => sprayTime > 0f ? secondsLeft / sprayTime : 0f;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.activated.AddListener(args => triggerHeld = true);
        grab.deactivated.AddListener(args => triggerHeld = false);
        grab.selectExited.AddListener(args => triggerHeld = false);
        secondsLeft = sprayTime;
        if (liquid != null)
            liquidSize = liquid.localScale;
        if (trigger != null)
            triggerRest = trigger.localRotation;
    }

    void Update()
    {
        bool spraying = triggerHeld && secondsLeft > 0f;
        if (spraying)
            secondsLeft = Mathf.Max(0f, secondsLeft - Time.deltaTime);

        if (spraying && !spray.isPlaying)
            spray.Play();
        else if (!spraying && spray.isPlaying)
            spray.Stop();

        if (liquid != null)
        {
            liquid.localScale = new Vector3(liquidSize.x, liquidSize.y * Mathf.Max(0.001f, Fullness), liquidSize.z);
            liquid.gameObject.SetActive(secondsLeft > 0f);
        }
        if (trigger != null)
            trigger.localRotation = triggerHeld ? triggerRest * Quaternion.Euler(-20f, 0f, 0f) : triggerRest;
    }
}
