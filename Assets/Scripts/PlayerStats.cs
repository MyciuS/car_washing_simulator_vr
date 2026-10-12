using UnityEngine;

// Holds the player's stats. Food goes down over time,
// and when food is empty, health starts going down.
public class PlayerStats : MonoBehaviour
{
    public float health = 100f;
    public float food = 100f;
    public int money = 0;

    // How much food is lost per second.
    public float foodLossPerSecond = 0.5f;

    // How much health is lost per second when starving.
    public float starvingDamagePerSecond = 1f;

    void Update()
    {
        food -= foodLossPerSecond * Time.deltaTime;

        if (food <= 0f)
        {
            food = 0f;
            health -= starvingDamagePerSecond * Time.deltaTime;
        }

        if (health < 0f)
        {
            health = 0f;
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
