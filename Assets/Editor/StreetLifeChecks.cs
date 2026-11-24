using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Run in Play mode with Tools > Car Wash > Check Street Life (Ctrl+Shift+F9).
static class StreetLifeChecks
{
    class Observation
    {
        public NpcStreetLife life;
        public bool resting, stopped, looked, resumed;
        public float restStarted;
        public Vector3 position;
        public Quaternion headRotation;
    }

    static List<Observation> observations;
    static float started;
    static bool heardBirds;
    static NpcSeatedIdle seated;
    static Vector3 seatedPosition;
    static int expectedWalkers;

    [MenuItem("Tools/Car Wash/Check Street Life %#F9")]
    static void Start()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Enter Play mode before checking street life.");
            return;
        }
        observations = Object.FindObjectsByType<NpcStreetLife>().Where(life => life.isActiveAndEnabled).Select(life => new Observation { life = life }).ToList();
        var population = Object.FindAnyObjectByType<NpcStreetPopulation>();
        expectedWalkers = population.count - (population.seatedSpot != null ? 1 : 0);
        seated = Object.FindAnyObjectByType<NpcSeatedIdle>();
        if (seated != null) seatedPosition = seated.transform.position;
        started = Time.time;
        heardBirds = false;
        EditorApplication.update -= Check;
        EditorApplication.update += Check;
        Debug.Log("Checking NPC rests, head movement, resumed walking, ambience and normal time for 40 seconds.");
    }

    static void Finish(string result)
    {
        EditorApplication.update -= Check;
        File.WriteAllText("Logs/StreetLifeChecks.txt", result);
        Debug.Log(result);
    }

    static void Check()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Check; return; }
        if (!Mathf.Approximately(Time.timeScale, 1f)) { Finish("FAIL: game time is not 1x"); return; }
        if (observations.Count != expectedWalkers) { Finish("FAIL: incorrect number of walking pedestrians"); return; }
        if (expectedWalkers == 2)
        {
            if (seated == null || seated.GetComponent<NpcWalker>().enabled || seated.GetComponent<NpcStreetLife>().enabled)
            { Finish("FAIL: seated neighbour is missing or still walking"); return; }
            if (Vector3.Distance(seated.transform.position, seatedPosition) > 0.01f)
            { Finish("FAIL: seated neighbour changed position"); return; }
            var a = seated.GetComponent<Animator>();
            Vector3 thigh = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position - a.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
            Vector3 shin = a.GetBoneTransform(HumanBodyBones.LeftFoot).position - a.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position;
            if (Vector3.Angle(thigh, seated.transform.forward) > 20f || Vector3.Angle(shin, Vector3.down) > 15f)
            { Finish("FAIL: legs are not in a seated pose"); return; }
        }
        foreach (var o in observations)
        {
            Transform head = o.life.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            if (o.life.IsResting && !o.resting)
            {
                o.restStarted = Time.time;
                o.position = o.life.transform.position;
                o.headRotation = head.localRotation;
            }
            if (o.life.IsResting && Time.time - o.restStarted > 0.8f)
            {
                if (Vector3.Distance(o.position, o.life.transform.position) > 0.05f) { Finish("FAIL: NPC moves while resting"); return; }
                o.stopped = true;
                o.looked |= Quaternion.Angle(o.headRotation, head.localRotation) > 5f;
            }
            if (!o.life.IsResting && o.stopped && Vector3.Distance(o.position, o.life.transform.position) > 0.5f) o.resumed = true;
            o.resting = o.life.IsResting;
        }
        var ambience = Object.FindAnyObjectByType<TownAmbience>();
        if (ambience == null || ambience.windClip == null || ambience.birdClip == null) { Finish("FAIL: missing ambience clips"); return; }
        var sources = ambience.GetComponentsInChildren<AudioSource>();
        if (!sources.Any(s => s.clip == ambience.windClip && s.isPlaying && s.loop && s.spatialBlend == 0f)) { Finish("FAIL: wind loop is not playing"); return; }
        heardBirds |= sources.Any(s => s.clip == ambience.birdClip && s.isPlaying && s.spatialBlend == 1f);
        if (Time.time - started < 40f) return;
        bool pass = heardBirds && observations.All(o => o.stopped && o.looked && o.resumed);
        Finish((pass ? "PASS" : "FAIL") + ": walkers=" + observations.Count + ", seated=" + (seated != null) + ", stopped=" + observations.Count(o => o.stopped) + ", looked=" + observations.Count(o => o.looked) + ", resumed=" + observations.Count(o => o.resumed) + ", birds=" + heardBirds + ", wind playing, timeScale=1");
    }
}
