using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A wrist watch with pages (clock, stats, wallet).
// Press the button (X on the left controller) while looking at the watch
// to go to the next page.
// Put this on the watch Canvas (Left Controller > Wrist Watch > Watch Screen).
public class WristWatch : MonoBehaviour
{
    // Where the stats come from.
    public PlayerStats playerStats;

    // The player's head (Main Camera), used to check if the player looks at the watch.
    public Transform playerHead;

    // The button that switches pages.
    public InputActionProperty nextPageButton;

    [Header("Pages (only one is shown at a time)")]
    public GameObject[] pages;
    // Small dots at the bottom, one per page.
    public Image[] pageDots;

    [Header("Clock page")]
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI dateText;

    [Header("Stats page")]
    public Image energyBar;
    public TextMeshProUGUI energyText;
    public Image foodBar;
    public TextMeshProUGUI foodText;

    [Header("Wallet page")]
    public TextMeshProUGUI moneyText;

    int currentPage;

    void OnEnable()
    {
        nextPageButton.action.Enable();
    }

    void OnDisable()
    {
        nextPageButton.action.Disable();
    }

    void Start()
    {
        ShowPage(0);
    }

    void Update()
    {
        // Switch page only when the button is pressed while looking at the watch.
        if (nextPageButton.action.WasPressedThisFrame() && IsPlayerLookingAtWatch())
        {
            ShowPage((currentPage + 1) % pages.Length);
        }

        // Clock page
        timeText.text = DateTime.Now.ToString("HH:mm");
        dateText.text = DateTime.Now.ToString("ddd, d MMM");

        // Stats page (bars go from 0 to 1)
        energyBar.fillAmount = playerStats.energy / 100f;
        energyText.text = Mathf.RoundToInt(playerStats.energy) + "%";
        foodBar.fillAmount = playerStats.food / 100f;
        foodText.text = Mathf.RoundToInt(playerStats.food) + "%";

        // Wallet page
        moneyText.text = "$" + playerStats.money;
    }

    // Shows one page, hides the others, and highlights its dot.
    void ShowPage(int index)
    {
        currentPage = index;
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
            pageDots[i].color = i == index ? Color.white : new Color(1f, 1f, 1f, 0.3f);
        }
    }

    // True when the head points at the watch and the screen faces the head.
    bool IsPlayerLookingAtWatch()
    {
        Vector3 headToWatch = (transform.position - playerHead.position).normalized;
        bool headPointsAtWatch = Vector3.Dot(playerHead.forward, headToWatch) > 0.8f;
        // The readable side of a Canvas looks along -forward, so forward points away from the head.
        bool screenFacesHead = Vector3.Dot(transform.forward, headToWatch) > 0.5f;
        return headPointsAtWatch && screenFacesHead;
    }
}
