using UnityEngine;

// Земляная стенка лишь выглядит сплошной: её коллайдер — триггер.
// При касании игроком открываем проход и показываем спрятанный предмет.
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class SecretPassage : MonoBehaviour
{
    [Tooltip("Спрайт предмета за стеной; его коллайдер остаётся активным")]
    public SpriteRenderer hiddenTreasure;

    SpriteRenderer wall;
    bool revealed;
    public bool Revealed { get { return revealed; } }

    void Awake()
    {
        wall = GetComponent<SpriteRenderer>();
        if (hiddenTreasure != null)
            hiddenTreasure.enabled = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (revealed || other.GetComponent<PlayerController>() == null)
            return;

        revealed = true;
        wall.enabled = false;
        if (hiddenTreasure != null)
            hiddenTreasure.enabled = true;
    }
}
