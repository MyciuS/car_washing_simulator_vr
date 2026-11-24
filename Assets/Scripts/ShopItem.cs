using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Something for sale in the shop. Grabbing it buys it: the price is taken from the player's money
// and the item is theirs to use. Without enough money it can't be picked up (it stays on the shelf
// and a red "Need $.." pops up). After it is bought, a new one appears on the shelf.
// Put this on the item, next to its XR Grab Interactable.
public class ShopItem : MonoBehaviour
{
    public string itemName = "Item";
    public int price = 10;
    // The price tag on the shelf in front of the item (its text is filled in automatically).
    public TextMeshPro priceTag;
    // A new one appears on the shelf this many seconds after this one is bought (-1 = sold only once).
    public float restockSeconds = 2f;

    // True once the player has paid for this one.
    public bool IsBought { get; private set; }

    XRBaseInteractable interactable;
    Rigidbody body;
    PlayerStats stats;
    Vector3 shelfPosition;
    Quaternion shelfRotation;
    Transform shelfParent;
    bool shelfKinematic;
    bool shelfGravity;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        interactable.selectEntered.AddListener(OnGrabbed);
        body = GetComponent<Rigidbody>();
        shelfPosition = transform.position;
        shelfRotation = transform.rotation;
        shelfParent = transform.parent;
        if (body != null)
        {
            shelfKinematic = body.isKinematic;
            shelfGravity = body.useGravity;
        }
    }

    void Start()
    {
        stats = FindAnyObjectByType<PlayerStats>();
        if (priceTag != null)
            priceTag.text = TagText();
    }

    public string TagText()
    {
        return "<size=55%>" + itemName + "</size>\n<b>$" + price + "</b>";
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (IsBought)
            return;

        // Only a hand can buy it (not, for example, the gun's nozzle socket when it's knocked against it).
        if (args.interactorObject is XRSocketInteractor)
        {
            StartCoroutine(PutBack(args.interactorObject));
            return;
        }

        if (stats == null || !stats.TrySpend(price))
        {
            ShopPopup.Show(transform.position, "Need $" + price, ShopPopup.Red);
            StartCoroutine(PutBack(args.interactorObject));
            return;
        }

        IsBought = true;
        ShopPopup.Show(transform.position, itemName + "\n-$" + price, ShopPopup.Green);
        if (restockSeconds >= 0f)
            Restock();
    }

    // Not bought: let go of it and put it back where it was on the shelf.
    IEnumerator PutBack(IXRSelectInteractor interactor)
    {
        // (Letting go during the grab itself confuses the interaction manager, so wait a frame.)
        yield return null;
        if (interactable.isSelected)
            interactable.interactionManager.SelectExit(interactor, (IXRSelectInteractable)interactable);
        yield return null;
        transform.SetPositionAndRotation(shelfPosition, shelfRotation);
        if (body != null)
        {
            body.isKinematic = shelfKinematic;
            body.useGravity = shelfGravity;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }

    // A fresh copy goes back on the shelf after a short while.
    void Restock()
    {
        GameObject copy = Instantiate(gameObject, shelfPosition, shelfRotation, shelfParent);
        copy.name = name;
        // The copy was made from the held item: give it the shelf's physics back.
        Rigidbody copyBody = copy.GetComponent<Rigidbody>();
        if (copyBody != null)
        {
            copyBody.isKinematic = shelfKinematic;
            copyBody.useGravity = shelfGravity;
        }
        copy.SetActive(false);
        Restocker.Instance.StartCoroutine(ShowLater(copy, restockSeconds));
    }

    static IEnumerator ShowLater(GameObject copy, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (copy != null)
            copy.SetActive(true);
    }

    // Runs the restock timers (an item can be used up or thrown away before its copy appears).
    class Restocker : MonoBehaviour
    {
        static Restocker instance;

        public static Restocker Instance
        {
            get
            {
                if (instance == null)
                    instance = new GameObject("Shop Restocker").AddComponent<Restocker>();
                return instance;
            }
        }
    }
}
