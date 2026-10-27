using System.Collections.Generic;
using UnityEngine;

// Put this on a spray particle system (water or soap). When drops hit something
// with a DirtySurface, the tool is applied to the spot where they hit, and some of the
// water runs off and drips onto the floor beside it. Drops that hit the floor (or anything
// else) wet the floor there (WetGround).
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
        int count = sprayParticles.GetCollisionEvents(other, hits);
        for (int i = 0; i < count; i++)
        {
            Vector3 point = hits[i].intersection;
            Vector3 normal = hits[i].normal;

            // Not the car: the drops land on the floor (or run down whatever they hit onto it).
            if (surface == null)
            {
                WetGround.Splash(point, cleaningTool.radius, cleaningTool.wetnessAdded, cleaningTool.soapAdded);
                continue;
            }

            // A particle hit gives the point and the surface direction, but not the
            // texture position. A tiny raycast at that point gives us the UV.
            if (Physics.Raycast(point + normal * 0.05f, -normal, out RaycastHit rayHit, 0.1f)
                && rayHit.collider.gameObject == other)
            {
                surface.Spray(rayHit.textureCoord, rayHit.point, rayHit.normal, cleaningTool);
            }
            // Some of the water runs off the car and drips onto the floor right next to it.
            WetGround.Splash(point + normal * 0.05f, cleaningTool.radius, cleaningTool.wetnessAdded * 0.3f, 0f);
        }
    }
}
