using UnityEngine;

public class NpcStreetPopulation : MonoBehaviour
{
    public NpcWalker template;
    [Min(1)] public int count = 3;
    public Transform seatedSpot;

    void Start()
    {
        if (template == null || template.route == null || template.route.Count < 2)
        {
            Debug.LogWarning("Street population needs a walker with a route.", this);
            return;
        }

        NpcRoute route = template.route;
        float length = 0f;
        for (int i = 0; i < route.Count; i++)
            length += SegmentLength(route, i);
        if (length < 0.01f)
            return;

        int total = Mathf.Max(1, count);
        float offset = Random.Range(0f, length);
        for (int i = 0; i < total; i++)
        {
            if (seatedSpot != null && i == total - 1)
            {
                NpcWalker neighbour = i == 0 ? template : Instantiate(template, seatedSpot.position, seatedSpot.rotation, transform);
                neighbour.name = "Seated Neighbour";
                neighbour.enabled = false;
                var life = neighbour.GetComponent<NpcStreetLife>();
                if (life != null) life.enabled = false;
                neighbour.gameObject.AddComponent<NpcSeatedIdle>().seat = seatedSpot;
                continue;
            }
            // Randomize within separate stretches so pedestrians do not spawn together.
            float distance = Mathf.Repeat(offset + (i + Random.Range(0.2f, 0.8f)) * length / total, length);
            int segment = 0;
            while (segment < route.Count - 1 && distance >= SegmentLength(route, segment))
            {
                distance -= SegmentLength(route, segment);
                segment++;
            }

            Vector3 a = route.GetPoint(segment);
            Vector3 b = route.GetPoint((segment + 1) % route.Count);
            float segmentLength = SegmentLength(route, segment);
            Vector3 position = Vector3.Lerp(a, b, segmentLength > 0f ? distance / segmentLength : 0f);
            Vector3 forward = b - a;
            forward.y = 0f;
            Quaternion rotation = forward.sqrMagnitude > 0f ? Quaternion.LookRotation(forward) : Quaternion.identity;

            NpcWalker walker = i == 0 ? template : Instantiate(template, position, rotation, transform);
            walker.transform.SetPositionAndRotation(position, rotation);
            walker.FollowRoute();
            Animator animator = walker.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.Play(0, 0, Random.value);
        }
    }

    static float SegmentLength(NpcRoute route, int index)
    {
        Vector3 delta = route.GetPoint((index + 1) % route.Count) - route.GetPoint(index);
        delta.y = 0f;
        return delta.magnitude;
    }
}
