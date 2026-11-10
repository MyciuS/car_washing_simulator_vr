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
    // On a big surface (a whole car) the mounds are spread further apart by themselves,
    // so that maxMounds is enough to cover all of it.
    public float spacing = 0.1f;
    // Smallest and biggest mound width, in meters (wider than the spacing, so mounds overlap).
    public float minSize = 0.14f;
    public float maxSize = 0.23f;
    // How tall a mound is compared to its width.
    public float height = 0.25f;
    // Most mounds this surface can have (keeps VR fast on a big car).
    public int maxMounds = 3000;

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
        FitSpacingToSurface();
    }

    // A whole car has room for many more mounds than maxMounds at the normal spacing, and then
    // foam would stop appearing partway. So on a big surface, space the mounds further apart
    // (and make them wider by the same amount) until maxMounds covers the whole surface.
    void FitSpacingToSurface()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null || !meshFilter.sharedMesh.isReadable)
            return;
        Vector3[] vertices = meshFilter.sharedMesh.vertices;
        int[] triangles = meshFilter.sharedMesh.triangles;
        float area = 0f;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 b = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 c = transform.TransformPoint(vertices[triangles[i + 2]]);
            area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        }
        // About one little cube of space per (spacing x spacing) of surface; 10% extra to be safe.
        float neededSpacing = Mathf.Sqrt(area / maxMounds) * 1.1f;
        if (neededSpacing <= spacing)
            return;
        float grow = neededSpacing / spacing;
        spacing = neededSpacing;
        minSize *= grow;
        maxSize *= grow;
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
