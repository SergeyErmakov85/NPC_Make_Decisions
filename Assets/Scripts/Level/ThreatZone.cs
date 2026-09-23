// ThreatZone.cs — зона угрозы. Внутри rInner интенсивность максимальна,
// от rInner до rOuter линейно спадает до нуля.
using UnityEngine;

namespace CorridorRisk {

public class ThreatZone : MonoBehaviour {
    public string id = "T?";
    public float rInner = 5f;
    public float rOuter = 10f;
    [Range(0f, 1f)] public float intensity = 0.5f;

    public float Evaluate(Vector2 p) {
        float d = Vector2.Distance(p, (Vector2)transform.position);
        if (d <= rInner) return intensity;
        if (d >= rOuter) return 0f;
        return intensity * (1f - (d - rInner) / (rOuter - rInner));
    }

    void OnDrawGizmos() {
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, rInner);
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.20f);
        Gizmos.DrawWireSphere(transform.position, rOuter);
    }
}

}
