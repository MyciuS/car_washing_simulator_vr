using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NpcWalker), typeof(Animator))]
public class NpcStreetLife : MonoBehaviour
{
    public Vector2 walkDuration = new Vector2(10f, 22f);
    public Vector2 restDuration = new Vector2(2f, 4f);
    public bool IsResting { get; private set; }

    NpcWalker walker;
    readonly List<Transform> bones = new List<Transform>();
    readonly List<Quaternion> restRotations = new List<Quaternion>();
    Transform hips;
    Transform head;
    Vector3 restHipsPosition;
    float nextChange;
    float restStarted;
    float blend;

    void Awake()
    {
        walker = GetComponent<NpcWalker>();
        Animator animator = GetComponent<Animator>();
        if (!animator.isHuman)
        {
            enabled = false;
            return;
        }
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
            if (bone == null) continue;
            bones.Add(bone);
            restRotations.Add(bone.localRotation);
        }
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (hips != null) restHipsPosition = hips.localPosition;
    }

    void Start() { nextChange = Time.time + Random.Range(walkDuration.x, walkDuration.y); }

    void Update()
    {
        if (Time.time < nextChange) return;
        IsResting = !IsResting;
        if (IsResting)
        {
            walker.StandStill();
            restStarted = Time.time;
            nextChange = Time.time + Random.Range(restDuration.x, restDuration.y);
        }
        else
        {
            walker.FollowRoute();
            nextChange = Time.time + Random.Range(walkDuration.x, walkDuration.y);
        }
    }

    void LateUpdate()
    {
        blend = Mathf.MoveTowards(blend, IsResting ? 1f : 0f, Time.deltaTime * 4f);
        if (blend <= 0f) return;
        for (int i = 0; i < bones.Count; i++)
            bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, restRotations[i], blend);
        if (hips != null)
            hips.localPosition = Vector3.Lerp(hips.localPosition, restHipsPosition, blend);
        if (head != null)
        {
            float glance = Mathf.Sin((Time.time - restStarted) * 2f) * 30f;
            head.localRotation *= Quaternion.Euler(0f, glance * blend, 0f);
        }
    }

    void OnDisable()
    {
        if (IsResting && walker != null) walker.FollowRoute();
        IsResting = false;
    }
}
