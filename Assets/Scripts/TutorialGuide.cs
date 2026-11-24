using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The tutorial for a new player: a board that shows one step at a time (grab the gun, rinse,
// foam the car, let the soap soak, scrub with the sponge, dry with the towel...).
// Each step checks by itself whether the player has done it, then the board moves on.
// Info steps wait for the continue button (A on the right controller, E on the keyboard).
// The skip button (B, or Q on the keyboard) jumps over a step if something doesn't work.
// A yellow arrow floats above the thing the player should use next.
// Put this on the board in the Tutorial scene and fill in the fields.
public class TutorialGuide : MonoBehaviour
{
    [Header("Things the player uses")]
    public CarToWash car;
    public WaterGun waterGun;
    public SprayHead soapNozzle;
    public RubCleaner sponge;
    public RubCleaner towel;
    public Transform bucket;

    [Header("Board")]
    public TextMeshPro titleText;
    public TextMeshPro bodyText;
    // Small line at the bottom: step number, live progress ("Car: 42% clean") and the buttons.
    public TextMeshPro footerText;
    // A pointer that floats above the thing to use next (hidden when there is nothing to point at).
    public Transform arrow;
    // Turn the board to face the player (leave off for a board hanging on a wall).
    public bool faceThePlayer = false;

    [Header("Buttons (if empty: A / E to continue, B / Q to skip)")]
    public InputActionProperty continueButton;
    public InputActionProperty skipButton;

    // The scene that starts after the last step.
    public string gameScene = "SampleScene";

    // One step of the tutorial.
    class Step
    {
        public string title;
        public string text;
        // Has the player done it? null = an info step: press continue.
        public Func<bool> isDone;
        // Live progress shown at the bottom, for example "Car: 42% clean" (optional).
        public Func<string> progress;
        // What the arrow points at (optional).
        public Func<Transform> pointAt;
        // Runs when the step starts (optional).
        public Action onStart;
    }

    List<Step> steps;
    int current;
    // When the current step was finished (the board shows "Done!" for a moment), or -1.
    float doneTime = -1f;
    float nextCheck;
    DirtySurface.Stats stats;
    float cleanliness;
    float rubbedAtStart;
    Transform playerHead;

    void Awake()
    {
        AddDefaultBinding(continueButton, "<XRController>{RightHand}/primaryButton", "<Keyboard>/e");
        AddDefaultBinding(skipButton, "<XRController>{RightHand}/secondaryButton", "<Keyboard>/q");
    }

    static void AddDefaultBinding(InputActionProperty button, string controller, string key)
    {
        if (button.action != null && button.action.bindings.Count == 0)
        {
            button.action.AddBinding(controller);
            button.action.AddBinding(key);
        }
    }

    void OnEnable()
    {
        continueButton.action?.Enable();
        skipButton.action?.Enable();
    }

    void OnDisable()
    {
        continueButton.action?.Disable();
        skipButton.action?.Disable();
    }

    void Start()
    {
        if (Camera.main != null)
            playerHead = Camera.main.transform;
        steps = MakeSteps();
        ShowStep(0);
    }

    // All the steps, in order. Change the texts here.
    List<Step> MakeSteps()
    {
        return new List<Step>
        {
            new Step
            {
                title = "Welcome to the car wash!",
                text = "This short lesson shows you how washing a car works.\n\n" +
                       "Grab things with the <b>grip</b> button (on the side of the controller).\n" +
                       "Spray with the <b>trigger</b>.\n\n" +
                       "Press <b>A</b> to start.",
            },
            new Step
            {
                title = "How dirt works",
                text = "This car has two layers of dirt:\n\n" +
                       "<b>Loose mud</b> (brown) - water and the sponge take it off.\n" +
                       "<b>Grime</b> (grey film) - stubborn. Soap loosens it.\n\n" +
                       "A car is done when it is clean <b>and rinsed</b> (no soap left on it).\n\n" +
                       "Press <b>A</b> to continue.",
            },
            new Step
            {
                title = "Grab the water gun",
                text = "Turn around: the tools are on the table <b>behind you</b>.\n\n" +
                       "Reach for the water gun and hold the <b>grip</b> button.",
                isDone = () => waterGun.IsHeld,
                pointAt = () => waterGun.transform,
            },
            new Step
            {
                title = "Rinse the car",
                text = "Aim at the car and pull the <b>trigger</b>.\n\n" +
                       "Water washes loose mud off and makes the paint wet.\n" +
                       "Wet the car all over.",
                isDone = () => stats.wet >= 0.2f,
                progress = () => "Wet: " + Percent(stats.wet / 0.2f),
            },
            new Step
            {
                title = "Click on the soap nozzle",
                text = "Grab the <b>red soap nozzle</b> with your other hand and push it onto the tip " +
                       "of the gun until it clicks on.",
                isDone = () => waterGun.AttachedHead == soapNozzle,
                pointAt = () => soapNozzle.transform,
            },
            new Step
            {
                title = "Cover the car in foam",
                text = "Pull the <b>trigger</b> to spray soap all over the car.\n\n" +
                       "Thick foam slowly runs down the car and drips off.",
                isDone = () => stats.soapy >= 0.5f,
                progress = () => "Foam: " + Percent(stats.soapy / 0.5f),
            },
            new Step
            {
                title = "Let the soap soak",
                text = "Wait a little! Soap needs about <b>15 seconds</b> to soak in.\n\n" +
                       "The foam turns <b>light brown</b> when it is ready.\n" +
                       "Soaked dirt comes off much faster.",
                isDone = () => stats.soaked >= 0.35f,
                progress = () => "Soaked: " + Percent(stats.soaked / 0.35f),
            },
            new Step
            {
                title = "Rinse the soaked dirt off",
                text = "Pull the soap nozzle off the tip, so the gun sprays water again.\n\n" +
                       "Rinse the foam and the dirt off.\nGet the car to <b>70% clean</b>.",
                isDone = () => cleanliness >= 0.7f,
                progress = () => "Car: " + Percent(cleanliness) + " clean",
            },
            new Step
            {
                title = "Grab the sponge",
                text = "Water doesn't get everything off. Put the gun down and grab the <b>sponge</b>.",
                isDone = () => sponge.IsHeld,
                pointAt = () => sponge.transform,
            },
            new Step
            {
                title = "Dip the sponge in the bucket",
                text = "Dip the sponge into the soapy water in the <b>bucket</b>.\n\n" +
                       "It fills up with soap, and any dirt in it is rinsed out.",
                isDone = () => sponge.HasSoap,
                pointAt = () => bucket,
            },
            new Step
            {
                title = "Scrub the car",
                text = "Rub the sponge over the dirty spots. <b>Moving</b> it is what cleans - " +
                       "holding it still does nothing.\n\n" +
                       "The sponge gets darker as it gets dirty: dip it in the bucket again to clean it.",
                onStart = () => rubbedAtStart = sponge.MetersRubbed,
                isDone = () => sponge.MetersRubbed - rubbedAtStart >= 8f,
                progress = () => "Scrubbed: " + Percent((sponge.MetersRubbed - rubbedAtStart) / 8f),
            },
            new Step
            {
                title = "Rinse again",
                text = "The sponge leaves soap behind. Rinse it off with the water gun.\n\n" +
                       "Get the car to <b>85% clean</b> - that is what customers want.",
                isDone = () => cleanliness >= 0.85f,
                progress = () => "Car: " + Percent(cleanliness) + " clean",
                pointAt = () => waterGun.IsHeld ? null : waterGun.transform,
            },
            new Step
            {
                title = "Dry and polish",
                text = "Grab the <b>microfiber towel</b> and wipe the car.\n\n" +
                       "It dries the paint, and clean, dry paint starts to shine.",
                isDone = () => stats.shiny >= 0.1f,
                progress = () => "Shine: " + Percent(stats.shiny / 0.1f),
                pointAt = () => towel.IsHeld ? null : towel.transform,
            },
            new Step
            {
                title = "You're ready!",
                text = "People in town will pay you to wash their cars. Walk up to someone and look " +
                       "at them, then press <b>A</b> to take the job.\n\n" +
                       "Wash the car within <b>5 minutes</b> and they add a <b>50% bonus</b>.\n" +
                       "Look at your <b>watch</b> and press <b>X</b> to see the time and your money.\n" +
                       "You start with only a sponge: buy the water gun, nozzles, soaps and snacks " +
                       "in the <b>shop</b> by the car wash (grab something to buy it).\n\n" +
                       "Press <b>A</b> to start the game.",
            },
        };
    }

    void Update()
    {
        if (steps == null)
            return;
        Step step = steps[current];

        // Read the car twice a second (it goes over every pixel of the car).
        if (Time.time >= nextCheck && car != null)
        {
            nextCheck = Time.time + 0.5f;
            stats = car.GetStats();
            cleanliness = car.GetCleanliness();
        }

        bool continuePressed = continueButton.action != null && continueButton.action.WasPressedThisFrame();
        bool skipPressed = skipButton.action != null && skipButton.action.WasPressedThisFrame();

        // Just finished: show "Done!" for a moment, then go on.
        if (doneTime >= 0f)
        {
            if (Time.time > doneTime + 1.5f)
                NextStep();
            return;
        }

        if (step.isDone == null)
        {
            if (continuePressed || skipPressed)
                NextStep();
        }
        else if (step.isDone())
        {
            doneTime = Time.time;
            bodyText.text = step.text + "\n\n<color=#7CFC7C><b>Done!</b></color>";
        }
        else if (skipPressed)
        {
            NextStep();
        }

        UpdateFooter(step);
    }

    void NextStep()
    {
        if (current == steps.Count - 1)
        {
            SceneManager.LoadScene(gameScene);
            return;
        }
        ShowStep(current + 1);
    }

    void ShowStep(int index)
    {
        current = index;
        doneTime = -1f;
        Step step = steps[index];
        step.onStart?.Invoke();
        titleText.text = step.title;
        bodyText.text = step.text;
        UpdateFooter(step);
    }

    void UpdateFooter(Step step)
    {
        string footer = "Step " + (current + 1) + " / " + steps.Count;
        if (step.progress != null && doneTime < 0f)
            footer += "     <color=#FFD54A>" + step.progress() + "</color>";
        if (step.isDone != null)
            footer += "     <color=#999999>B: skip</color>";
        footerText.text = footer;
    }

    // 0.42 -> "42%" (never more than 100%).
    static string Percent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    // The board turns to face the player (only around the up axis, if faceThePlayer is on),
    // and the arrow bobs above the thing to use next.
    void LateUpdate()
    {
        if (faceThePlayer && playerHead != null)
        {
            Vector3 away = transform.position - playerHead.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), Time.deltaTime * 2f);
        }

        if (arrow == null || steps == null)
            return;
        Transform target = steps[current].pointAt?.Invoke();
        arrow.gameObject.SetActive(target != null && doneTime < 0f);
        if (target == null)
            return;
        arrow.position = TopOf(target) + Vector3.up * (0.4f + Mathf.Sin(Time.time * 3f) * 0.05f);
        arrow.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
    }

    // The highest point of an object (all its meshes), so the arrow floats just above it.
    static Vector3 TopOf(Transform target)
    {
        bool found = false;
        Bounds bounds = new Bounds(target.position, Vector3.zero);
        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer)
                continue; // spray particles fly far away from the object
            if (found)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            found = true;
        }
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }
}
