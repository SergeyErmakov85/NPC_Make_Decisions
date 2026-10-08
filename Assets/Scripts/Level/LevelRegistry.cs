// LevelRegistry.cs — единая точка доступа к объектам уровня.
// Собирается один раз, чтобы не вызывать FindObjectsByType каждый кадр.
using System.Collections.Generic;
using UnityEngine;

namespace CorridorRisk {

public static class LevelRegistry {
    public static WeatherZone[] WeatherZones { get; private set; }
    public static ShelterZone[] ShelterZones { get; private set; }
    public static SupplyPoint[] SupplyPoints { get; private set; }
    public static Viewpoint[]   Viewpoints   { get; private set; }
    public static CampZone      Camp         { get; private set; }
    public static HutZone       Hut          { get; private set; }

    static bool _built;

    public static void EnsureBuilt() { if (!_built) Rebuild(); }

    public static void Rebuild() {
        WeatherZones = Object.FindObjectsByType<WeatherZone>(FindObjectsSortMode.None);
        ShelterZones = Object.FindObjectsByType<ShelterZone>(FindObjectsSortMode.None);
        SupplyPoints = Object.FindObjectsByType<SupplyPoint>(FindObjectsSortMode.None);
        Viewpoints   = Object.FindObjectsByType<Viewpoint>(FindObjectsSortMode.None);
        Camp         = Object.FindFirstObjectByType<CampZone>();
        Hut          = Object.FindFirstObjectByType<HutZone>();
        _built = true;
        Debug.Log($"[LevelRegistry] участков непогоды {WeatherZones.Length}, укрытий {ShelterZones.Length}, " +
                  $"родников и ягодников {SupplyPoints.Length}, смотровых точек {Viewpoints.Length}");
    }

    /// «Сырая» сила непогоды без масштаба и фона: максимум по участкам, 0 на стоянке.
    public static float RawWeather(Vector2 p) {
        EnsureBuilt();
        if (Camp != null && Camp.Contains(p)) return 0f;
        float m = 0f;
        for (int i = 0; i < WeatherZones.Length; i++) m = Mathf.Max(m, WeatherZones[i].Evaluate(p));
        return m;
    }

    /// Доступность укрытий в точке: максимум по участкам укрытий.
    public static float Shelter(Vector2 p) {
        EnsureBuilt();
        float m = 0f;
        for (int i = 0; i < ShelterZones.Length; i++) m = Mathf.Max(m, ShelterZones[i].Evaluate(p));
        return m;
    }

    public static void ResetAll() {
        EnsureBuilt();
        foreach (var p in SupplyPoints) p.ResetSupply();
        foreach (var o in Viewpoints)   o.ResetViewpoint();
    }
}

}
