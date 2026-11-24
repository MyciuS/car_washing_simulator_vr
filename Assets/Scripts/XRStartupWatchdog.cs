using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

// Sometimes the game never shows up in the headset: Unity connects to the headset (Meta Link says
// "ready"), but Unity's XR display never starts, so the headset stays in the Meta menu.
// Stopping XR and starting it again fixes that. This checks a few seconds after the game starts,
// and restarts XR if a headset is connected but nothing is shown in it (at most a few times).
// It starts by itself, it doesn't need to be put on an object.
public static class XRStartupWatchdog
{
    // How long to give XR to start (loading the scene can take a few seconds).
    const float WaitSeconds = 8f;
    const int MaxRestarts = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
        GameObject runner = new GameObject("XR Startup Watchdog");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        IEnumerator Start()
        {
            for (int restart = 1; restart <= MaxRestarts; restart++)
            {
                yield return new WaitForSecondsRealtime(WaitSeconds);

                XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
                // No headset (playing with mouse and keyboard), or the headset works: nothing to do.
                if (manager == null || manager.activeLoader == null || DisplayRunning())
                    break;

                Debug.LogWarning("XR Startup Watchdog: a headset is connected but the game isn't shown in it. " +
                                 "Restarting XR (" + restart + "/" + MaxRestarts + ").");
                manager.StopSubsystems();
                manager.DeinitializeLoader();
                yield return null;
                yield return manager.InitializeLoader();
                if (manager.activeLoader == null)
                    break;
                manager.StartSubsystems();
            }
            Destroy(gameObject);
        }

        static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        static bool DisplayRunning()
        {
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem display in displays)
            {
                if (display.running)
                    return true;
            }
            return false;
        }
    }
}
