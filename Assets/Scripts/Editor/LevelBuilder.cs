// LevelBuilder.cs — построение сцены из level_corridor_risk_2d.json.
//
// Расставлять 25 узлов и 17 зон руками бессмысленно: при первой же правке
// геометрии всё придётся переделывать. Этот скрипт читает тот же JSON,
// который порождает level.py, поэтому расчётная модель и сцена
// гарантированно не разойдутся.
//
// Меню: CorridorRisk → Построить уровень из JSON
using UnityEditor;
using UnityEngine;

namespace CorridorRisk.EditorTools {

public static class LevelBuilder {

    const string RootName = "Level";

    [MenuItem("CorridorRisk/Построить уровень из JSON")]
    public static void Build() {
        var json = Selection.activeObject as TextAsset;
        if (json == null) {
            var guids = AssetDatabase.FindAssets("level_corridor_risk_2d t:TextAsset");
            if (guids.Length > 0)
                json = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (json == null) {
            EditorUtility.DisplayDialog("CorridorRisk",
                "Не найден level_corridor_risk_2d.json.\n" +
                "Положите его в Assets/ и выделите в Project, затем повторите.", "Ок");
            return;
        }

        var data = JsonUtility.FromJson<LevelFile>(json.text);
        if (data == null || data.nodes == null || data.nodes.Length == 0) {
            EditorUtility.DisplayDialog("CorridorRisk", "JSON не разобрался. Проверьте файл.", "Ок");
            return;
        }

        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build level");

        var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        BuildWaypoints(root, data);
        BuildThreatZones(root, data, circle);
        BuildCoverZones(root, data, circle);
        BuildPickups(root, data, circle);
        BuildPosts(root, data, circle);
        BuildSpecialZones(root, data, circle);
        BuildRouteLines(root, data);
        BuildBounds(root, data);
        SetupCamera(data);

        Selection.activeGameObject = root;
        Debug.Log($"[LevelBuilder] уровень «{data.name}» построен: узлов {data.nodes.Length}, " +
                  $"зон угрозы {data.threatZones.Length}, зон укрытий {data.coverZones.Length}");
    }

    // --- части уровня ----------------------------------------------------------

    static void BuildWaypoints(GameObject root, LevelFile d) {
        var parent = Child(root, "Waypoints");
        foreach (var n in d.nodes) {
            var go = new GameObject(n.id);
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(n.x, n.y, 0f);
            go.AddComponent<WaypointMarker>().id = n.id;
        }
    }

    static void BuildThreatZones(GameObject root, LevelFile d, Sprite circle) {
        var parent = Child(root, "ThreatZones");
        foreach (var z in d.threatZones) {
            var go = new GameObject(z.id);
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(z.x, z.y, 0f);
            var c = go.AddComponent<ThreatZone>();
            c.id = z.id; c.rInner = z.rInner; c.rOuter = z.rOuter; c.intensity = z.intensity;
            AddCircleVisual(go, circle, z.rOuter, new Color(0.86f, 0.15f, 0.15f, 0.10f), -1);
            AddCircleVisual(go, circle, z.rInner, new Color(0.86f, 0.15f, 0.15f, 0.16f), -1);
        }
    }

    static void BuildCoverZones(GameObject root, LevelFile d, Sprite circle) {
        var parent = Child(root, "CoverZones");
        foreach (var z in d.coverZones) {
            var go = new GameObject(z.id);
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(z.x, z.y, 0f);
            var c = go.AddComponent<CoverZone>();
            c.id = z.id; c.radius = z.radius; c.density = z.density;
            AddCircleVisual(go, circle, z.radius,
                            new Color(0.09f, 0.64f, 0.29f, 0.08f + 0.14f * z.density), -2);
        }
    }

    static void BuildPickups(GameObject root, LevelFile d, Sprite circle) {
        var parent = Child(root, "Pickups");
        foreach (var p in d.pickups) {
            var go = new GameObject(p.id);
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(p.x, p.y, 0f);
            var c = go.AddComponent<Pickup>();
            c.id = p.id;
            c.kind = p.kind == "med" ? PickupKind.Med : PickupKind.Ammo;
            c.amount = p.amount;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true; col.radius = 1.5f;
            AddCircleVisual(go, circle, 1.2f,
                            c.kind == PickupKind.Ammo
                                ? new Color(0.92f, 0.70f, 0.09f, 0.95f)
                                : new Color(0.94f, 0.27f, 0.27f, 0.95f), 2);
        }
    }

    static void BuildPosts(GameObject root, LevelFile d, Sprite circle) {
        var parent = Child(root, "ObservationPosts");
        foreach (var o in d.observationPosts) {
            var go = new GameObject(o.id);
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(o.x, o.y, 0f);
            go.AddComponent<ObservationPost>().id = o.id;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true; col.radius = 1.5f;
            AddCircleVisual(go, circle, 1.1f, new Color(0.55f, 0.36f, 0.96f, 0.95f), 2);
        }
    }

    static void BuildSpecialZones(GameObject root, LevelFile d, Sprite circle) {
        var safe = new GameObject("SafeZone");
        safe.transform.SetParent(root.transform);
        safe.transform.position = new Vector3(d.safeZone.x, d.safeZone.y, 0f);
        safe.AddComponent<SafeZone>().radius = d.safeZone.radius;
        AddCircleVisual(safe, circle, d.safeZone.radius, new Color(0.23f, 0.51f, 0.96f, 0.12f), -2);

        var goal = new GameObject("GoalZone");
        goal.transform.SetParent(root.transform);
        goal.transform.position = new Vector3(d.goalZone.x, d.goalZone.y, 0f);
        goal.AddComponent<GoalZone>().radius = d.goalZone.radius;
        AddCircleVisual(goal, circle, d.goalZone.radius, new Color(0.13f, 0.77f, 0.37f, 0.55f), 0);
    }

    static void BuildRouteLines(GameObject root, LevelFile d) {
        var parent = Child(root, "Routes");
        DrawRoute(parent, "Route_A", d.routes.A, d, new Color(0.06f, 0.65f, 0.91f));
        DrawRoute(parent, "Route_B", d.routes.B, d, new Color(0.96f, 0.62f, 0.07f));
        DrawRoute(parent, "Route_C", d.routes.C, d, new Color(0.94f, 0.27f, 0.27f));
    }

    static void DrawRoute(GameObject parent, string name, string[] ids, LevelFile d, Color color) {
        if (ids == null || ids.Length < 2) return;
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = ids.Length;
        lr.widthMultiplier = 0.35f;
        lr.numCapVertices = 4;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = new Color(color.r, color.g, color.b, 0.55f);
        lr.sortingOrder = -3;
        for (int i = 0; i < ids.Length; i++) {
            var n = System.Array.Find(d.nodes, x => x.id == ids[i]);
            if (n != null) lr.SetPosition(i, new Vector3(n.x, n.y, 0f));
        }
    }

    static void BuildBounds(GameObject root, LevelFile d) {
        var parent = Child(root, "Bounds");
        var b = d.worldBounds;
        float w = b.xMax - b.xMin, h = b.yMax - b.yMin, t = 2f;
        MakeWall(parent, "Top",    new Vector2((b.xMin + b.xMax) / 2f, b.yMax + t / 2f), new Vector2(w + 2 * t, t));
        MakeWall(parent, "Bottom", new Vector2((b.xMin + b.xMax) / 2f, b.yMin - t / 2f), new Vector2(w + 2 * t, t));
        MakeWall(parent, "Left",   new Vector2(b.xMin - t / 2f, (b.yMin + b.yMax) / 2f), new Vector2(t, h));
        MakeWall(parent, "Right",  new Vector2(b.xMax + t / 2f, (b.yMin + b.yMax) / 2f), new Vector2(t, h));
    }

    static void MakeWall(GameObject parent, string name, Vector2 pos, Vector2 size) {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
    }

    static void SetupCamera(LevelFile d) {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        cam.orthographicSize = 21f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
    }

    // --- утилиты ---------------------------------------------------------------

    static GameObject Child(GameObject parent, string name) {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        return go;
    }

    static void AddCircleVisual(GameObject parent, Sprite sprite, float radius, Color color, int order) {
        if (sprite == null) return;
        var go = new GameObject("Visual");
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = Vector3.zero;
        float spriteRadius = sprite.bounds.extents.x;          // в мировых единицах при scale = 1
        float k = spriteRadius > 0.0001f ? radius / spriteRadius : 1f;
        go.transform.localScale = new Vector3(k, k, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
    }
}

}
