using System.Collections.Generic;
using UnityEngine;

// A wet layer over a flat floor (the car wash floor). Water that lands on the floor, or drips
// off a car, makes it darker and glossy. Soap that drips off a car (or is sprayed on the floor)
// leaves foamy puddles, and water rinses them away. Water dries and foam pops by itself over time.
// Put this on a flat Quad lying just above the floor (rotated to face up), with a material that
// uses the "Custom/Wet Ground" shader. The Quad's size is the area that can get wet.
[RequireComponent(typeof(MeshRenderer))]
public class WetGround : MonoBehaviour
{
    // How detailed the wet patches are: pixels per meter of floor.
    public float pixelsPerMeter = 12f;
    // How long a soaked spot takes to dry, and a soap puddle to disappear, in seconds.
    public float dryingTime = 150f;
    public float foamTime = 90f;
    // Smallest wet spot, in meters (water splashes and spreads when it lands).
    public float minSpotRadius = 0.3f;

    // Every wet floor in the scene, so sprays and cars can find the one under them.
    static readonly List<WetGround> allFloors = new List<WetGround>();

    int width;
    int height;
    float[] water;
    float[] foam;
    Color32[] pixels;
    Texture2D wetMap;
    bool textureChanged;
    float nextDrying;

    void OnEnable()
    {
        allFloors.Add(this);
    }

    void OnDisable()
    {
        allFloors.Remove(this);
    }

    void Start()
    {
        // A Quad is 1 x 1 m before scaling.
        Vector3 size = transform.lossyScale;
        width = Mathf.Max(4, Mathf.RoundToInt(size.x * pixelsPerMeter));
        height = Mathf.Max(4, Mathf.RoundToInt(size.y * pixelsPerMeter));
        water = new float[width * height];
        foam = new float[width * height];
        pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
            UpdatePixel(i);

        // Red = how wet, green = how much foam. The shader turns that into dark, glossy or foamy floor.
        // It's data, not a picture, so it must be linear (no color correction of the values).
        wetMap = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        wetMap.wrapMode = TextureWrapMode.Clamp;
        wetMap.SetPixels32(pixels);
        wetMap.Apply(false);
        GetComponent<MeshRenderer>().material.SetTexture("_WetMap", wetMap);
    }

    // Wets the floor where water or soap lands. Called by sprays, and by cars that drip.
    // water = how much wetter the spot gets, soap = how much foam it leaves (per call).
    public static void Splash(Vector3 point, float radius, float water, float soap)
    {
        if (water <= 0f && soap <= 0f)
            return;
        foreach (WetGround floor in allFloors)
            floor.AddWater(point, radius, water, soap);
    }

    void AddWater(Vector3 point, float radius, float addWater, float addSoap)
    {
        if (water == null)
            return;
        // Water falls straight down, so anything above this floor lands on it.
        float heightAbove = point.y - transform.position.y;
        if (heightAbove < -0.3f || heightAbove > 3f)
            return;
        Vector3 local = transform.InverseTransformPoint(point);
        if (Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.y) > 0.5f)
            return;

        float centerX = (local.x + 0.5f) * width;
        float centerY = (local.y + 0.5f) * height;
        float radiusInPixels = Mathf.Max(radius, minSpotRadius) * pixelsPerMeter;
        int reach = Mathf.CeilToInt(radiusInPixels);
        for (int y = (int)centerY - reach; y <= (int)centerY + reach; y++)
        {
            for (int x = (int)centerX - reach; x <= (int)centerX + reach; x++)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                    continue;
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(centerX, centerY)) / radiusInPixels;
                if (distance > 1f)
                    continue;
                // Strongest in the middle of the spot, weaker at the edge.
                float amount = 1f - distance;
                int i = y * width + x;
                water[i] = Mathf.Min(1f, water[i] + addWater * amount);
                // Soap adds foam, water rinses foam away.
                foam[i] = Mathf.Clamp01(foam[i] + (addSoap - addWater * 0.5f) * amount);
                UpdatePixel(i);
            }
        }
        textureChanged = true;
    }

    // Twice a second the floor dries a little and the foam pops a little.
    void Update()
    {
        if (water == null || Time.time < nextDrying)
            return;
        nextDrying = Time.time + 0.5f;

        float dried = 0.5f / dryingTime;
        float popped = 0.5f / foamTime;
        for (int i = 0; i < water.Length; i++)
        {
            if (water[i] <= 0f && foam[i] <= 0f)
                continue;
            water[i] = Mathf.Max(0f, water[i] - dried);
            foam[i] = Mathf.Max(0f, foam[i] - popped);
            UpdatePixel(i);
            textureChanged = true;
        }
    }

    // Send the new pixels to the GPU once per frame, only if something changed.
    void LateUpdate()
    {
        if (!textureChanged)
            return;
        wetMap.SetPixels32(pixels);
        wetMap.Apply(false);
        textureChanged = false;
    }

    void UpdatePixel(int i)
    {
        pixels[i] = new Color32((byte)(water[i] * 255f), (byte)(foam[i] * 255f), 0, 255);
    }
}
