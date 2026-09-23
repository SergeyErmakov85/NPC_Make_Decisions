// Pickup.cs — ресурс (ammo) или аптечка (med).
// Гаснет на время эпизода и восстанавливается при сбросе.
using UnityEngine;

namespace CorridorRisk {

public enum PickupKind { Ammo, Med }

[RequireComponent(typeof(CircleCollider2D))]
public class Pickup : MonoBehaviour {
    public string id = "P?";
    public PickupKind kind = PickupKind.Ammo;
    [Range(0f, 1f)] public float amount = 0.30f;   // доля от максимума

    public bool IsActive { get; private set; } = true;

    void Reset() {                                  // вызывается при добавлении компонента в редакторе
        var c = GetComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 1.5f;
    }

    public void ResetPickup() {
        IsActive = true;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.enabled = true;
    }

    public void Consume() {
        IsActive = false;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.enabled = false;
    }

    void OnDrawGizmos() {
        Gizmos.color = kind == PickupKind.Ammo ? Color.yellow : Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.6f);
    }
}

}
