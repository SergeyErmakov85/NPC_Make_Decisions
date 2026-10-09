// Zones.cs — стоянка (лагерь у костра) и горный приют. Оба — просто круги.
//   CampZone — место старта: непогоды здесь нет, силы восстанавливаются;
//   HutZone  — горный приют, цель похода.
using UnityEngine;

namespace CorridorRisk {

public class CampZone : MonoBehaviour {
    public float radius = 6f;
    public float energyRegenPerSecond = 3f;         // сколько сил восстанавливает отдых у костра
    public bool Contains(Vector2 p) => Vector2.Distance(p, (Vector2)transform.position) <= radius;

    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(0.95f, 0.6f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

public class HutZone : MonoBehaviour {
    public float radius = 2.5f;
    public bool Contains(Vector2 p) => Vector2.Distance(p, (Vector2)transform.position) <= radius;

    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(0.15f, 0.85f, 0.35f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

}
