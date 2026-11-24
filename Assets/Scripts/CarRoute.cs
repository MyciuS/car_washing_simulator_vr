using System.Collections.Generic;
using UnityEngine;

public class CarRoute : MonoBehaviour
{
    [SerializeField] Transform[] waypoints;
    [SerializeField, Min(0f)] float speed = 3f;
    [SerializeField, Min(0f)] float turnSpeed = 90f;
    [SerializeField, Min(0.01f)] float brakingDistance = 3f;

    [SerializeField] Transform[] wheels;
    [SerializeField] Vector3 wheelSpinAxis = Vector3.right;
    [SerializeField, Min(0.01f)] float wheelRadius = 0.32f;

    [SerializeField] Transform frontLeftSteer;
    [SerializeField] Transform frontRightSteer;
    [SerializeField, Range(0f, 45f)] float maxSteerAngle = 30f;
    [SerializeField, Min(0.01f)] float steeringLookAhead = 3f;
    [SerializeField, Min(0.01f)] float cornerDistance = 3f;

    readonly List<Vector3> route = new List<Vector3>();
    readonly List<Vector3> routeTangents = new List<Vector3>();
    int nextPoint = 1;
    float steerAngle;

    void Start()
    {
        PrepareSteeringPivot(frontLeftSteer);
        PrepareSteeringPivot(frontRightSteer);

        if (waypoints.Length == 0)
            return;

        BuildRoute();
        transform.position = route[0];

        if (route.Count > 1)
        {
            Vector3 direction = routeTangents[0];
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    void BuildRoute()
    {
        route.Add(waypoints[0].position);
        routeTangents.Add(waypoints.Length > 1
            ? (waypoints[1].position - waypoints[0].position).normalized
            : Vector3.zero);
        for (int i = 1; i < waypoints.Length - 1; i++)
        {
            Vector3 corner = waypoints[i].position;
            Vector3 incoming = waypoints[i - 1].position - corner;
            Vector3 outgoing = waypoints[i + 1].position - corner;
            float trim = Mathf.Min(cornerDistance, Mathf.Min(incoming.magnitude, outgoing.magnitude) * 0.5f);
            Vector3 entry = corner + incoming.normalized * trim;
            Vector3 exit = corner + outgoing.normalized * trim;

            route.Add(entry);
            routeTangents.Add(-incoming.normalized);
            for (int step = 1; step <= 12; step++)
            {
                float t = step / 12f;
                route.Add(Vector3.Lerp(
                    Vector3.Lerp(entry, corner, t),
                    Vector3.Lerp(corner, exit, t), t));
                routeTangents.Add(Vector3.Lerp(corner - entry, exit - corner, t).normalized);
            }
        }

        if (waypoints.Length > 1)
        {
            route.Add(waypoints[waypoints.Length - 1].position);
            routeTangents.Add((route[route.Count - 1] - route[route.Count - 2]).normalized);
        }
    }

    void PrepareSteeringPivot(Transform pivot)
    {
        if (pivot == null || System.Array.IndexOf(wheels, pivot.parent) < 0)
            return;

        // Repair the saved scene hierarchy while keeping the wheel in place.
        Transform wheel = pivot.parent;
        pivot.SetParent(transform, false);
        pivot.SetPositionAndRotation(wheel.position, transform.rotation);
        wheel.SetParent(pivot, true);
    }

    void Update()
    {
        if (nextPoint >= route.Count)
        {
            SetSteering(0f);
            return;
        }

        Vector3 previousPosition = transform.position;
        Vector3 target = route[nextPoint];
        Vector3 direction = target - previousPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Vector3 ahead = target;
            float remaining = steeringLookAhead - Vector3.Distance(previousPosition, target);
            for (int i = nextPoint + 1; i < route.Count && remaining > 0f; i++)
            {
                Vector3 next = route[i];
                remaining -= Vector3.Distance(ahead, next);
                ahead = remaining < 0f ? Vector3.MoveTowards(next, ahead, -remaining) : next;
            }

            Vector3 steeringDirection = ahead - previousPosition;
            steeringDirection.y = 0f;
            float headingError = Vector3.SignedAngle(transform.forward, steeringDirection, Vector3.up);
            SetSteering(Mathf.Clamp(headingError, -maxSteerAngle, maxSteerAngle));

        }

        float currentSpeed = speed;
        if (nextPoint == route.Count - 1)
        {
            float remaining = Vector3.Distance(previousPosition, target);
            currentSpeed *= Mathf.Clamp(remaining / brakingDistance, 0.15f, 1f);
        }

        float budget = currentSpeed * Time.deltaTime;
        float distance = 0f;
        while (nextPoint < route.Count && budget > 0f)
        {
            Vector3 from = transform.position;
            Vector3 to = route[nextPoint];
            transform.position = Vector3.MoveTowards(from, to, budget);
            float moved = Vector3.Distance(from, transform.position);
            budget -= moved;
            distance += moved;

            if (transform.position != to)
                break;

            nextPoint++;
        }

        int segmentEnd = Mathf.Min(nextPoint, route.Count - 1);
        Vector3 segmentStart = route[segmentEnd - 1];
        float segmentLength = Vector3.Distance(segmentStart, route[segmentEnd]);
        float progress = segmentLength > 0f
            ? Vector3.Distance(segmentStart, transform.position) / segmentLength
            : 1f;
        Vector3 tangent = Vector3.Lerp(routeTangents[segmentEnd - 1], routeTangents[segmentEnd], progress);
        tangent.y = 0f;
        if (tangent.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(tangent),
                turnSpeed * Time.deltaTime);
        }

        float rollDegrees = distance / wheelRadius * Mathf.Rad2Deg;

        foreach (Transform wheel in wheels)
        {
            if (wheel != null)
                wheel.Rotate(wheelSpinAxis, rollDegrees, Space.Self);
        }

    }

    void SetSteering(float targetAngle)
    {
        steerAngle = Mathf.MoveTowards(
            steerAngle, targetAngle, 30f * Time.deltaTime);

        Quaternion rotation = Quaternion.Euler(0f, steerAngle, 0f);

        if (frontLeftSteer != null)
            frontLeftSteer.localRotation = rotation;

        if (frontRightSteer != null)
            frontRightSteer.localRotation = rotation;
    }
}
