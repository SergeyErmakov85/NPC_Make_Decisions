// LevelRegistry.cs — единая точка доступа к объектам уровня.
// Собирается один раз, чтобы не вызывать FindObjectsByType каждый кадр.
using System.Collections.Generic;
using UnityEngine;

namespace CorridorRisk {

public static class LevelRegistry {
    public static ThreatZone[]      ThreatZones { get; private set; }
    public static CoverZone[]       CoverZones  { get; private set; }
    public static Pickup[]          Pickups     { get; private set; }
    public static ObservationPost[] Posts       { get; private set; }
    public static SafeZone          Safe        { get; private set; }
    public static GoalZone          Goal        { get; private set; }

    static bool _built;

    public static void EnsureBuilt() { if (!_built) Rebuild(); }

    public static void Rebuild() {
        ThreatZones = Object.FindObjectsByType<ThreatZone>(FindObjectsSortMode.None);
        CoverZones  = Object.FindObjectsByType<CoverZone>(FindObjectsSortMode.None);
        Pickups     = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None);
        Posts       = Object.FindObjectsByType<ObservationPost>(FindObjectsSortMode.None);
        Safe        = Object.FindFirstObjectByType<SafeZone>();
        Goal        = Object.FindFirstObjectByType<GoalZone>();
        _built = true;
        Debug.Log($"[LevelRegistry] зон угрозы {ThreatZones.Length}, укрытий {CoverZones.Length}, " +
                  $"предметов {Pickups.Length}, постов {Posts.Length}");
    }

    /// Сырая угроза без масштаба и фона: max по зонам, 0 в безопасной зоне.
    public static float RawThreat(Vector2 p) {
        EnsureBuilt();
        if (Safe != null && Safe.Contains(p)) return 0f;
        float m = 0f;
        for (int i = 0; i < ThreatZones.Length; i++) m = Mathf.Max(m, ThreatZones[i].Evaluate(p));
        return m;
    }

    public static float Cover(Vector2 p) {
        EnsureBuilt();
        float m = 0f;
        for (int i = 0; i < CoverZones.Length; i++) m = Mathf.Max(m, CoverZones[i].Evaluate(p));
        return m;
    }

    public static void ResetAll() {
        EnsureBuilt();
        foreach (var p in Pickups) p.ResetPickup();
        foreach (var o in Posts)   o.ResetPost();
    }
}

}
