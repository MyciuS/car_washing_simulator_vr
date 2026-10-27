using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A hand tool that cleans by rubbing it on the car: the sponge, the wheel brush and the towel.
// While the tool is held and touches a DirtySurface, its CleaningTool is applied where it touches.
// The more it is moved over the car, the more it cleans (holding it still does nothing).
// Tools that use soap get it by being dipped in the bucket (SoapBucket). Rubbing uses the soap up,
// and the tool slowly gets dirty, which makes it clean worse until it is rinsed in the bucket again.
// Put this on the tool, next to its XR Grab Interactable.
[RequireComponent(typeof(XRGrabInteractable))]
public class RubCleaner : MonoBehaviour
{
    // The part of the tool that touches the car (the sponge, the bristles, the towel). Not a trigger.
    public BoxCollider rubArea;

    // What rubbing does with soap in the tool, and without it.
    // Tools that never use soap (the towel) only need "Without Soap".
    public CleaningTool withSoap;
    public CleaningTool withoutSoap;

    // How many meters of rubbing count as one full use of the tool (smaller = cleans faster).
    public float metersPerUse = 0.05f;

    [Header("Soap (tools that use soap)")]
    // How much of a full soap load is used up per meter of rubbing.
    public float soapPerMeter = 0.15f;

    [Header("Getting dirty")]
    // How dirty the tool gets per meter of rubbing (1 = fully dirty).
    public float dirtPerMeter = 0.08f;
    // How much worse a fully dirty tool cleans (0.5 = half as well).
    public float dirtyPenalty = 0.5f;

    [Header("Looks")]
    // The tool's mesh, which turns from its own color to the dirty color.
    public Renderer toolRenderer;
    // Which of the mesh's materials gets dirty (for meshes with more than one, like the brush).
    public int dirtyMaterial = 0;
    public Color dirtyColor = new Color(0.45f, 0.36f, 0.25f);
    // Optional foam on the tool, shown while it holds soap.
    public Transform foam;

    // How much soap the tool holds (0 = none, 1 = just dipped in the bucket), and how dirty it is.
    float soapLoad;
    float dirtiness;

    XRGrabInteractable grab;
    Material toolMaterial;
    Color cleanColor;
    Vector3 foamSize;
    Vector3 lastPosition;
    Collider[] touching = new Collider[8];

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (toolRenderer != null)
        {
            toolMaterial = toolRenderer.materials[dirtyMaterial];
            cleanColor = toolMaterial.GetColor("_BaseColor");
        }
        if (foam != null)
            foamSize = foam.localScale;
        lastPosition = RubCenter();
        UpdateLooks();
    }

    // Runs after the hands have moved this frame.
    void LateUpdate()
    {
        Vector3 center = RubCenter();
        Vector3 movement = center - lastPosition;
        lastPosition = center;

        // Only while held. Big jumps (just picked up, teleported) are not rubbing.
        if (!grab.isSelected || movement.magnitude > 0.2f)
            return;

        Vector3 halfSize = Vector3.Scale(rubArea.size * 0.5f, rubArea.transform.lossyScale);
        int count = Physics.OverlapBoxNonAlloc(center, halfSize, touching, rubArea.transform.rotation,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            DirtySurface surface = touching[i].GetComponent<DirtySurface>();
            if (surface != null)
                Rub(surface, touching[i], movement);
        }
    }

    // Cleans the spot where the tool touches this part of the car.
    void Rub(DirtySurface surface, Collider carCollider, Vector3 movement)
    {
        // The direction that pushes the tool out of the car is the car's surface direction where it touches.
        if (!Physics.ComputePenetration(rubArea, rubArea.transform.position, rubArea.transform.rotation,
                carCollider, carCollider.transform.position, carCollider.transform.rotation,
                out Vector3 outOfCar, out float depth))
            return;

        // A short ray onto the car finds the touched spot and its place on the dirt texture.
        // (Thin parts like a flat side can push either way, so try both directions.)
        Vector3 center = RubCenter();
        if (!carCollider.Raycast(new Ray(center + outOfCar * 0.2f, -outOfCar), out RaycastHit hit, 0.4f)
            && !carCollider.Raycast(new Ray(center - outOfCar * 0.2f, outOfCar), out hit, 0.4f))
            return;

        // Only movement along the car counts as rubbing (pressing in and out does nothing).
        float rubbed = Vector3.ProjectOnPlane(movement, hit.normal).magnitude;
        if (rubbed <= 0f)
            return;

        bool soapy = withSoap != null && soapLoad > 0f;
        CleaningTool tool = soapy ? withSoap : withoutSoap;
        float strength = rubbed / metersPerUse * (1f - dirtiness * dirtyPenalty);
        surface.Spray(hit.textureCoord, hit.point, hit.normal, tool, strength);

        if (soapy)
            soapLoad = Mathf.Max(0f, soapLoad - rubbed * soapPerMeter);
        dirtiness = Mathf.Min(1f, dirtiness + rubbed * dirtPerMeter);
        UpdateLooks();
    }

    // Dipped in the bucket's water: full of soap again (if the tool uses soap) and the dirt rinsed out.
    void OnTriggerStay(Collider other)
    {
        SoapBucket bucket = other.GetComponentInParent<SoapBucket>();
        if (bucket == null || other != bucket.waterZone)
            return;
        if (withSoap != null)
            soapLoad = 1f;
        dirtiness = 0f;
        UpdateLooks();
    }

    // A splash when the tool goes into the water.
    void OnTriggerEnter(Collider other)
    {
        SoapBucket bucket = other.GetComponentInParent<SoapBucket>();
        if (bucket != null && other == bucket.waterZone)
            bucket.Splash(RubCenter());
    }

    // The tool darkens as it gets dirty, and the foam on it shrinks as the soap runs out.
    void UpdateLooks()
    {
        if (toolMaterial != null)
            toolMaterial.SetColor("_BaseColor", Color.Lerp(cleanColor, dirtyColor, dirtiness));
        if (foam != null)
        {
            foam.gameObject.SetActive(soapLoad > 0.02f);
            foam.localScale = foamSize * Mathf.Lerp(0.4f, 1f, soapLoad);
        }
    }

    Vector3 RubCenter()
    {
        return rubArea.transform.TransformPoint(rubArea.center);
    }
}
