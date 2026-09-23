// LevelData.cs — контейнеры для разбора level_corridor_risk_2d.json.
// Все поля public и класс помечен [Serializable] — иначе UnityEngine.JsonUtility их не увидит.
// ВАЖНО: имена полей обязаны совпадать с ключами JSON буква в букву.
using System;

namespace CorridorRisk {

[Serializable] public class NodeDef      { public string id; public float x; public float y; }
[Serializable] public class EdgeDef      { public string a; public string b; public float length; }
[Serializable] public class RoutesDef    { public string[] A; public string[] B; public string[] C; }
[Serializable] public class ThreatDef    { public string id; public float x, y, rInner, rOuter, intensity; }
[Serializable] public class CoverDef     { public string id; public float x, y, radius, density; }
[Serializable] public class PickupDef    { public string id; public float x, y; public string kind; public float amount; }
[Serializable] public class PostDef      { public string id; public float x, y; }
[Serializable] public class CircleDef    { public string id; public float x, y, radius; }
[Serializable] public class BoundsDef    { public float xMin, xMax, yMin, yMax; }
[Serializable] public class BinsDef      { public float[] dist; public float[] cover; public float[] threat; }

[Serializable]
public class LevelFile {
    public string      version;
    public string      name;
    public BoundsDef   worldBounds;
    public float       dMax;
    public NodeDef[]   nodes;
    public EdgeDef[]   edges;
    public RoutesDef   routes;
    public ThreatDef[] threatZones;
    public CoverDef[]  coverZones;
    public PickupDef[] pickups;
    public PostDef[]   observationPosts;
    public CircleDef   safeZone;
    public CircleDef   goalZone;
    public string[]    spawnNodes;
    public BinsDef     bins;
}

}
