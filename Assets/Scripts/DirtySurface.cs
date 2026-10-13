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

    [Header("Foam")]
    public Color foamColor = new Color(0.95f, 0.96f, 0.98f);
    // Foam slowly turns this color while it soaks. Fully this color = ready to rinse.
    public Color soakedFoamColor = new Color(0.78f, 0.65f, 0.48f);
    // How big one tile of the bubble pattern is on the car, in meters (smaller = smaller bubbles).
    public float bubbleTileSize = 0.15f;
    // How wet and shiny the foam looks (0 = matte, 1 = mirror).
    public float foamSmoothness = 0.6f;

    int width;
    int height;
    Color paintColor;
    float[] looseDirt;
    float[] grime;
    float[] mudShade;
    float[] soap;
    // When soap first landed on each pixel, and when it has soaked enough (Time.time).
    float[] soapStartTime;
    float[] soapReadyTime;
    // True where soaked soap has loosened the dirt. Stays true after the soap is rinsed off.
    bool[] loosened;
    // Random value per pixel so foam edges look uneven instead of perfectly round.
    float[] foamVariation;
    Color32[] pixels;
    Texture2D surfaceTexture;
    // Where the foam is (0..255 per pixel). The material uses it to show the bubble pattern only there.
    byte[] foamMaskPixels;
    Texture2D foamMask;
    FoamClumps foamClumps;
    bool textureChanged;
    // Timer for refreshing the soaking foam color (twice a second is enough).
    float nextSoakRefresh;

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

        // Foam mask: the material's "Detail" bubble texture and bumps only show where foam is.
        foamMask = new Texture2D(width, height, TextureFormat.Alpha8, false);
        foamMask.wrapMode = TextureWrapMode.Clamp;
        foamMask.SetPixelData(foamMaskPixels, 0);
        foamMask.Apply(false);
        material.SetTexture("_DetailMask", foamMask);
        // Repeat the bubble pattern so bubbles have the same size on every side of the car.
        material.SetTextureScale("_DetailAlbedoMap",
            new Vector2(transform.lossyScale.x / bubbleTileSize, transform.lossyScale.y / bubbleTileSize));

        foamClumps = GetComponent<FoamClumps>();
    }

    // How much soap is at a point on the surface (uv = 0..1). Used by FoamClumps.
    public float GetSoap(Vector2 uv)
    {
        int x = Mathf.Clamp((int)(uv.x * width), 0, width - 1);
        int y = Mathf.Clamp((int)(uv.y * height), 0, height - 1);
        return soap[y * width + x];
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
        soapStartTime = new float[count];
        soapReadyTime = new float[count];
        loosened = new bool[count];
        foamVariation = new float[count];
        foamMaskPixels = new byte[count];
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
                foamVariation[i] = Mathf.Clamp01(Noise(p * 20f, seed + 130f));
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
                    {
                        soapStartTime[i] = Time.time;
                        soapReadyTime[i] = Time.time + tool.soapSoakTime;
                    }
                    soap[i] = Mathf.Min(1f, soap[i] + tool.soapAdded * amount);
                }
                soap[i] = Mathf.Max(0f, soap[i] - tool.soapRinse * amount);

                UpdatePixel(i);
            }
        }

        textureChanged = true;

        // Tell the 3D foam mounds: new soap may add a mound, any soap change resizes them.
        if (foamClumps != null)
        {
            if (tool.soapAdded > 0f)
                foamClumps.SoapHit(uv, tool.radius);
            else if (tool.soapRinse > 0f)
                foamClumps.SoapChanged();
        }
    }

    // How far the soap at a point has soaked: 0 = just sprayed, 1 = ready to rinse.
    public float GetSoakProgress(Vector2 uv)
    {
        int x = Mathf.Clamp((int)(uv.x * width), 0, width - 1);
        int y = Mathf.Clamp((int)(uv.y * height), 0, height - 1);
        return SoakProgress(y * width + x);
    }

    float SoakProgress(int i)
    {
        if (soap[i] <= 0f)
            return 0f;
        float soakTime = soapReadyTime[i] - soapStartTime[i];
        if (soakTime <= 0f)
            return 1f;
        return Mathf.Clamp01((Time.time - soapStartTime[i]) / soakTime);
    }

    // Twice a second, recolor the soapy pixels so the foam slowly turns brown while it soaks.
    void Update()
    {
        if (Time.time < nextSoakRefresh)
            return;
        nextSoakRefresh = Time.time + 0.5f;

        bool anySoap = false;
        for (int i = 0; i < soap.Length; i++)
        {
            if (soap[i] > 0f)
            {
                UpdatePixel(i);
                anySoap = true;
            }
        }
        if (anySoap)
        {
            textureChanged = true;
            if (foamClumps != null)
                foamClumps.SoapChanged(); // the 3D mounds change color too
        }
    }

    // Send the new pixels to the GPU once per frame, only if something was cleaned.
    void LateUpdate()
    {
        if (textureChanged)
        {
            surfaceTexture.SetPixels32(pixels);
            surfaceTexture.Apply(false);
            foamMask.SetPixelData(foamMaskPixels, 0);
            foamMask.Apply(false);
            textureChanged = false;
        }
    }

    // Works out the color of one pixel: paint, then grime, then mud, then foam on top.
    // Alpha is smoothness: clean paint is shiny, dirt is matte, foam is wet and a bit shiny.
    void UpdatePixel(int i)
    {
        Color color = Color.Lerp(paintColor, grimeColor, grime[i] * grimeStrength);
        Color mud = Color.Lerp(lightMudColor, darkMudColor, mudShade[i]);
        color = Color.Lerp(color, mud, looseDirt[i]);
        float smoothness = Mathf.Lerp(0.85f, 0.1f, Mathf.Max(looseDirt[i], grime[i] * grimeStrength));

        // Foam: uneven edges (foamVariation), thin foam still lets a little paint show through.
        float foam = Mathf.Clamp01(soap[i] * 2.5f - foamVariation[i] * 0.6f);
        // Foam turns from white to light brown while it soaks (brown = dirt loosened, ready to rinse).
        Color currentFoamColor = Color.Lerp(foamColor, soakedFoamColor, SoakProgress(i));
        color = Color.Lerp(color, currentFoamColor, foam * 0.92f);
        color.a = Mathf.Lerp(smoothness, foamSmoothness, foam);
        pixels[i] = color;
        foamMaskPixels[i] = (byte)(foam * 255f);
    }
}
