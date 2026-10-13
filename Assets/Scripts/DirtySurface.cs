using UnityEngine;

// A painted surface covered in dirt that can be cleaned. Put it on a Quad
// (for example one side of the car). The dirt has two layers:
//   - loose mud: comes off with water
//   - grime: stubborn film that water alone can't remove
// Soap can be sprayed on top. Once it has soaked long enough, the dirt under it
// is "loosened" and water removes it (grime too) much faster (see CleaningTool).
// The paint color comes from the object's material (Base Color).
[RequireComponent(typeof(MeshRenderer), typeof(MeshCollider))]
public class DirtySurface : MonoBehaviour
{
    // How detailed the dirt is: pixels per meter of surface.
    public float pixelsPerMeter = 80f;

    [Header("Dirt colors")]
    public Color lightMudColor = new Color(0.55f, 0.45f, 0.32f);
    public Color darkMudColor = new Color(0.28f, 0.21f, 0.14f);
    public Color grimeColor = new Color(0.36f, 0.34f, 0.3f);
    // How strongly the grime covers the paint (0 = invisible, 1 = fully).
    public float grimeStrength = 0.6f;
    public Color foamColor = new Color(0.95f, 0.96f, 0.98f);

    int width;
    int height;
    Color paintColor;
    float[] looseDirt;
    float[] grime;
    float[] mudShade;
    float[] soap;
    // The time (Time.time) when the soap on each pixel has soaked enough.
    float[] soapReadyTime;
    // True where soaked soap has loosened the dirt. Stays true after the soap is rinsed off.
    bool[] loosened;
    Color32[] pixels;
    Texture2D surfaceTexture;
    bool textureChanged;

    void Start()
    {
        // Texture size follows the real size of this side (a Quad is 1 x 1 before scaling).
        width = Mathf.Max(8, Mathf.RoundToInt(transform.lossyScale.x * pixelsPerMeter));
        height = Mathf.Max(8, Mathf.RoundToInt(transform.lossyScale.y * pixelsPerMeter));

        Material material = GetComponent<MeshRenderer>().material;
        paintColor = material.GetColor("_BaseColor");

        MakeStartingDirt();

        // A texture that shows paint + dirt, put on this object's material.
        surfaceTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        surfaceTexture.wrapMode = TextureWrapMode.Clamp;
        surfaceTexture.SetPixels32(pixels);
        surfaceTexture.Apply(false);
        material.SetTexture("_BaseMap", surfaceTexture);
        material.SetColor("_BaseColor", Color.white);
    }

    // Fills the surface with mud and grime: more mud low on the car (road dirt),
    // plus random patches so it doesn't look like a flat color.
    void MakeStartingDirt()
    {
        int count = width * height;
        looseDirt = new float[count];
        grime = new float[count];
        mudShade = new float[count];
        soap = new float[count];
        soapReadyTime = new float[count];
        loosened = new bool[count];
        pixels = new Color32[count];
        float seed = Random.value * 100f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // World position of this pixel (a Quad goes from -0.5 to 0.5).
                Vector3 p = transform.TransformPoint((x + 0.5f) / width - 0.5f, (y + 0.5f) / height - 0.5f, 0f);
                float heightAboveGround = p.y - transform.root.position.y;

                float patches = Noise(p * 3f, seed);                 // big mud patches
                float splashes = Noise(p * 12f, seed + 50f);         // smaller splashes
                float grain = Noise(p * 45f, seed + 90f);            // fine dried-mud grain
                float road = Mathf.Clamp01(1.1f - heightAboveGround); // 1 near the ground, 0 at the top

                int i = y * width + x;
                looseDirt[i] = Mathf.Clamp01(0.35f + road * 0.7f + (patches - 0.5f) * 0.8f
                                             + Mathf.SmoothStep(0.55f, 0.8f, splashes) * 0.3f + (grain - 0.5f) * 0.2f);
                grime[i] = Mathf.Clamp01(0.35f + patches * 0.4f);
                mudShade[i] = Mathf.Clamp01(0.3f + road * 0.5f + (splashes - 0.5f) + (grain - 0.5f) * 0.4f);
                UpdatePixel(i);
            }
        }
    }

    // Smooth random value (0..1) that changes across the surface in any direction.
    float Noise(Vector3 p, float seed)
    {
        return (Mathf.PerlinNoise(p.x + seed, p.y)
              + Mathf.PerlinNoise(p.z + seed, p.y + 33f)
              + Mathf.PerlinNoise(p.x + 51f, p.z + seed)) / 3f * 1.4f - 0.2f;
    }

    // Applies a tool (water, soap...) in a round spot around a point on the surface (uv = 0..1).
    public void Spray(Vector2 uv, CleaningTool tool)
    {
        int centerX = (int)(uv.x * width);
        int centerY = (int)(uv.y * height);
        int radius = Mathf.Max(1, Mathf.RoundToInt(tool.radius * pixelsPerMeter));

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int px = centerX + x;
                int py = centerY + y;
                float distance = Mathf.Sqrt(x * x + y * y) / radius;
                if (distance > 1f || px < 0 || py < 0 || px >= width || py >= height)
                    continue;

                // Strongest in the middle of the spot, weaker at the edge.
                float amount = 1f - distance;
                int i = py * width + px;

                // Soap that has sat long enough makes the dirt easier to wash off.
                if (soap[i] > 0f && Time.time >= soapReadyTime[i])
                    loosened[i] = true;
                float looseRemoval = loosened[i] ? tool.soakedLooseDirtRemoval : tool.looseDirtRemoval;
                float grimeRemoval = loosened[i] ? tool.soakedGrimeRemoval : tool.grimeRemoval;
                looseDirt[i] = Mathf.Max(0f, looseDirt[i] - looseRemoval * amount);
                grime[i] = Mathf.Max(0f, grime[i] - grimeRemoval * amount);

                // Fresh soap starts its soak timer; water rinses soap off.
                if (tool.soapAdded > 0f)
                {
                    if (soap[i] <= 0f)
                        soapReadyTime[i] = Time.time + tool.soapSoakTime;
                    soap[i] = Mathf.Min(1f, soap[i] + tool.soapAdded * amount);
                }
                soap[i] = Mathf.Max(0f, soap[i] - tool.soapRinse * amount);

                UpdatePixel(i);
            }
        }

        textureChanged = true;
    }

    // Send the new pixels to the GPU once per frame, only if something was cleaned.
    void LateUpdate()
    {
        if (textureChanged)
        {
            surfaceTexture.SetPixels32(pixels);
            surfaceTexture.Apply(false);
            textureChanged = false;
        }
    }

    // Works out the color of one pixel: paint, then grime, then mud, then soap foam on top.
    // Alpha is smoothness: clean paint is shiny, dirt and foam are matte.
    void UpdatePixel(int i)
    {
        Color color = Color.Lerp(paintColor, grimeColor, grime[i] * grimeStrength);
        Color mud = Color.Lerp(lightMudColor, darkMudColor, mudShade[i]);
        color = Color.Lerp(color, mud, looseDirt[i]);
        color = Color.Lerp(color, foamColor, soap[i] * 0.85f);
        color.a = Mathf.Lerp(0.85f, 0.1f, Mathf.Max(looseDirt[i], grime[i] * grimeStrength, soap[i]));
        pixels[i] = color;
    }
}
