using UnityEngine;
using UnityEngine.InputSystem;

// Moves the fingers of a hand model with the controller buttons, so the hand
// looks like it is really holding the controller.
// Grip closes the middle, ring and little fingers (and the thumb),
// trigger closes the index finger.
// Put this on the arm object (Left Controller > Left Arm). The finger bones are
// found by name (for example "L_IndexProximal"), so the hand model from the
// XR Hands sample works without setting anything up by hand.
public class ControllerHand : MonoBehaviour
{
    // Grip button value (0 = open, 1 = fully pressed). "XRI Left Interaction/Select Value".
    public InputActionProperty gripValue;

    // Trigger value (0 = open, 1 = fully pressed). "XRI Left Interaction/Activate Value".
    public InputActionProperty triggerValue;

    [Header("How far each joint bends when fully closed (degrees)")]
    public float proximalBend = 70f;
    public float intermediateBend = 85f;
    public float distalBend = 55f;
    public float thumbBend = 35f;

    // How much the fingers are bent when no button is pressed (0 = the model's own relaxed hand, 1 = fist).
    [Range(0f, 1f)]
    public float relaxed = 0.1f;

    // How fast the fingers follow the buttons.
    public float fingerSpeed = 15f;

    // One bending joint: the bone and its starting (open hand) rotation.
    struct Joint
    {
        public Transform bone;
        public Quaternion openRotation;
        public float bend;
    }

    Joint[] indexJoints;
    Joint[] gripJoints;
    Joint[] thumbJoints;

    float grip;
    float trigger;

    void Awake()
    {
        indexJoints = FindFinger("Index");
        gripJoints = Combine(FindFinger("Middle"), FindFinger("Ring"), FindFinger("Little"));
        thumbJoints = new[] { FindJoint("ThumbProximal", thumbBend), FindJoint("ThumbDistal", thumbBend) };
    }

    void Update()
    {
        // The XR rig's Input Action Manager already turns these actions on.
        float targetGrip = ReadValue(gripValue);
        float targetTrigger = ReadValue(triggerValue);

        // Move smoothly so the fingers don't snap.
        float step = fingerSpeed * Time.deltaTime;
        grip = Mathf.MoveTowards(grip, targetGrip, step);
        trigger = Mathf.MoveTowards(trigger, targetTrigger, step);

        Bend(indexJoints, trigger);
        Bend(gripJoints, grip);
        Bend(thumbJoints, grip);
    }

    // Bends all joints of a finger towards the palm by "pressed" (0 = relaxed, 1 = closed).
    void Bend(Joint[] joints, float pressed)
    {
        float amount = Mathf.Lerp(relaxed, 1f, pressed);
        foreach (Joint joint in joints)
        {
            if (joint.bone == null)
                continue;
            // The hand bones curl towards the palm around their own X axis.
            joint.bone.localRotation = joint.openRotation * Quaternion.Euler(joint.bend * amount, 0f, 0f);
        }
    }

    Joint[] FindFinger(string finger)
    {
        return new[]
        {
            FindJoint(finger + "Proximal", proximalBend),
            FindJoint(finger + "Intermediate", intermediateBend),
            FindJoint(finger + "Distal", distalBend),
        };
    }

    // Finds a bone whose name ends with the joint name (ignores the "L_" / "R_" prefix).
    Joint FindJoint(string jointName, float bend)
    {
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
        {
            if (bone.name.EndsWith("_" + jointName))
                return new Joint { bone = bone, openRotation = bone.localRotation, bend = bend };
        }
        Debug.LogWarning("ControllerHand: no bone called *_" + jointName + " under " + name, this);
        return new Joint();
    }

    static Joint[] Combine(params Joint[][] fingers)
    {
        var all = new System.Collections.Generic.List<Joint>();
        foreach (Joint[] finger in fingers)
            all.AddRange(finger);
        return all.ToArray();
    }

    static float ReadValue(InputActionProperty property)
    {
        return property.action != null ? property.action.ReadValue<float>() : 0f;
    }
}
