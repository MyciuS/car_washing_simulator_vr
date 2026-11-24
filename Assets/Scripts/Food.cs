using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Food or a drink from the shop. Hold it up to your mouth to take a bite (or a sip).
// Each bite fills the player's food (and energy, for coffee and energy drinks) a bit;
// after the last bite it's gone.
// Put this on the food, next to its XR Grab Interactable.
[RequireComponent(typeof(XRGrabInteractable))]
public class Food : MonoBehaviour
{
    // How much food and energy the whole thing gives (out of 100).
    public float food = 30f;
    public float energy = 0f;
    // How many bites (or sips) it takes to finish it.
    public int bites = 3;
    // How close to the mouth it has to be held, in meters.
    public float mouthDistance = 0.17f;
    // Seconds between bites.
    public float biteInterval = 0.8f;

    XRGrabInteractable grab;
    PlayerStats stats;
    int bitesLeft;
    float nextBite;
    Vector3 fullSize;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        bitesLeft = bites;
        fullSize = transform.localScale;
    }

    void Start()
    {
        stats = FindAnyObjectByType<PlayerStats>();
    }

    void Update()
    {
        if (!grab.isSelected || Time.time < nextBite || stats == null || Camera.main == null)
            return;

        // The mouth is a little below and in front of the eyes.
        Transform head = Camera.main.transform;
        Vector3 mouth = head.position - head.up * 0.08f + head.forward * 0.06f;
        if (Vector3.Distance(Center(), mouth) > mouthDistance)
            return;

        nextBite = Time.time + biteInterval;
        bitesLeft--;
        stats.Eat(food / bites);
        stats.AddEnergy(energy / bites);
        string gained = energy > 0f ? "+" + Mathf.RoundToInt(energy / bites) + " energy" : "+" + Mathf.RoundToInt(food / bites) + " food";
        ShopPopup.Show(mouth + head.forward * 0.25f, gained, ShopPopup.Yellow);

        if (bitesLeft <= 0)
        {
            grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
            Destroy(gameObject);
            return;
        }
        // A bitten piece looks a bit smaller.
        transform.localScale = fullSize * Mathf.Lerp(0.55f, 1f, (float)bitesLeft / bites);
    }

    Vector3 Center()
    {
        Renderer r = GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : transform.position;
    }
}
