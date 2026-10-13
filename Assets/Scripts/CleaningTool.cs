using UnityEngine;

// Describes how well something cleans a DirtySurface.
// Make one asset per cleaning method: Assets > Create > Car Wash > Cleaning Tool.
// Water only removes loose mud. Later, soap or a brush can be new assets
// with a higher "Grime Removal" so they also remove the stubborn grime.
[CreateAssetMenu(menuName = "Car Wash/Cleaning Tool")]
public class CleaningTool : ScriptableObject
{
    // How much loose mud one hit removes at the center of the spot (0 = none, 1 = all).
    public float looseDirtRemoval = 0.005f;

    // How much stubborn grime one hit removes at the center of the spot.
    public float grimeRemoval = 0f;

    // Size of the cleaned spot around each hit, in meters.
    public float radius = 0.08f;
}
