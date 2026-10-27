using UnityEngine;

// Bends the arm so it goes from the hand back to the player's shoulder,
// instead of sticking straight out of the controller.
// The shoulder is guessed from the head (headset) position, and the elbow is
// found with simple two-bone IK (like most VR games that show arms).
// Put this on the arm object (Left Controller > Left Arm), next to ControllerHand.
// The bones are found by name ("L_Forearm", "L_UpperArm"), so nothing has to be set up by hand.
public class ArmIK : MonoBehaviour
{
    // The player's head (Main Camera). Found automatically if left empty.
    public Transform head;

    [Header("Where the shoulder is, measured from the eyes (meters)")]
    public float shoulderWidth = 0.18f;
    public float shoulderDrop = 0.22f;
    public float shoulderBack = 0.06f;

    // Elbow to shoulder (meters). The forearm length is read from the model.
    public float upperArmLength = 0.30f;

    // How far the wrist can bend (degrees), so it can't look broken.
    // Real wrists bend a lot up and down (towards the palm or the back of the hand),
    // but only a little to the sides.
    public float maxWristBendUpDown = 25f;
    public float maxWristBendSideways = 12f;

    // How much the elbows point out to the sides (0 = straight down).
    public float elbowsOut = 0.25f;

    // How fast the body turns to follow the head.
    public float bodyTurnSpeed = 4f;

    Transform forearm;
    Transform upperArm;
    Transform wrist;
    bool isLeft;
    float forearmLength;

    // Rest pose: each bone's local rotation, and which local axis points along the arm.
    Quaternion forearmRest;
    Quaternion upperArmRest;
    Vector3 forearmAxis;
    Vector3 upperArmAxis;

    Vector3 bodyForward;

    void Awake()
    {
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
        {
            if (bone.name.EndsWith("_Forearm"))
                forearm = bone;
            else if (bone.name.EndsWith("_UpperArm"))
                upperArm = bone;
            else if (bone.name.EndsWith("_Wrist"))
                wrist = bone;
        }
        if (forearm == null || upperArm == null || wrist == null)
        {
            Debug.LogWarning("ArmIK: no *_Forearm / *_UpperArm / *_Wrist bones under " + name, this);
            enabled = false;
            return;
        }

        isLeft = forearm.name.StartsWith("L_");
        forearmLength = Vector3.Distance(forearm.position, upperArm.position);

        // In the model the arm is straight, so both bones point from the wrist to the elbow.
        Vector3 alongArm = (upperArm.position - forearm.position).normalized;
        forearmRest = forearm.localRotation;
        upperArmRest = upperArm.localRotation;
        forearmAxis = Quaternion.Inverse(forearm.rotation) * alongArm;
        upperArmAxis = Quaternion.Inverse(upperArm.rotation) * alongArm;
    }

    void Start()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
        if (head != null)
            bodyForward = HeadDirection();
    }

    // Runs after the controllers have moved this frame.
    void LateUpdate()
    {
        if (head == null)
            return;

        // The body slowly turns to where the head is looking.
        bodyForward = Vector3.Slerp(bodyForward, HeadDirection(), bodyTurnSpeed * Time.deltaTime).normalized;
        Vector3 bodyRight = Vector3.Cross(Vector3.up, bodyForward);
        Vector3 outward = isLeft ? -bodyRight : bodyRight;

        Vector3 shoulder = head.position + Vector3.down * shoulderDrop - bodyForward * shoulderBack + outward * shoulderWidth;

        // Two-bone IK: find the elbow so the forearm and upper arm keep their lengths.
        Vector3 toShoulder = shoulder - forearm.position;
        float a = forearmLength;
        float b = upperArmLength;
        float distance = Mathf.Clamp(toShoulder.magnitude, Mathf.Abs(a - b) + 0.01f, a + b - 0.001f);
        Vector3 direction = toShoulder.normalized;
        // How far along the wrist-shoulder line the elbow is, and how far it sticks out.
        float along = (a * a - b * b + distance * distance) / (2f * distance);
        float side = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));

        // Elbows point down, a bit out and a bit back.
        Vector3 hint = Vector3.down + outward * elbowsOut - bodyForward * 0.3f;
        Vector3 bendDirection = Vector3.ProjectOnPlane(hint, direction).normalized;
        Vector3 elbowDirection = (direction * along + bendDirection * side).normalized;

        // Point the forearm at the elbow, but don't bend the wrist too far.
        Quaternion forearmStart = forearm.parent.rotation * forearmRest;
        Vector3 straightAhead = forearmStart * forearmAxis;
        elbowDirection = LimitWristBend(straightAhead, elbowDirection);
        forearm.rotation = Quaternion.FromToRotation(straightAhead, elbowDirection) * forearmStart;

        // Point the upper arm at the shoulder.
        Quaternion upperArmStart = forearm.rotation * upperArmRest;
        Vector3 upperArmNow = upperArmStart * upperArmAxis;
        Vector3 toShoulderFromElbow = shoulder - upperArm.position;
        upperArm.rotation = Quaternion.FromToRotation(upperArmNow, toShoulderFromElbow) * upperArmStart;
    }

    // Turns "wanted" (direction from the wrist to the elbow) so the wrist stays within its limits.
    // "straight" is the direction of a forearm with an unbent wrist.
    Vector3 LimitWristBend(Vector3 straight, Vector3 wanted)
    {
        // The back of the hand faces the wrist bone's up axis; the thumb side is the third axis.
        Vector3 backOfHand = Vector3.ProjectOnPlane(wrist.up, straight).normalized;
        Vector3 thumbSide = Vector3.Cross(straight, backOfHand);

        float upDown = Mathf.Atan2(Vector3.Dot(wanted, backOfHand), Vector3.Dot(wanted, straight)) * Mathf.Rad2Deg;
        float sideways = Mathf.Asin(Mathf.Clamp(Vector3.Dot(wanted, thumbSide), -1f, 1f)) * Mathf.Rad2Deg;

        // Too far? Shrink both parts together, so the wrist still bends the same way, just less.
        float howFar = Mathf.Sqrt(Mathf.Pow(upDown / maxWristBendUpDown, 2f) + Mathf.Pow(sideways / maxWristBendSideways, 2f));
        if (howFar > 1f)
        {
            upDown /= howFar;
            sideways /= howFar;
        }
        upDown *= Mathf.Deg2Rad;
        sideways *= Mathf.Deg2Rad;

        Vector3 inBendPlane = straight * Mathf.Cos(upDown) + backOfHand * Mathf.Sin(upDown);
        return inBendPlane * Mathf.Cos(sideways) + thumbSide * Mathf.Sin(sideways);
    }

    // Where the head faces, flat on the ground. Still works when looking straight down or up.
    Vector3 HeadDirection()
    {
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        Vector3 up = Vector3.ProjectOnPlane(head.up, Vector3.up);
        Vector3 direction = forward + up * -head.forward.y;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }
}
