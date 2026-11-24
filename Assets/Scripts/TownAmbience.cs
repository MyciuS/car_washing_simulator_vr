using UnityEngine;

public class TownAmbience : MonoBehaviour
{
    public AudioClip windClip;
    public AudioClip birdClip;
    [Range(0f, 1f)] public float windVolume = 0.04f;
    [Range(0f, 1f)] public float birdVolume = 0.35f;

    AudioSource birds;
    float nextBird;

    void Start()
    {
        if (windClip != null)
        {
            AudioSource wind = gameObject.AddComponent<AudioSource>();
            wind.clip = windClip;
            wind.volume = windVolume;
            wind.loop = true;
            wind.spatialBlend = 0f;
            wind.priority = 200;
            wind.Play();
        }
        birds = new GameObject("Distant birds").AddComponent<AudioSource>();
        birds.transform.SetParent(transform);
        birds.clip = birdClip;
        birds.volume = birdVolume;
        birds.spatialBlend = 1f;
        birds.minDistance = 8f;
        birds.maxDistance = 35f;
        birds.dopplerLevel = 0f;
        birds.priority = 200;
        nextBird = Time.time + Random.Range(3f, 7f);
    }

    void Update()
    {
        if (birdClip == null || Time.time < nextBird) return;
        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(10f, 18f);
            birds.transform.position = camera.transform.position + new Vector3(offset.x, 4f, offset.y);
            birds.pitch = Random.Range(0.9f, 1.12f);
            birds.Play();
        }
        nextBird = Time.time + Random.Range(9f, 18f);
    }
}
