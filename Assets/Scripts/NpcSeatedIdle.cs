using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class NpcSeatedIdle : MonoBehaviour
{
    // The marker is the surface of the step, facing out from the house.
    public Transform seat;
    Animator animator;
    readonly List<Transform> bones = new List<Transform>();
    readonly List<Quaternion> rest = new List<Quaternion>();

    void Start()
    {
        animator = GetComponent<Animator>();
        if (seat == null || !animator.isHuman) { enabled = false; return; }
        var life = GetComponent<NpcStreetLife>();
        if (life != null) life.enabled = false;
        var walker = GetComponent<NpcWalker>();
        if (walker != null) walker.enabled = false;
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
            if (bone == null) continue;
            bones.Add(bone);
            rest.Add(bone.localRotation);
        }
        animator.enabled = false;
    }

    void LateUpdate()
    {
        if (seat == null) return;
        transform.SetPositionAndRotation(seat.position, seat.rotation);
        for (int i = 0; i < bones.Count; i++) bones[i].localRotation = rest[i];
        animator.GetBoneTransform(HumanBodyBones.Hips).position = seat.position + Vector3.up * 0.1f;
        PoseSide(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -1f);
        PoseSide(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 1f);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head != null) head.localRotation *= Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.45f) * 8f, 0f);
    }

    void PoseSide(HumanBodyBones thighId, HumanBodyBones shinId, HumanBodyBones footId, HumanBodyBones toesId,
        HumanBodyBones armId, HumanBodyBones forearmId, HumanBodyBones handId, float side)
    {
        Transform thigh = animator.GetBoneTransform(thighId), shin = animator.GetBoneTransform(shinId);
        Transform foot = animator.GetBoneTransform(footId), toes = animator.GetBoneTransform(toesId);
        Aim(thigh, shin, transform.forward + transform.right * side * 0.08f - Vector3.up * 0.08f);
        Aim(shin, foot, Vector3.down + transform.forward * 0.04f);
        if (toes != null) Aim(foot, toes, transform.forward);

        Transform arm = animator.GetBoneTransform(armId), forearm = animator.GetBoneTransform(forearmId);
        Transform hand = animator.GetBoneTransform(handId);
        Vector3 target = shin.position + Vector3.up * 0.09f - transform.forward * 0.06f;
        float upper = Vector3.Distance(arm.position, forearm.position), lower = Vector3.Distance(forearm.position, hand.position);
        Vector3 toTarget = target - arm.position;
        float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
        float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
        Vector3 bend = Vector3.ProjectOnPlane(Vector3.down + transform.right * side * 0.5f, toTarget.normalized).normalized;
        Vector3 elbow = arm.position + toTarget.normalized * along + bend * Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
        Aim(arm, forearm, elbow - arm.position);
        Aim(forearm, hand, target - forearm.position);
    }

    static void Aim(Transform bone, Transform child, Vector3 direction)
    {
        bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
    }
}
