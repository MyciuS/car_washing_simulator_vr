using UnityEngine;

// A walking route for NPCs (see NpcWalker). The child objects, in order, are the points of
// the route: NPCs walk from point to point, and after the last one they go back to the first.
// To change the route, move the child objects; to add a point, duplicate one and put it in
// the right place in the list.
public class NpcRoute : MonoBehaviour
{
    public int Count => transform.childCount;

    public Vector3 GetPoint(int index)
    {
        return transform.GetChild(index).position;
    }

    // Where to walk next to join the route from a position: finds the stretch of the route
    // (from one point to the next) that is closest, and gives the point at the end of it.
    // So an NPC halfway along a street keeps going the same way instead of turning back.
    public int NextPointFrom(Vector3 position)
    {
        int next = 0;
        float closestDistance = float.MaxValue;
        Vector3 p = Flat(position);
        for (int i = 0; i < Count; i++)
        {
            Vector3 a = Flat(GetPoint(i));
            Vector3 b = Flat(GetPoint((i + 1) % Count));
            Vector3 ab = b - a;
            float along = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            float distance = Vector3.Distance(p, a + ab * along);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                next = (i + 1) % Count;
            }
        }
        return next;
    }

    static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    // Draws the route in the Scene view, so it is easy to see and edit.
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < Count; i++)
        {
            Vector3 point = GetPoint(i) + Vector3.up * 0.2f;
            Vector3 next = GetPoint((i + 1) % Count) + Vector3.up * 0.2f;
            Gizmos.DrawSphere(point, 0.2f);
            Gizmos.DrawLine(point, next);
        }
    }
}
