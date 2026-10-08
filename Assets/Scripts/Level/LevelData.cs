// LevelData.cs — контейнеры для разбора level_corridor_risk_2d.json.
// Все поля public и класс помечен [Serializable] — иначе UnityEngine.JsonUtility их не увидит.
// ВАЖНО: имена полей обязаны совпадать с ключами JSON буква в букву.
using System;

namespace CorridorRisk {

[Serializable] public class NodeDef      { public string id; public float x; public float y; }            // узел графа тропинок
[Serializable] public class EdgeDef      { public string a; public string b; public float length; }       // отрезок тропы
[Serializable] public class RoutesDef    { public string[] A; public string[] B; public string[] C; }     // три тропы
[Serializable] public class WeatherDef   { public string id; public float x, y, rInner, rOuter, intensity; } // участок непогоды
[Serializable] public class ShelterDef   { public string id; public float x, y, radius, density; }        // участок укрытий
[Serializable] public class SupplyDef    { public string id; public float x, y; public string kind; public float amount; } // родник/ягодник
[Serializable] public class ViewpointDef { public string id; public float x, y; }                         // смотровая точка
[Serializable] public class CircleDef    { public string id; public float x, y, radius; }                 // стоянка или приют
[Serializable] public class BoundsDef    { public float xMin, xMax, yMin, yMax; }
[Serializable] public class BinsDef      { public float[] dist; public float[] shelter; public float[] weather; }

[Serializable]
public class LevelFile {
    public string         version;
    public string         name;
    public BoundsDef      worldBounds;
    public float          dMax;
    public NodeDef[]      nodes;
    public EdgeDef[]      edges;
    public RoutesDef      routes;
    public WeatherDef[]   weatherZones;
    public ShelterDef[]   shelterZones;
    public SupplyDef[]    supplyPoints;
    public ViewpointDef[] viewpoints;
    public CircleDef      campZone;
    public CircleDef      hutZone;
    public string[]       spawnNodes;
    public BinsDef        bins;
}

}
