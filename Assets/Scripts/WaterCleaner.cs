using System.Collections.Generic;
using UnityEngine;

// Put this on the water spray particle system. When water drops hit something
// with a DirtySurface, the spot where they hit gets cleaned.
// The particle system's Collision module needs "Send Collision Messages" turned on.
[RequireComponent(typeof(ParticleSystem))]
public class WaterCleaner : MonoBehaviour
{
    // How this water cleans (Assets/Cleaning/Water.asset).
    public CleaningTool cleaningTool;

    ParticleSystem waterParticles;
    List<ParticleCollisionEvent> hits = new List<ParticleCollisionEvent>();

    void Awake()
    {
        waterParticles = GetComponent<ParticleSystem>();
    }

    // Unity calls this when drops from this particle system hit a collider.
    void OnParticleCollision(GameObject other)
    {
        DirtySurface surface = other.GetComponent<DirtySurface>();
        if (surface == null)
            return;

        int count = waterParticles.GetCollisionEvents(other, hits);
        for (int i = 0; i < count; i++)
        {
            // A particle hit gives the point and the surface direction, but not the
            // texture position. A tiny raycast at that point gives us the UV.
            Vector3 point = hits[i].intersection;
            Vector3 normal = hits[i].normal;
            if (Physics.Raycast(point + normal * 0.05f, -normal, out RaycastHit rayHit, 0.1f)
                && rayHit.collider.gameObject == other)
            {
                surface.Clean(rayHit.textureCoord, cleaningTool);
            }
        }
    }
}
