using UnityEngine;

// Describes what a spray (or later a brush) does to a DirtySurface.
// Make one asset per method: Assets > Create > Car Wash > Cleaning Tool.
//   - Water: removes loose mud and rinses soap off. On soaked spots it also removes grime.
//   - Soap: leaves soap on the car. After it has soaked for a while, water cleans better there.
[CreateAssetMenu(menuName = "Car Wash/Cleaning Tool")]
public class CleaningTool : ScriptableObject
{
    // Size of the spot around each hit, in meters.
    public float radius = 0.08f;

    [Header("Cleaning (per hit, at the center of the spot)")]
    // How much loose mud one hit removes (0 = none, 1 = all).
    public float looseDirtRemoval = 0.005f;
    // How much stubborn grime one hit removes.
    public float grimeRemoval = 0f;

    [Header("Cleaning where soap has soaked in (dirt is loosened)")]
    public float soakedLooseDirtRemoval = 0.05f;
    public float soakedGrimeRemoval = 0f;

    [Header("Soap")]
    // How much soap one hit leaves on the car (soap gun only).
    public float soapAdded = 0f;
    // How long the soap must sit before it loosens the dirt, in seconds.
    public float soapSoakTime = 30f;
    // How much soap one hit washes off (water).
    public float soapRinse = 0f;
}
