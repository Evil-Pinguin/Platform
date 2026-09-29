using UnityEngine;

// Однократный подбор: триггер не останавливает игрока, а после касания
// кристалл исчезает. Счётчик доступен другим системам (например, HUD).
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class CollectibleCrystal : MonoBehaviour
{
    [Tooltip("Если указан, предмет можно взять только после открытия прохода")]
    public SecretPassage passage;

    public static int CollectedCount { get; private set; }
    bool collected;

    void OnTriggerEnter2D(Collider2D other) { TryCollect(other); }
    // Если игрок уже стоял в триггере, когда стена открылась, подобрать
    // предмет можно без обязательного выхода и повторного входа.
    void OnTriggerStay2D(Collider2D other) { TryCollect(other); }

    void TryCollect(Collider2D other)
    {
        if (collected || (passage != null && !passage.Revealed))
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
            return;

        collected = true;
        CollectedCount++;
        if (player.fx != null)
            player.fx.Ability();
        Debug.Log("Кристалл найден! Всего: " + CollectedCount);
        Destroy(gameObject);
    }
}
