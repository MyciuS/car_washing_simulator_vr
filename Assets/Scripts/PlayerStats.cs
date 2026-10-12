using UnityEngine;

// Holds the player's stats. Food goes down over time,
// and when food is empty, energy starts going down.
// (Later: sleeping will give energy back.)
public class PlayerStats : MonoBehaviour
{
    public float energy = 100f;
    public float food = 100f;
    public int money = 0;

    // How much food is lost per second.
    public float foodLossPerSecond = 0.5f;

    // How much energy is lost per second when starving.
    public float starvingEnergyLossPerSecond = 1f;

    void Update()
    {
        food -= foodLossPerSecond * Time.deltaTime;

        if (food <= 0f)
        {
            food = 0f;
            energy -= starvingEnergyLossPerSecond * Time.deltaTime;
        }

        if (energy < 0f)
        {
            energy = 0f;
        }
    }

    // Call this to give the player money (for example after washing a car).
    public void AddMoney(int amount)
    {
        money += amount;
    }

    // Call this when the player eats something.
    public void Eat(float amount)
    {
        food = Mathf.Min(food + amount, 100f);
    }
}
