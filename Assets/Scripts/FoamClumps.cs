using UnityEngine;

// Puffy 3D foam mounds sitting on a DirtySurface (one side of the car).
// The side is split into a grid; where soap lands, each grid cell gets one mound.
// Each mound's size follows the soap amount under it, so mounds grow as more soap
// is sprayed and shrink away when water rinses the soap off. Neighbouring mounds
// overlap, so together they look like one lumpy foam blanket.
// Put this next to DirtySurface.
[RequireComponent(typeof(DirtySurface))]
public class FoamClumps : MonoBehaviour
{
    // A particle system set up to draw foam mounds (Assets/Prefabs/Foam Clumps.prefab).
    public ParticleSystem foamPrefab;

    // Distance between mounds, in meters (bigger = fewer mounds, faster in VR).
    public float spacing = 0.08f;
    // Smallest and biggest mound width, in meters (wider than the spacing, so mounds overlap).
    public float minSize = 0.11f;
    public float maxSize = 0.18f;
    // How tall a mound is compared to its width.
    public float height = 0.25f;

    DirtySurface surface;
    ParticleSystem foam;
    ParticleSystem.Particle[] clumps;
    int cellsX;
    int cellsY;
    bool[] cellHasMound;
    bool soapChanged;

    void Start()
    {
        surface = GetComponent<DirtySurface>();
        foam = Instantiate(foamPrefab, transform);
        foam.transform.localPosition = Vector3.zero;
        foam.transform.localRotation = Quaternion.identity;

        // One grid cell per mound, sized from the real size of this side.
        cellsX = Mathf.Max(1, Mathf.CeilToInt(transform.lossyScale.x / spacing));
        cellsY = Mathf.Max(1, Mathf.CeilToInt(transform.lossyScale.y / spacing));
        cellHasMound = new bool[cellsX * cellsY];
        var main = foam.main;
        main.maxParticles = cellsX * cellsY;
        clumps = new ParticleSystem.Particle[main.maxParticles];
    }

    // Called by DirtySurface when soap lands at a point (uv = 0..1).
    public void SoapHit(Vector2 uv, float radius)
    {
        soapChanged = true;

        // Pick a random point inside the sprayed spot and find its grid cell.
        Vector2 offset = Random.insideUnitCircle * radius;
        float u = uv.x + offset.x / transform.lossyScale.x;
        float v = uv.y + offset.y / transform.lossyScale.y;
        if (u < 0f || v < 0f || u >= 1f || v >= 1f)
            return;
        int cx = (int)(u * cellsX);
        int cy = (int)(v * cellsY);
        if (cellHasMound[cy * cellsX + cx])
            return;
        cellHasMound[cy * cellsX + cx] = true;

        // New mound near the middle of the cell (a little random, so it doesn't look like a grid).
        Vector3 local = new Vector3((cx + Random.Range(0.2f, 0.8f)) / cellsX - 0.5f,
                                    (cy + Random.Range(0.2f, 0.8f)) / cellsY - 0.5f, 0f);
        var emit = new ParticleSystem.EmitParams();
        emit.position = transform.TransformPoint(local);
        // The mound mesh points along +Z; turn it so it sticks out of the car, with a random spin.
        emit.rotation3D = new Vector3(0f, 180f, Random.Range(0f, 360f));
        emit.startSize3D = Vector3.zero; // starts invisible, grows with the soap (see LateUpdate)
        emit.startLifetime = 100000f;
        foam.Emit(emit, 1);
    }

    // Called by DirtySurface when soap was rinsed.
    public void SoapChanged()
    {
        soapChanged = true;
    }

    // Resize every mound to match the soap under it. Only runs when the soap changed.
    void LateUpdate()
    {
        if (!soapChanged)
            return;
        soapChanged = false;

        int count = foam.GetParticles(clumps);
        for (int i = 0; i < count; i++)
        {
            Vector3 local = transform.InverseTransformPoint(clumps[i].position);
            Vector2 uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
            float soap = surface.GetSoap(uv);

            if (soap <= 0f)
            {
                // Soap gone: remove the mound and free its grid cell.
                clumps[i].remainingLifetime = 0f;
                int cx = Mathf.Clamp((int)(uv.x * cellsX), 0, cellsX - 1);
                int cy = Mathf.Clamp((int)(uv.y * cellsY), 0, cellsY - 1);
                cellHasMound[cy * cellsX + cx] = false;
                continue;
            }

            // Every mound gets its own size and its own soap level where it appears,
            // taken from its random seed, so foam edges look uneven.
            float random1 = (clumps[i].randomSeed % 1000) / 1000f;
            float random2 = (clumps[i].randomSeed / 1000 % 1000) / 1000f;
            float width = Mathf.Lerp(minSize, maxSize, random1);
            float grow = Mathf.Clamp01((soap - random2 * 0.25f) / 0.3f);
            clumps[i].startSize3D = new Vector3(width, width, width * height) * grow;

            // Turn light brown while the soap soaks, same as the foam on the paint.
            clumps[i].startColor = Color.Lerp(Color.white, surface.soakedFoamColor, surface.GetSoakProgress(uv));
        }
        foam.SetParticles(clumps, count);
    }
}
