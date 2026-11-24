using System.IO;
using UnityEditor;
using UnityEngine;

static class CarEngineAudioChecks
{
    static GameObject test;
    static CarRoute car;
    static Vector3 previous;
    static float started, lastMoved, highestPitch;
    static bool heardMoving, heardBraking;

    [MenuItem("Tools/Car Wash/Check Car Engine Audio %#F10")]
    static void Start()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play mode first."); return; }
        var realCar = Object.FindAnyObjectByType<CarRoute>();
        if (realCar == null || new SerializedObject(realCar).FindProperty("engineClip").objectReferenceValue == null)
        { Debug.LogError("Moving scene car has no engine clip."); return; }
        if (test != null) Object.Destroy(test);
        test = new GameObject("Car Engine Audio Check");
        test.transform.position = new Vector3(1000f, 0f, 1000f);
        Transform a = new GameObject("Start").transform, b = new GameObject("Finish").transform;
        a.SetParent(test.transform); b.SetParent(test.transform);
        a.position = test.transform.position; b.position = a.position + Vector3.forward * 8f;
        // Waypoints stay outside the moving car's hierarchy.
        car = new GameObject("Test car").AddComponent<CarRoute>();
        car.transform.SetParent(test.transform);
        var settings = new SerializedObject(car);
        var points = settings.FindProperty("waypoints");
        points.arraySize = 2;
        points.GetArrayElementAtIndex(0).objectReferenceValue = a;
        points.GetArrayElementAtIndex(1).objectReferenceValue = b;
        settings.FindProperty("wheels").arraySize = 0;
        settings.FindProperty("speed").floatValue = 5f;
        settings.FindProperty("engineClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarEngine.wav");
        settings.ApplyModifiedPropertiesWithoutUndo();
        started = lastMoved = Time.time;
        previous = a.position;
        highestPitch = 0f;
        heardMoving = heardBraking = false;
        EditorApplication.update -= Check;
        EditorApplication.update += Check;
    }

    static void Finish(string result)
    {
        EditorApplication.update -= Check;
        if (test != null) Object.Destroy(test);
        File.WriteAllText("Logs/CarEngineAudioChecks.txt", result);
        Debug.Log(result);
    }

    static void Check()
    {
        if (!EditorApplication.isPlaying || car == null) { EditorApplication.update -= Check; return; }
        if (Time.time - started < 0.15f) return;
        var audio = car.GetComponent<AudioSource>();
        if (audio == null || audio.clip == null || audio.spatialBlend != 1f || !audio.loop)
        { Finish("FAIL: missing spatial looping engine source"); return; }
        bool moved = Vector3.Distance(car.transform.position, previous) > 0.0001f;
        if (moved)
        {
            lastMoved = Time.time;
            heardMoving |= audio.isPlaying && audio.volume > 0f;
            highestPitch = Mathf.Max(highestPitch, audio.pitch);
            heardBraking |= highestPitch - audio.pitch > 0.1f;
        }
        previous = car.transform.position;
        if (!moved && Time.time - lastMoved > 0.6f)
        {
            bool pass = heardMoving && heardBraking && !audio.isPlaying && audio.volume == 0f && Mathf.Approximately(Time.timeScale, 1f);
            Finish((pass ? "PASS" : "FAIL") + ": engine while moving=" + heardMoving + ", pitch follows braking=" + heardBraking + ", stopped=" + !audio.isPlaying + ", volume=" + audio.volume + ", timeScale=" + Time.timeScale);
        }
        else if (Time.time - started > 12f) Finish("FAIL: engine check timed out");
    }
}
