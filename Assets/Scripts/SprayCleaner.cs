using System.Collections.Generic;
using UnityEngine;

// Put this on a spray particle system (water or soap). When drops hit something
// with a DirtySurface, the tool is applied to the spot where they hit.
// The particle system's Collision module needs "Send Collision Messages" turned on.
[RequireComponent(typeof(ParticleSystem))]
public class SprayCleaner : MonoBehaviour
{
    // What this spray does to the car (Assets/Cleaning/Water.asset or Soap.asset).
    public CleaningTool cleaningTool;

    ParticleSystem sprayParticles;
    List<ParticleCollisionEvent> hits = new List<ParticleCollisionEvent>();

    void Awake()
    {
        sprayParticles = GetComponent<ParticleSystem>();
    }

    // Unity calls this when drops from this particle system hit a collider.
    void OnParticleCollision(GameObject other)
    {
        DirtySurface surface = other.GetComponent<DirtySurface>();
        if (surface == null)
            return;

        int count = sprayParticles.GetCollisionEvents(other, hits);
        for (int i = 0; i < count; i++)
        {
            // A particle hit gives the point and the surface direction, but not the
            // texture position. A tiny raycast at that point gives us the UV.
            Vector3 point = hits[i].intersection;
            Vector3 normal = hits[i].normal;
            if (Physics.Raycast(point + normal * 0.05f, -normal, out RaycastHit rayHit, 0.1f)
                && rayHit.collider.gameObject == other)
            {
                surface.Spray(rayHit.textureCoord, cleaningTool);
            }
        }
    }
}
