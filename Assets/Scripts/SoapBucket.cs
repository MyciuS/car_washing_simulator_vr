using UnityEngine;

// A bucket of soapy water. Dip the sponge or the wheel brush (RubCleaner) into the water to
// load it with soap and rinse the dirt out of it.
// The water surface stays level and sloshes when the bucket is moved around.
// Put this on the bucket, next to its Rigidbody and XR Grab Interactable.
public class SoapBucket : MonoBehaviour
{
    // A trigger collider filling the water. Tools dipped into it get soap.
    public Collider waterZone;

    // The flat water surface (with the foam on it). Its pivot is in the middle of the water.
    public Transform waterSurface;

    // Splash and bubbles when a tool is dipped in (a child particle system).
    public ParticleSystem splash;

    [Header("Sloshing")]
    // How much the water leans when the bucket speeds up or slows down.
    public float sloshAmount = 1.5f;
    // How hard the water swings back, and how quickly the swinging dies down.
    public float sloshSpring = 60f;
    public float sloshDamping = 5f;

    Vector3 lastPosition;
    Vector3 lastVelocity;
    // How far the water surface leans sideways, and how fast that lean is changing.
    Vector3 lean;
    Vector3 leanSpeed;

    void Start()
    {
        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        Vector3 velocity = (transform.position - lastPosition) / deltaTime;
        Vector3 acceleration = (velocity - lastVelocity) / deltaTime;
        lastPosition = transform.position;
        lastVelocity = velocity;

        // Water in a bucket that speeds up piles up at the back: its surface faces "up + acceleration / gravity".
        Vector3 wantedLean = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(acceleration, Vector3.up) / 9.81f * sloshAmount, 0.5f);
        // A spring pulls the lean towards that, so the water swings past it and settles down.
        leanSpeed += ((wantedLean - lean) * sloshSpring - leanSpeed * sloshDamping) * deltaTime;
        lean = Vector3.ClampMagnitude(lean + leanSpeed * deltaTime, 0.3f);

        // Level (plus the lean) no matter how the bucket is tilted; the foam turns with the bucket.
        Quaternion bucketTurn = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        waterSurface.rotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up + lean).normalized) * bucketTurn;
    }

    // Called by a tool that was just dipped in. Splashes at the water surface above that point.
    public void Splash(Vector3 where)
    {
        if (splash == null)
            return;
        splash.transform.position = new Vector3(where.x, waterSurface.position.y, where.z);
        splash.Play();
    }
}
