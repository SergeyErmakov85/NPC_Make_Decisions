// SupplyPoint.cs — место, где можно пополнить запасы или подкрепиться:
//   Spring  — родник: пополняет запас воды и еды (supplies);
//   Berries — ягодник: перекус восстанавливает силы (energy).
// На время похода «исчерпывается» и восстанавливается при сбросе эпизода.
using UnityEngine;

namespace CorridorRisk {

public enum SupplyKind { Spring, Berries }

[RequireComponent(typeof(CircleCollider2D))]
public class SupplyPoint : MonoBehaviour {
    public string id = "SP?";
    public SupplyKind kind = SupplyKind.Spring;
    [Range(0f, 1f)] public float amount = 0.30f;   // доля от максимума

    public bool IsActive { get; private set; } = true;

    void Reset() {                                  // вызывается при добавлении компонента в редакторе
        var c = GetComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 1.5f;
    }

    public void ResetSupply() {
        IsActive = true;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.enabled = true;
    }

    public void Consume() {
        IsActive = false;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.enabled = false;
    }

    void OnDrawGizmosSelected() {
        Gizmos.color = kind == SupplyKind.Spring ? new Color(0.2f, 0.7f, 1f) : new Color(0.75f, 0.2f, 0.55f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.6f);
    }
}

}
