using UnityEngine;

// The time of day in the game. Game time runs faster than real time:
// by default 1 real second = 1 game minute, so a whole day lasts 24 real minutes.
// Other scripts read the time from here (the wrist watch shows it, washing jobs time themselves with it).
// Put one of these in the scene (on the Player Stats object).
public class GameClock : MonoBehaviour
{
    // How many game minutes pass in one real second.
    public float gameMinutesPerSecond = 1f;

    // The time of day when the game starts, in hours (8.5 = 08:30).
    public float startHour = 8f;

    // The clock in the scene, so any script can use GameClock.Instance.
    public static GameClock Instance { get; private set; }

    // Game minutes since the start of day 1 (keeps counting over midnight).
    public float TotalMinutes { get; private set; }

    // Which day it is (1 = the first day).
    public int Day => 1 + (int)(TotalMinutes / (24f * 60f));

    // The time of day as text, for example "14:05".
    public string TimeText
    {
        get
        {
            int minuteOfDay = (int)(TotalMinutes % (24f * 60f));
            return (minuteOfDay / 60).ToString("00") + ":" + (minuteOfDay % 60).ToString("00");
        }
    }

    void Awake()
    {
        Instance = this;
        TotalMinutes = startHour * 60f;
    }

    void Update()
    {
        TotalMinutes += Time.deltaTime * gameMinutesPerSecond;
    }

    // A number of game minutes as text, for example 150 -> "2:30".
    public static string Duration(float minutes)
    {
        int whole = Mathf.Max(0, Mathf.CeilToInt(minutes));
        return (whole / 60) + ":" + (whole % 60).ToString("00");
    }
}
