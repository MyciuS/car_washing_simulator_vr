using System.Collections.Generic;
using UnityEngine;

// Walks an NPC along a route (NpcRoute), around and around. It stays on the ground (sidewalks
// are a bit higher than the road) and waits when the player or another NPC is right in front of it.
// Other scripts (NpcCustomer) can make it stand still, face something, or walk somewhere else.
// Put this on the NPC. Put the NPC on the "Ignore Raycast" layer, so it doesn't find itself
// when it looks for the ground.
public class NpcWalker : MonoBehaviour
{
    public NpcRoute route;
    public float walkSpeed = 1.2f;
    // How fast the NPC turns, in degrees per second.
    public float turnSpeed = 300f;
    // The NPC waits if someone is closer than this in front of it (meters).
    public float personalSpace = 0.9f;

    // Every NPC, so they can keep their distance from each other.
    static readonly List<NpcWalker> allWalkers = new List<NpcWalker>();

    Transform playerHead;
    int nextPoint;
    bool standingStill;
    bool goingSomewhere;
    Vector3 destination;
    Vector3? faceTowards;

    // True once a GoTo walk has reached its destination.
    public bool HasArrived { get; private set; }

    void OnEnable()
    {
        allWalkers.Add(this);
    }

    void OnDisable()
    {
        allWalkers.Remove(this);
    }

    void Start()
    {
        if (Camera.main != null)
            playerHead = Camera.main.transform;
        if (route != null && route.Count > 0)
            nextPoint = route.NextPointFrom(transform.position);
    }

    // Walk along the route again, starting from the closest point of it.
    public void FollowRoute()
    {
        standingStill = false;
        goingSomewhere = false;
        faceTowards = null;
        if (route != null && route.Count > 0)
            nextPoint = route.NextPointFrom(transform.position);
    }

    // Walk straight to a place and stop there.
    public void GoTo(Vector3 place)
    {
        standingStill = false;
        goingSomewhere = true;
        HasArrived = false;
        destination = place;
        faceTowards = null;
    }

    // Stop walking and stand where it is.
    public void StandStill()
    {
        standingStill = true;
    }

    // While standing, turn to look at this point (null = don't turn).
    public void Face(Vector3? point)
    {
        faceTowards = point;
    }

    void Update()
    {
        if (standingStill || HasArrived && goingSomewhere)
        {
            if (faceTowards.HasValue)
                TurnTowards(faceTowards.Value - transform.position);
            return;
        }

        Vector3 target;
        if (goingSomewhere)
        {
            target = destination;
        }
        else
        {
            if (route == null || route.Count == 0)
                return;
            target = route.GetPoint(nextPoint);
        }

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude < 0.3f)
        {
            if (goingSomewhere)
                HasArrived = true;
            else
                nextPoint = (nextPoint + 1) % route.Count;
            return;
        }

        TurnTowards(toTarget);
        // Walk only roughly in the right direction (no walking sideways), and not into anyone.
        if (Vector3.Angle(transform.forward, toTarget) < 45f && !SomeoneInTheWay())
        {
            Vector3 step = transform.forward * Mathf.Min(walkSpeed * Time.deltaTime, toTarget.magnitude);
            transform.position = OnGround(transform.position + step);
        }
    }

    void TurnTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;
        Quaternion wanted = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, turnSpeed * Time.deltaTime);
    }

    // Is the player or another NPC right in front of us?
    bool SomeoneInTheWay()
    {
        if (playerHead != null && IsInFront(playerHead.position))
            return true;
        foreach (NpcWalker other in allWalkers)
        {
            if (other != this && IsInFront(other.transform.position))
                return true;
        }
        return false;
    }

    bool IsInFront(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude < personalSpace && Vector3.Dot(offset.normalized, transform.forward) > 0.5f;
    }

    // The same spot, moved up or down onto the ground (sidewalk, road, car wash floor...).
    Vector3 OnGround(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y;
        return position;
    }
}
