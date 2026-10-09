// WeatherZone.cs — участок непогоды: продуваемый перевал, осыпь, открытый гребень.
// Внутри rInner непогода сильнее всего, от rInner до rOuter она линейно
// ослабевает до нуля.
using UnityEngine;

namespace CorridorRisk {

public class WeatherZone : MonoBehaviour {
    public string id = "W?";
    public float rInner = 5f;
    public float rOuter = 10f;
    [Range(0f, 1f)] public float intensity = 0.5f;   // сила непогоды в центре участка

    public float Evaluate(Vector2 p) {
        float d = Vector2.Distance(p, (Vector2)transform.position);
        if (d <= rInner) return intensity;
        if (d >= rOuter) return 0f;
        return intensity * (1f - (d - rInner) / (rOuter - rInner));
    }

    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(0.25f, 0.45f, 0.85f, 0.55f);   // сине-серые «тучи»
        Gizmos.DrawWireSphere(transform.position, rInner);
        Gizmos.color = new Color(0.25f, 0.45f, 0.85f, 0.20f);
        Gizmos.DrawWireSphere(transform.position, rOuter);
    }
}

}
