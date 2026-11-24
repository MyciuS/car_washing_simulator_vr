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

    // Pays for something. Returns false (and takes nothing) if there isn't enough money.
    public bool TrySpend(int amount)
    {
        if (money < amount)
            return false;
        money -= amount;
        return true;
    }

    // Call this when the player eats something.
    public void Eat(float amount)
    {
        food = Mathf.Min(food + amount, 100f);
    }

    // Call this when the player drinks coffee or an energy drink.
    public void AddEnergy(float amount)
    {
        energy = Mathf.Min(energy + amount, 100f);
    }
}
