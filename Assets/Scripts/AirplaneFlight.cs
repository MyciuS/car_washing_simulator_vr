using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class AirplaneFlight : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float speed = 12f;
    [SerializeField, Min(1f)] float flightDistance = 900f;
    [SerializeField] Material smokeMaterial;

    Vector3 startPosition;
    Vector3 direction;
    float distance;
    ParticleSystem smoke;
    Material runtimeMaterial;
    Texture2D smokeTexture;

    void Start()
    {
        // flight.fbx has its nose along local +X.
        direction = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        startPosition = transform.position;
        Debug.Assert(direction.sqrMagnitude > 0f && smokeMaterial != null,
            "Airplane needs a horizontal heading and a smoke material.", this);
        if (direction.sqrMagnitude == 0f || smokeMaterial == null)
        {
            enabled = false;
            return;
        }

        var emitter = new GameObject("Smoke Trail");
        emitter.transform.SetParent(transform, false);
        Bounds bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
        emitter.transform.localPosition = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
        smoke = emitter.AddComponent<ParticleSystem>();
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = smoke.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startSpeed = 0f;
        main.startLifetime = 12f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startColor = new Color(0.92f, 0.94f, 0.96f, 0.6f);
        main.maxParticles = 256;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = smoke.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 1f;
        var shape = smoke.shape;
        shape.enabled = false;

        var size = smoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 4f));
        var fade = smoke.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = smoke.GetComponent<ParticleSystemRenderer>();
        smokeTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        smokeTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float radius = new Vector2((x + 0.5f) / 16f - 1f, (y + 0.5f) / 16f - 1f).magnitude;
                float alpha = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(radius));
                smokeTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        smokeTexture.Apply();
        runtimeMaterial = new Material(smokeMaterial);
        runtimeMaterial.SetTexture("_BaseMap", smokeTexture);
        renderer.sharedMaterial = runtimeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        smoke.Play();
    }

    void Update()
    {
        distance += speed * Time.deltaTime;
        if (distance >= flightDistance)
        {
            distance %= flightDistance;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            transform.position = startPosition + direction * distance;
            smoke.Play();
            return;
        }
        transform.position = startPosition + direction * distance;
    }

    void OnDestroy()
    {
        Destroy(runtimeMaterial);
        Destroy(smokeTexture);
    }
}
