using System;
using TMPro;
using UnityEngine;

// Shows the player's stats and the current time on the watch text.
// Put this on the watch Canvas next to the right controller.
public class WristWatch : MonoBehaviour
{
    // Where the stats come from.
    public PlayerStats playerStats;

    // The text on the watch screen.
    public TextMeshProUGUI watchText;

    void Update()
    {
        watchText.text =
            DateTime.Now.ToString("HH:mm") + "\n" +
            "Health: " + Mathf.RoundToInt(playerStats.health) + "\n" +
            "Food: " + Mathf.RoundToInt(playerStats.food) + "\n" +
            "Money: $" + playerStats.money;
    }
}
