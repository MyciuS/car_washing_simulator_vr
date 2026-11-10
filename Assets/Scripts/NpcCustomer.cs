using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// An NPC who walks around town and owns a car that is parked somewhere (CarToWash).
// Walk up to them and look at them: they offer you money to wash their car. Press the talk
// button (A on the right controller, E on the keyboard) to say yes. They then walk to their car
// and wait next to it. Once the car is clean enough (and rinsed), they pay you straight away
// (as long as you are still near the car or them), and go back to their walk. Wash it quickly (within bonusTimeMinutes of game
// time) and they pay a bonus on top.
// Put this on the NPC, next to NpcWalker.
[RequireComponent(typeof(NpcWalker))]
public class NpcCustomer : MonoBehaviour
{
    // The car this NPC owns.
    public CarToWash car;

    // How much the NPC pays for the wash.
    public int price = 40;

    // How clean the car has to be before the NPC pays (0.85 = 85%).
    [Range(0f, 1f)]
    public float cleanEnough = 0.85f;

    [Header("Speed bonus")]
    // Wash the car within this many game minutes after saying yes and the NPC pays extra
    // (300 game minutes = 5 real minutes, see GameClock).
    public float bonusTimeMinutes = 300f;
    // The extra money, as a share of the price (0.5 = half the price on top).
    [Range(0f, 1f)]
    public float bonusShare = 0.5f;

    // Where the money goes. Found automatically if left empty.
    public PlayerStats playerStats;

    // The button that says yes. If it has no bindings, A on the right controller and E on the keyboard are used.
    public InputActionProperty talkButton;

    // The text above the NPC's head.
    public TextMeshPro speechBubble;

    // How close the player has to be to talk (meters).
    public float talkDistance = 2.5f;

    enum State { Walking, Offering, GoingToCar, WaitingByCar, Paid }

    State state = State.Walking;
    NpcWalker walker;
    Transform playerHead;
    // After the player walks away from an offer, the NPC doesn't ask again until this time.
    float dontAskUntil;
    float paidTime;
    float nextCleanCheck;
    float cleanliness;
    bool carWashed;
    // When the player said yes, in game minutes.
    float jobStartMinutes;

    void Awake()
    {
        walker = GetComponent<NpcWalker>();
        if (talkButton.action != null && talkButton.action.bindings.Count == 0)
        {
            talkButton.action.AddBinding("<XRController>{RightHand}/primaryButton");
            talkButton.action.AddBinding("<Keyboard>/e");
        }
    }

    void OnEnable()
    {
        talkButton.action?.Enable();
    }

    void OnDisable()
    {
        talkButton.action?.Disable();
    }

    void Start()
    {
        if (Camera.main != null)
            playerHead = Camera.main.transform;
        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();
        Say(null);
    }

    void Update()
    {
        if (playerHead == null || car == null)
            return;
        bool talkPressed = talkButton.action != null && talkButton.action.WasPressedThisFrame();

        switch (state)
        {
            case State.Walking:
                // Ask the player, if they come close, the car still needs a wash and they didn't just say no.
                if (!carWashed && Time.time >= dontAskUntil && PlayerIsClose())
                {
                    state = State.Offering;
                    walker.StandStill();
                    Say("Hi! Could you wash my car?\nI'll pay $" + price + ".\n<size=70%>[A] Sure!</size>");
                }
                break;

            case State.Offering:
                walker.Face(playerHead.position);
                if (talkPressed)
                {
                    state = State.GoingToCar;
                    jobStartMinutes = GameMinutes();
                    nextCleanCheck = 0f;
                    walker.GoTo(car.ownerWaitSpot.position);
                    Say("Great! It's parked over there.\nI'll wait next to it.\nBe quick and I'll add $" + Bonus() + "!");
                }
                else if (PlayerDistance() > talkDistance + 1.5f)
                {
                    // The player walked away: carry on, and don't ask again straight away.
                    state = State.Walking;
                    dontAskUntil = Time.time + 20f;
                    walker.FollowRoute();
                    Say(null);
                }
                break;

            case State.GoingToCar:
                UpdateCarLabel();
                // Washed already, before the owner even got there.
                if (CarIsClean() && PlayerIsNearby())
                {
                    Pay();
                    break;
                }
                if (walker.HasArrived)
                {
                    state = State.WaitingByCar;
                    walker.Face(car.transform.position);
                    Say(null);
                }
                break;

            case State.WaitingByCar:
                UpdateCarLabel();
                if (CarIsClean() && PlayerIsNearby())
                {
                    walker.Face(playerHead.position);
                    Pay();
                }
                else if (PlayerDistance() <= talkDistance + 1f)
                {
                    walker.Face(playerHead.position);
                    Say(Mathf.RoundToInt(cleanliness * 100f) + "% clean.\nI'd like it at least "
                        + Mathf.RoundToInt(cleanEnough * 100f) + "% clean,\nand rinsed, please.");
                }
                else
                {
                    walker.Face(car.transform.position);
                    Say(CarIsClean() ? "Looks great!\nCome get your money!" : null);
                }
                break;

            case State.Paid:
                // Say thanks for a moment, then go back to walking around.
                if (Time.time > paidTime + 4f)
                {
                    state = State.Walking;
                    walker.FollowRoute();
                    Say(null);
                }
                break;
        }
    }

