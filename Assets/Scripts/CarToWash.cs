using TMPro;
using UnityEngine;

// A car that belongs to an NPC and that the player can wash for money (see NpcCustomer).
// Works out how clean the whole car is from all the DirtySurfaces on it, and shows a
// floating label above it while the player has a washing job for it.
// Put this on the car's root object.
public class CarToWash : MonoBehaviour
{
    // Where the owner stands and waits while the player washes the car (on the sidewalk next to it).
    public Transform ownerWaitSpot;

    // Text above the car, shown while the player has a job to wash it.
    public TextMeshPro label;

    DirtySurface[] surfaces;
    Transform playerHead;

    void Awake()
    {
        surfaces = GetComponentsInChildren<DirtySurface>();
        if (label != null)
            label.gameObject.SetActive(false);
    }

    void Start()
    {
        if (Camera.main != null)
            playerHead = Camera.main.transform;
    }

    // How clean the whole car is: 0 = as dirty as it started, 1 = spotless (and rinsed).
    // Bigger parts of the car count more than small ones.
    public float GetCleanliness()
    {
        float total = 0f;
        int pixels = 0;
        foreach (DirtySurface surface in surfaces)
        {
            total += surface.GetCleanliness() * surface.PixelCount;
            pixels += surface.PixelCount;
        }
        return pixels > 0 ? total / pixels : 0f;
    }

    // The numbers from all DirtySurfaces on the car together (see DirtySurface.Stats).
    public DirtySurface.Stats GetStats()
    {
        DirtySurface.Stats total = new DirtySurface.Stats();
        int pixels = 0;
        foreach (DirtySurface surface in surfaces)
        {
            DirtySurface.Stats s = surface.GetStats();
            int weight = surface.PixelCount;
            total.dirtRemoved += s.dirtRemoved * weight;
            total.soapy += s.soapy * weight;
            total.soaked += s.soaked * weight;
            total.wet += s.wet * weight;
            total.shiny += s.shiny * weight;
            pixels += weight;
        }
        if (pixels > 0)
        {
            total.dirtRemoved /= pixels;
            total.soapy /= pixels;
            total.soaked /= pixels;
            total.wet /= pixels;
            total.shiny /= pixels;
        }
        return total;
    }

    public void ShowLabel(string text)
    {
        if (label == null)
            return;
        label.gameObject.SetActive(true);
        label.text = text;
    }

    public void HideLabel()
    {
        if (label != null)
            label.gameObject.SetActive(false);
    }

    // The label always turns to face the player, so it can be read from anywhere.
    void LateUpdate()
    {
        if (label != null && label.gameObject.activeSelf && playerHead != null)
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - playerHead.position);
    }
}
