// SunZone.cs — солнечная поляна: тихое место с хорошей погодой.
// Внутри поляны непогода стихает (до strength в центре), а путешественник
// восстанавливает силы, как у костра на стоянке. В центральной части (core)
// действие полное, от края ядра до radius линейно ослабевает до нуля.
using UnityEngine;

namespace CorridorRisk {

public class SunZone : MonoBehaviour {
    public string id = "SUN?";
    public float radius = 4f;
    [Range(0f, 1f)] public float core = 0.6f;                // доля радиуса с полным действием
    [Range(0f, 1f)] public float strength = 0.85f;           // насколько стихает непогода в центре
    public float energyRegenPerSecond = 5f;                  // восстановление сил в центре

    /// Сила действия поляны в точке: 1 в ядре, 0 за радиусом.
    public float Evaluate(Vector2 p) {
        float d = Vector2.Distance(p, (Vector2)transform.position);
        float r0 = radius * core;
        if (d <= r0) return 1f;
        if (d >= radius) return 0f;
        return 1f - (d - r0) / Mathf.Max(1e-4f, radius - r0);
    }

    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius * core);
    }
}

}
