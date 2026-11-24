using TMPro;
using UnityEngine;

// A short message that floats up from a spot and fades away, facing the player
// (for example "-$40" when buying something, or "+20 food" when eating).
// Call ShopPopup.Show(position, text, color) from any script.
public class ShopPopup : MonoBehaviour
{
    public static readonly Color Green = new Color(0.45f, 1f, 0.45f);
    public static readonly Color Red = new Color(1f, 0.4f, 0.35f);
    public static readonly Color Yellow = new Color(1f, 0.85f, 0.3f);

    const float LifeSeconds = 1.8f;

    TextMeshPro text;
    float startTime;
    Color color;

    public static void Show(Vector3 position, string message, Color color)
    {
        GameObject go = new GameObject("Popup");
        go.transform.position = position + Vector3.up * 0.15f;
        ShopPopup popup = go.AddComponent<ShopPopup>();
        popup.text = go.AddComponent<TextMeshPro>();
        popup.text.text = message;
        popup.text.fontSize = 0.45f;
        popup.text.fontStyle = FontStyles.Bold;
        popup.text.alignment = TextAlignmentOptions.Center;
        popup.text.textWrappingMode = TextWrappingModes.NoWrap;
        popup.text.outlineWidth = 0.2f;
        popup.text.outlineColor = new Color32(0, 0, 0, 200);
        popup.text.rectTransform.sizeDelta = new Vector2(1f, 0.3f);
        popup.color = color;
        popup.startTime = Time.time;
        popup.FacePlayer();
    }

    void Update()
    {
        float t = (Time.time - startTime) / LifeSeconds;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        transform.position += Vector3.up * (0.12f * Time.deltaTime);
        text.color = new Color(color.r, color.g, color.b, 1f - t * t);
        FacePlayer();
    }

    void FacePlayer()
    {
        Camera head = Camera.main;
        if (head != null)
            transform.rotation = Quaternion.LookRotation(transform.position - head.transform.position);
    }
}