    void Pay()
    {
        bool quick = BonusMinutesLeft() > 0f;
        playerStats.AddMoney(quick ? price + Bonus() : price);
        carWashed = true;
        car.HideLabel();
        state = State.Paid;
        paidTime = Time.time;
        if (quick)
            Say("Wow, that was fast!\nHere's $" + price + " + $" + Bonus() + " bonus. Thanks!");
        else
            Say("Wow, it looks brand new!\nHere's your $" + price + ". Thanks!");
    }

    // The extra money for a quick wash.
    int Bonus()
    {
        return Mathf.RoundToInt(price * bonusShare);
    }

    // Game minutes left to earn the bonus (0 or less = too late).
    float BonusMinutesLeft()
    {
        return bonusTimeMinutes - (GameMinutes() - jobStartMinutes);
    }

    // The game time in minutes (see GameClock), or real seconds if there is no game clock.
    static float GameMinutes()
    {
        return GameClock.Instance != null ? GameClock.Instance.TotalMinutes : Time.time;
    }

    // Twice a second, check how clean the car is and show it above the car, with the time left for the bonus.
    void UpdateCarLabel()
    {
        if (Time.time < nextCleanCheck)
            return;
        nextCleanCheck = Time.time + 0.5f;
        cleanliness = car.GetCleanliness();
        float left = BonusMinutesLeft();
        string bonusLine = left > 0f
            ? "<color=#7CFC7C>Bonus +$" + Bonus() + ": " + GameClock.Duration(left) + " left</color>"
            : "<color=#BBBBBB>No bonus, too slow</color>";
        car.ShowLabel("Wash for $" + price + "\n" + bonusLine + "\n" + Mathf.RoundToInt(cleanliness * 100f) + "% clean");
    }

    // The player is near, and the NPC is roughly in front of them (they are looking this way).
    bool PlayerIsClose()
    {
        Vector3 toNpc = transform.position - playerHead.position;
        toNpc.y = 0f;
        Vector3 looking = playerHead.forward;
        looking.y = 0f;
        return toNpc.magnitude <= talkDistance && Vector3.Dot(toNpc.normalized, looking.normalized) > 0.5f;
    }

    // Clean enough to pay, judged the same way as the label shows it ("85% clean" counts as 85%).
    bool CarIsClean()
    {
        return Mathf.RoundToInt(cleanliness * 100f) >= Mathf.RoundToInt(cleanEnough * 100f);
    }

    // The player is close to the owner or to the car (still standing at it after the last wipe).
    bool PlayerIsNearby()
    {
        Vector3 toCar = car.transform.position - playerHead.position;
        toCar.y = 0f;
        return PlayerDistance() <= talkDistance + 1f || toCar.magnitude <= 5f;
    }

    float PlayerDistance()
    {
        Vector3 offset = transform.position - playerHead.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    // Shows text above the NPC's head (null hides it).
    void Say(string text)
    {
        if (speechBubble == null)
            return;
        speechBubble.gameObject.SetActive(text != null);
        if (text != null)
            speechBubble.text = text;
    }

    // The speech bubble always turns to face the player.
    void LateUpdate()
    {
        if (speechBubble != null && speechBubble.gameObject.activeSelf && playerHead != null)
            speechBubble.transform.rotation = Quaternion.LookRotation(speechBubble.transform.position - playerHead.position);
    }
}
