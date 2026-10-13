using System.Collections.Generic;
using UnityEngine;

// Puffy 3D foam mounds sitting on a DirtySurface (a side of the test car or a whole car).
// Where soap lands, a mound is added right there, pointing out of the car. Space is split
// into small cubes with at most one mound each, so mounds spread out evenly.
// Each mound's size follows the soap amount under it, so mounds grow as more soap is
// sprayed and shrink away when water rinses the soap off. Neighbouring mounds overlap,
// so together they look like one lumpy foam blanket.
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
    // Most mounds this surface can have (keeps VR fast on a big car).
    public int maxMounds = 2000;

    // One foam mound: where it sits, which way it points, and its own random look.
    struct Mound
    {
        public Vector3 position;
        public Vector3 rotation;   // points out of the car (euler angles)
        public Vector2 uv;         // where its soap amount is read from
        public Vector3Int cell;    // the little cube of space it belongs to
        public float width;
        public float appearAt;     // soap amount where it starts to show (uneven edges)
    }

    DirtySurface surface;
    ParticleSystem foam;
    List<Mound> mounds = new List<Mound>();
    HashSet<Vector3Int> usedCells = new HashSet<Vector3Int>();
    ParticleSystem.Particle[] particles;
    bool soapChanged;

    void Start()
    {
        surface = GetComponent<DirtySurface>();
        foam = Instantiate(foamPrefab, transform);
        var main = foam.main;
        main.maxParticles = maxMounds;
        particles = new ParticleSystem.Particle[maxMounds];
    }

    // Called by DirtySurface when soap lands. uv = place on the texture, point/normal = place on the car.
    public void SoapHit(Vector2 uv, Vector3 point, Vector3 normal)
    {
        soapChanged = true;
        if (mounds.Count >= maxMounds)
            return;
        Vector3Int cell = Vector3Int.FloorToInt(point / spacing);
        if (!usedCells.Add(cell))
            return; // this little cube already has a mound

        Mound mound = new Mound();
        mound.position = point;
        // The mound mesh points along +Z: turn it to point out of the car, with a random spin.
        mound.rotation = (Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f))).eulerAngles;
        mound.uv = uv;
        mound.cell = cell;
        mound.width = Random.Range(minSize, maxSize);
        mound.appearAt = Random.Range(0f, 0.25f);
        mounds.Add(mound);
    }

    // Called by DirtySurface when soap was rinsed or changed color.
    public void SoapChanged()
    {
        soapChanged = true;
    }

    // Rebuild the drawn mounds from the soap under each one. Only runs when the soap changed.
    void LateUpdate()
    {
        if (!soapChanged)
            return;
        soapChanged = false;

        int count = 0;
        for (int i = mounds.Count - 1; i >= 0; i--)
        {
            Mound mound = mounds[i];
            float soap = surface.GetSoap(mound.uv);
            if (soap <= 0f)
            {
                // Soap gone: remove the mound and free its cube.
                usedCells.Remove(mound.cell);
                mounds.RemoveAt(i);
                continue;
            }

            float grow = Mathf.Clamp01((soap - mound.appearAt) / 0.3f);
            ParticleSystem.Particle particle = new ParticleSystem.Particle();
            particle.position = mound.position;
            particle.rotation3D = mound.rotation;
            particle.startSize3D = new Vector3(mound.width, mound.width, mound.width * height) * grow;
            // Turn light brown while the soap soaks, same as the foam on the paint.
            particle.startColor = Color.Lerp(Color.white, surface.soakedFoamColor, surface.GetSoakProgress(mound.uv));
            particle.startLifetime = 100000f;
            particle.remainingLifetime = 100000f;
            particles[count++] = particle;
        }
        foam.SetParticles(particles, count);
    }
}
