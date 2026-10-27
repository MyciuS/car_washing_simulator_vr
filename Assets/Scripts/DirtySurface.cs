using UnityEngine;

// A painted surface covered in dirt that can be cleaned: one side of the test car,
// or a whole car model. Works on any mesh whose UVs don't overlap. The dirt has two layers:
//   - loose mud: comes off with water
//   - grime: stubborn film that water alone can't remove
// Soap can be sprayed on top. Once it has soaked long enough, the dirt under it
// is "loosened" and water removes it (grime too) much faster (see CleaningTool).
// Thick soap slowly runs down the car and drips off the bottom edges onto the ground (WetGround).
// The paint comes from the object's material (Base Map texture and Base Color).
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class DirtySurface : MonoBehaviour
{
    // How detailed the dirt is: pixels per meter of surface.
    public float pixelsPerMeter = 80f;

    // Optional: where the car starts dirty, painted by an artist (white = muddy).
    // Leave empty for random road dirt (more mud low on the car).
    public Texture2D dirtMask;

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

    [Header("Water and shine")]
    // How shiny clean paint is before and after it is polished with the towel (0 = matte, 1 = mirror).
    public float dullSmoothness = 0.6f;
    public float polishedSmoothness = 0.95f;
    // Wet paint looks a bit darker and very glossy.
    public float wetSmoothness = 0.9f;
    public float wetDarkening = 0.15f;
    // How long water takes to dry on its own, in seconds (the towel dries it right away).
    public float dryingTime = 120f;

    [Header("Soap running down")]
    // Thick soap slowly slides down the car. This much soap always sticks where it is.
    public float soapThatSticks = 0.12f;
    // How much of the soap above that moves one pixel down per step (4 steps a second).
    public float soapRunSpeed = 0.25f;

    int width;
    int height;
    Color paintColor;
    // Paint color of every pixel (from the material's Base Map), or null for one plain color.
    Color[] paintPixels;
    // Where every pixel is on the car in the world, and whether it is on the car at all
    // (parts of a texture can be empty space between the pieces of the model).
    Vector3[] pixelPosition;
    bool[] onSurface;
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
    // How wet the paint is (0 = dry, 1 = soaking), and how polished (0 = dull, 1 = fully shiny).
    float[] wetness;
    float[] shine;
    // For every pixel, the neighbour pixel just below it on the car, where soap runs to (-1 = none).
    int[] downhill;
    // Pixels at a bottom edge: soap that reaches them drips off onto the ground.
    bool[] dripEdge;
    float[] soapAfterRunning;
    System.Collections.Generic.List<int> soapMoved = new System.Collections.Generic.List<int>();
    float nextSoapRun;
    // Middle of the whole car, so drips can fall a little outside the car instead of under it.
    Vector3 carCenter;
    Color32[] pixels;
    Texture2D surfaceTexture;
    // Where the foam is (0..255 per pixel). The material uses it to show the bubble pattern only there.
    byte[] foamMaskPixels;
    Texture2D foamMask;
    FoamClumps foamClumps;
    bool textureChanged;
    // Timer for refreshing the soaking foam color (twice a second is enough).
    float nextSoakRefresh;
    // Less soap than this is invisible, so it counts as no soap (no bonus, gets rinsed away).
    const float MinSoap = 0.05f;

    void Start()
    {
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        Material material = GetComponent<MeshRenderer>().material;
        paintColor = material.GetColor("_BaseColor");

        ChooseTextureSize(mesh);
        Texture paintTexture = material.GetTexture("_BaseMap");
        if (paintTexture != null)
            paintPixels = ReadTexture(paintTexture);
        FindPixelPositions(mesh);
        FindDownhillPixels();
        MakeStartingDirt(dirtMask != null ? ReadTexture(dirtMask) : null);

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
        // Repeat the bubble pattern so bubbles are about the same size everywhere.
        material.SetTextureScale("_DetailAlbedoMap",
            new Vector2(width / pixelsPerMeter / bubbleTileSize, height / pixelsPerMeter / bubbleTileSize));

        foamClumps = GetComponent<FoamClumps>();
    }

    // Picks the texture size from how many meters of the model one full texture covers,
    // across (u) and up (v). A flat 4.2 m x 0.8 m side gets 336 x 64 pixels.
    void ChooseTextureSize(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        int[] triangles = mesh.triangles;
        float metersAcross = 0f, metersUp = 0f, totalArea = 0f;

        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 p0 = transform.TransformPoint(vertices[triangles[t]]);
            Vector3 p1 = transform.TransformPoint(vertices[triangles[t + 1]]);
            Vector3 p2 = transform.TransformPoint(vertices[triangles[t + 2]]);
            Vector2 uv1 = uvs[triangles[t + 1]] - uvs[triangles[t]];
            Vector2 uv2 = uvs[triangles[t + 2]] - uvs[triangles[t]];
            float uvArea = uv1.x * uv2.y - uv2.x * uv1.y;
            if (Mathf.Abs(uvArea) < 1e-9f)
                continue;

            // How far you move in the world when u (or v) goes from 0 to 1 on this triangle.
            Vector3 alongU = ((p1 - p0) * uv2.y - (p2 - p0) * uv1.y) / uvArea;
            Vector3 alongV = ((p2 - p0) * uv1.x - (p1 - p0) * uv2.x) / uvArea;
            float weight = Mathf.Abs(uvArea);
            metersAcross += alongU.magnitude * weight;
            metersUp += alongV.magnitude * weight;
            totalArea += weight;
        }

        width = Mathf.Clamp(Mathf.RoundToInt(metersAcross / totalArea * pixelsPerMeter), 8, 2048);
        height = Mathf.Clamp(Mathf.RoundToInt(metersUp / totalArea * pixelsPerMeter), 8, 2048);
    }

    // Goes over every triangle of the model and remembers, for each texture pixel,
    // where that pixel is on the car in the world.
    void FindPixelPositions(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        int[] triangles = mesh.triangles;
        pixelPosition = new Vector3[width * height];
        onSurface = new bool[width * height];
        Vector2 size = new Vector2(width, height);

        for (int t = 0; t < triangles.Length; t += 3)
        {
            // The triangle in texture pixels, and in the world.
            Vector2 a = Vector2.Scale(uvs[triangles[t]], size);
            Vector2 b = Vector2.Scale(uvs[triangles[t + 1]], size);
            Vector2 c = Vector2.Scale(uvs[triangles[t + 2]], size);
            Vector3 pa = transform.TransformPoint(vertices[triangles[t]]);
            Vector3 pb = transform.TransformPoint(vertices[triangles[t + 1]]);
            Vector3 pc = transform.TransformPoint(vertices[triangles[t + 2]]);
            float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
            if (Mathf.Abs(area) < 1e-9f)
                continue;

            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // How much of each corner this pixel is made of (all three >= 0 means inside).
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float wb = ((p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y)) / area;
                    float wc = ((b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y)) / area;
                    float wa = 1f - wb - wc;
                    if (wa < 0f || wb < 0f || wc < 0f)
                        continue;
                    int i = y * width + x;
                    pixelPosition[i] = pa * wa + pb * wb + pc * wc;
                    onSurface[i] = true;
                }
            }
        }

        // Grow each piece 2 pixels into the empty space around it, so no lines show along its edges.
        for (int pass = 0; pass < 2; pass++)
        {
            bool[] before = (bool[])onSurface.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (before[i])
                        continue;
                    int n = -1;
                    if (x > 0 && before[i - 1]) n = i - 1;
                    else if (x < width - 1 && before[i + 1]) n = i + 1;
                    else if (y > 0 && before[i - width]) n = i - width;
                    else if (y < height - 1 && before[i + width]) n = i + width;
                    if (n < 0)
                        continue;
                    pixelPosition[i] = pixelPosition[n];
                    onSurface[i] = true;
                }
            }
        }
    }

    // For every pixel, finds the neighbouring pixel that is lowest on the car: that is where soap runs to.
    // Only neighbours that are really next to it on the car count (not across a seam in the texture).
    // A pixel on a slope with nothing lower next to it is a bottom edge, where soap drips off.
    void FindDownhillPixels()
    {
        int count = width * height;
        downhill = new int[count];
        dripEdge = new bool[count];
        soapAfterRunning = new float[count];
        float pixelSize = 1f / pixelsPerMeter;
        const float steep = 0.3f; // flatter than this (about 17 degrees) and soap stays put
        Bounds car = GetComponent<Renderer>().bounds;
        foreach (Renderer r in transform.root.GetComponentsInChildren<Renderer>())
            car.Encapsulate(r.bounds);
        carCenter = car.center;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                downhill[i] = -1;
                if (!onSurface[i])
                    continue;
                float steepest = steep;
                bool hasHigherNeighbour = false;
                for (int ny = y - 1; ny <= y + 1; ny++)
                {
                    for (int nx = x - 1; nx <= x + 1; nx++)
                    {
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height || (nx == x && ny == y))
                            continue;
                        int n = ny * width + nx;
                        if (!onSurface[n])
                            continue;
                        Vector3 step = pixelPosition[n] - pixelPosition[i];
                        float distance = step.magnitude;
                        if (distance < 1e-5f || distance > pixelSize * 3f)
                            continue;
                        float slope = -step.y / distance; // how much it goes down per meter
                        if (slope > steepest)
                        {
                            steepest = slope;
                            downhill[i] = n;
                        }
                        if (slope < -steep)
                            hasHigherNeighbour = true;
                    }
                }
                dripEdge[i] = downhill[i] < 0 && hasHigherNeighbour;
            }
        }
    }

    // Thick soap slides one pixel down the car. At a bottom edge it drips off onto the ground.
    void RunSoapDown()
    {
        System.Array.Copy(soap, soapAfterRunning, soap.Length);
        soapMoved.Clear();
        for (int i = 0; i < soap.Length; i++)
        {
            float extra = soap[i] - soapThatSticks;
            if (extra <= 0f)
                continue;
            float running = extra * soapRunSpeed;
            int below = downhill[i];
            if (below >= 0)
            {
                // Soap arriving on a clean spot keeps soaking from where it started.
                if (soapAfterRunning[below] <= 0f)
                {
                    soapStartTime[below] = soapStartTime[i];
                    soapReadyTime[below] = soapReadyTime[i];
                }
                soapAfterRunning[below] = Mathf.Min(1f, soapAfterRunning[below] + running);
                soapAfterRunning[i] -= running;
                soapMoved.Add(i);
                soapMoved.Add(below);
            }
            else if (dripEdge[i])
            {
                soapAfterRunning[i] -= running;
                soapMoved.Add(i);
                // It drips onto the floor just outside the car.
                Vector3 outward = pixelPosition[i] - carCenter;
                outward.y = 0f;
                WetGround.Splash(pixelPosition[i] + outward.normalized * 0.12f, 0.1f, running * 0.3f, running * 0.5f);
            }
        }
        if (soapMoved.Count == 0)
            return;

        float[] before = soap;
        soap = soapAfterRunning;
        soapAfterRunning = before;
        foreach (int i in soapMoved)
            UpdatePixel(i);
        textureChanged = true;
        if (foamClumps != null)
            foamClumps.SoapChanged();
    }

    // Reads any texture at this surface's size (works even if the texture isn't marked Read/Write).
    Color[] ReadTexture(Texture source)
    {
        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, temporary);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;
        Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        copy.Apply(false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        Color[] colors = copy.GetPixels();
        Destroy(copy);
        return colors;
    }

    // How much soap is at a point on the surface (uv = 0..1). Used by FoamClumps.
    public float GetSoap(Vector2 uv)
    {
        return soap[PixelIndex(uv)];
    }

    // The pixel at a point on the surface (uv = 0..1).
    int PixelIndex(Vector2 uv)
    {
        int x = Mathf.Clamp((int)(uv.x * width), 0, width - 1);
        int y = Mathf.Clamp((int)(uv.y * height), 0, height - 1);
        return y * width + x;
    }

    // Fills the surface with mud and grime. With a dirt mask the mud goes where the mask is white,
    // otherwise there is more mud low on the car (road dirt). Random patches on top so it
    // doesn't look like a flat color.
    void MakeStartingDirt(Color[] mask)
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
        wetness = new float[count];
        shine = new float[count];
        foamMaskPixels = new byte[count];
        pixels = new Color32[count];
        float seed = Random.value * 100f;
        float groundY = LowestPointOfCar();

        for (int i = 0; i < count; i++)
        {
            if (onSurface[i])
            {
                Vector3 p = pixelPosition[i];
                float patches = Noise(p * 3f, seed);                 // big mud patches
                float splashes = Noise(p * 12f, seed + 50f);         // smaller splashes
                float grain = Noise(p * 45f, seed + 90f);            // fine dried-mud grain
                float road = Mathf.Clamp01(1.1f - (p.y - groundY));  // 1 near the ground, 0 at the top

                if (mask != null)
                    looseDirt[i] = Mathf.Clamp01(mask[i].grayscale * (0.75f + (patches - 0.5f) * 0.6f) + (grain - 0.5f) * 0.2f);
                else
                    looseDirt[i] = Mathf.Clamp01(0.35f + road * 0.7f + (patches - 0.5f) * 0.8f
                                                 + Mathf.SmoothStep(0.55f, 0.8f, splashes) * 0.3f + (grain - 0.5f) * 0.2f);
                grime[i] = Mathf.Clamp01(0.35f + patches * 0.4f);
                mudShade[i] = Mathf.Clamp01(0.3f + road * 0.5f + (splashes - 0.5f) + (grain - 0.5f) * 0.4f);
                foamVariation[i] = Mathf.Clamp01(Noise(p * 20f, seed + 130f));
            }
            UpdatePixel(i);
        }
    }

    // The lowest point of the whole car (all its meshes), used as "the ground" for road dirt.
    float LowestPointOfCar()
    {
        float lowest = float.MaxValue;
        foreach (MeshRenderer r in transform.root.GetComponentsInChildren<MeshRenderer>())
            lowest = Mathf.Min(lowest, r.bounds.min.y);
        return lowest;
    }

    // Smooth random value (0..1) that changes across the surface in any direction.
    float Noise(Vector3 p, float seed)
    {
        return (Mathf.PerlinNoise(p.x + seed, p.y)
              + Mathf.PerlinNoise(p.z + seed, p.y + 33f)
              + Mathf.PerlinNoise(p.x + 51f, p.z + seed)) / 3f * 1.4f - 0.2f;
    }

    // Applies a tool (water, soap, sponge...) in a round spot around the point where it hit the car.
    // uv = the hit's place on the texture, point/normal = the hit in the world.
    // strength scales the tool (1 = one full hit; hand tools pass less for a short rub).
    public void Spray(Vector2 uv, Vector3 point, Vector3 normal, CleaningTool tool, float strength = 1f)
    {
        int centerX = (int)(uv.x * width);
        int centerY = (int)(uv.y * height);
        // Look at the pixels around the hit on the texture; the real distance on the car decides.
        int reach = Mathf.CeilToInt(tool.radius * pixelsPerMeter * 1.5f);

        for (int y = -reach; y <= reach; y++)
        {
            for (int x = -reach; x <= reach; x++)
            {
                int px = centerX + x;
                int py = centerY + y;
                if (px < 0 || py < 0 || px >= width || py >= height)
                    continue;
                int i = py * width + px;
                if (!onSurface[i])
                    continue;
                float distance = Vector3.Distance(pixelPosition[i], point) / tool.radius;
                if (distance > 1f)
                    continue;

                // Strongest in the middle of the spot, weaker at the edge.
                float amount = (1f - distance) * strength;

                // Soap that has sat long enough makes the dirt easier to wash off.
                if (soap[i] >= MinSoap && Time.time >= soapReadyTime[i])
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
                if (tool.soapRinse > 0f && soap[i] < MinSoap)
                    soap[i] = 0f; // rinse away the last invisible bit too

                // Water makes the paint wet; the towel dries it and polishes clean, dry paint.
                wetness[i] = Mathf.Clamp01(wetness[i] + (tool.wetnessAdded - tool.drying) * amount);
                if (tool.polish > 0f && wetness[i] < 0.1f && IsClean(i))
                    shine[i] = Mathf.Min(1f, shine[i] + tool.polish * amount);

                UpdatePixel(i);
            }
        }

        textureChanged = true;

        // Tell the 3D foam mounds: new soap may add a mound, any soap change resizes them.
        if (foamClumps != null)
        {
            if (tool.soapAdded > 0f)
                foamClumps.SoapHit(uv, point, normal);
            else if (tool.soapRinse > 0f)
                foamClumps.SoapChanged();
        }
    }

    // No mud, no grime and no soap left on this pixel.
    bool IsClean(int i)
    {
        return looseDirt[i] < 0.05f && grime[i] < 0.05f && soap[i] < MinSoap;
    }

    // How far the soap at a point has soaked: 0 = just sprayed, 1 = ready to rinse.
    public float GetSoakProgress(Vector2 uv)
    {
        return SoakProgress(PixelIndex(uv));
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

    // Twice a second, recolor the soapy pixels so the foam slowly turns brown while it soaks,
    // and let wet paint dry a little.
    // Only pixels still soaking (or that just finished) or still wet are recolored, so the rest costs nothing.
    void Update()
    {
        if (Time.time >= nextSoapRun)
        {
            nextSoapRun = Time.time + 0.25f;
            RunSoapDown();
        }

        if (Time.time < nextSoakRefresh)
            return;
        nextSoakRefresh = Time.time + 0.5f;

        float driedPerRefresh = 0.5f / dryingTime;
        bool anySoaking = false;
        bool anyWet = false;
        for (int i = 0; i < soap.Length; i++)
        {
            bool soaking = soap[i] > 0f && Time.time - 0.5f < soapReadyTime[i];
            bool wet = wetness[i] > 0f;
            if (wet)
                wetness[i] = Mathf.Max(0f, wetness[i] - driedPerRefresh);
            if (soaking || wet)
                UpdatePixel(i);
            anySoaking |= soaking;
            anyWet |= wet;
        }
        if (anySoaking || anyWet)
            textureChanged = true;
        if (anySoaking && foamClumps != null)
            foamClumps.SoapChanged(); // the 3D mounds change color too
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

    // Works out the color of one pixel: paint, then grime, then mud, then water, then foam on top.
    // Alpha is smoothness: clean paint is shiny (more after polishing), wet paint is glossy,
    // dirt is matte, foam is wet and a bit shiny.
    void UpdatePixel(int i)
    {
        Color paint = paintPixels != null ? paintPixels[i] * paintColor : paintColor;
        Color color = Color.Lerp(paint, grimeColor, grime[i] * grimeStrength);
        Color mud = Color.Lerp(lightMudColor, darkMudColor, mudShade[i]);
        color = Color.Lerp(color, mud, looseDirt[i]);
        color *= 1f - wetDarkening * wetness[i];

        float paintSmoothness = Mathf.Lerp(dullSmoothness, polishedSmoothness, shine[i]);
        paintSmoothness = Mathf.Lerp(paintSmoothness, Mathf.Max(paintSmoothness, wetSmoothness), wetness[i]);
        float smoothness = Mathf.Lerp(paintSmoothness, 0.1f, Mathf.Max(looseDirt[i], grime[i] * grimeStrength));

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
