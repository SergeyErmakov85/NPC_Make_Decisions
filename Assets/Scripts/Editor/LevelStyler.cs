// LevelStyler.cs — «презентационное» оформление уже построенного уровня
// в мирном сюжете ноутбука v3: путешественник идёт к горному приюту.
//
// LevelBuilder отвечает за геометрию и логику, этот скрипт — только за вид.
// Уровень рисуется как туристическая карта:
//   WeatherZone   → участки непогоды (облако с дождём, сила = intensity);
//   ShelterZone   → лес (деревья, плотность = density);
//   SupplyPoint   → родники (запас воды и еды) / ягодники (силы);
//   Viewpoint     → смотровые точки;  CampZone → стоянка;  HutZone → горный приют.
// Имена объектов и компонентов не меняются: код симуляции их не замечает.
//
// Скрипт идемпотентный: перед применением удаляет всё, что создал раньше,
// поэтому после «Построить уровень из JSON» его можно просто запустить снова.
//
// Меню: CorridorRisk → Оформить уровень для презентации
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CorridorRisk.EditorTools {

public static class LevelStyler {

    const string LevelRoot    = "Level";
    const string DecoRoot     = "Presentation";
    const string DecoPrefix   = "Deco_";
    const string ArtFolder    = "Assets/Art/Presentation";
    const string UnlitMatPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // --- палитра ------------------------------------------------------------------

    static readonly Color Paper      = Hex(0xEEF1E8);   // поле вокруг карты
    static readonly Color Frame      = Hex(0x6E5C47);   // рамка карты
    static readonly Color Ink        = Hex(0x1F2A37);   // основной текст
    static readonly Color Weather    = Hex(0x4C6E91);   // непогода
    static readonly Color Forest     = Hex(0x2E7D46);   // лес
    static readonly Color Spring     = Hex(0x1E8FD6);   // родник
    static readonly Color Berry      = Hex(0xC2185B);   // ягодник
    static readonly Color View       = Hex(0x7E57C2);   // смотровая точка
    static readonly Color Camp       = Hex(0xF57C00);   // стоянка
    static readonly Color Hut        = Hex(0xB5432F);   // приют
    static readonly Color RouteA     = Hex(0x1565C0);   // защищённая тропа
    static readonly Color RouteB     = Hex(0xEF8A17);   // мимо родников
    static readonly Color RouteC     = Hex(0xD62839);   // напрямик

    // порядок отрисовки (sortingOrder)
    const int OPaper = -100, OFrame = -91, OTerrain = -90,
              OForest = -60, OTree = -55, OWeather = -50, OWeatherRing = -49, OCloud = -44,
              OCamp = -40, ORouteCase = -30, ORoute = -29, OWaypoint = -20,
              OPinShadow = 4, OPin = 5, OBadge = 15, OPill = 18, OText = 20;

    static Sprite _disc, _soft, _rrect, _square, _terrain,
                  _tree, _cloud, _drop, _berry, _view, _tent, _hut;
    static Material _unlit;
    static Font _font;
    static Rect? _clip;          // границы карты: всё, что за ними, не рисуем

    [MenuItem("CorridorRisk/Оформить уровень для презентации")]
    public static void Apply() {
        var level = GameObject.Find(LevelRoot);
        if (level == null) {
            EditorUtility.DisplayDialog("CorridorRisk",
                "На сцене нет объекта «Level».\nСначала выполните CorridorRisk → Построить уровень из JSON.", "Ок");
            return;
        }
        var data = LoadLevelData();
        if (data == null) {
            EditorUtility.DisplayDialog("CorridorRisk", "Не найден level_corridor_risk_2d.json.", "Ок");
            return;
        }

        LoadArt(data);
        Cleanup(level);

        var deco = new GameObject(DecoRoot);
        Undo.RegisterCreatedObjectUndo(deco, "Style level");

        var wb = data.worldBounds;
        _clip = Rect.MinMaxRect(wb.xMin, wb.yMin, wb.xMax, wb.yMax);
        BuildBackground(deco, data);
        StyleRoutes(level, deco, data);            // до леса: деревья обходят тропы
        StyleForest(level, data);
        StyleWeather(level);
        StyleCampAndHut(level);
        StyleWaypoints(level);
        StyleViewpoints(level);
        StyleSupplies(level);
        _clip = null;
        BuildTitle(deco, data);
        BuildLegend(deco, data);
        SetupCameraAndPost(deco);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(level.scene);
        Debug.Log("[LevelStyler] оформление «путь к горному приюту» применено");
    }

    // --- фон и рельеф -----------------------------------------------------------

    static void BuildBackground(GameObject deco, LevelFile d) {
        var b = d.worldBounds;
        Vector2 c = new Vector2((b.xMin + b.xMax) / 2f, (b.yMin + b.yMax) / 2f);
        float w = b.xMax - b.xMin, h = b.yMax - b.yMin;

        var paper = AddSprite(deco, "Paper", _square, Paper, OPaper);
        paper.transform.position = c;
        paper.transform.localScale = new Vector3(w + 40f, h + 30f, 1f);

        var frame = AddSprite(deco, "MapFrame", _rrect, Frame, OFrame);
        frame.transform.position = c;
        frame.drawMode = SpriteDrawMode.Sliced;
        frame.size = new Vector2(w + 0.7f, h + 0.7f);

        var terrain = AddSprite(deco, "Terrain", _terrain, Color.white, OTerrain);
        terrain.transform.position = c;

        // маска карты: заливки зон не вылезают на заголовок и легенду
        var mask = new GameObject("MapMask").AddComponent<SpriteMask>();
        mask.transform.SetParent(deco.transform, false);
        mask.transform.position = c;
        mask.sprite = _square;
        mask.transform.localScale = new Vector3(w, h, 1f);
    }

    // высота рельефа в точке (x, y): общий подъём к приюту, гора у приюта и холмы
    static float Height(float x, float y) {
        float hgt = 0.012f * (x + 32f);
        hgt += 0.60f * Gauss(x - 27f, y - 1f, 7.5f);
        hgt += 0.30f * Gauss(x - 6f,  y - 19f, 9f);
        hgt += 0.26f * Gauss(x + 15f, y + 17f, 7f);
        hgt += 0.18f * Gauss(x - 14f, y + 16f, 6f);
        hgt += 0.12f * Gauss(x + 22f, y - 14f, 5f);
        hgt += 0.035f * Mathf.Sin(0.35f * x + 1.3f) * Mathf.Sin(0.42f * y + 0.7f);
        return hgt;
    }

    static float Gauss(float dx, float dy, float s) => Mathf.Exp(-(dx * dx + dy * dy) / (2f * s * s));

    // --- тропы ------------------------------------------------------------------

    static readonly List<Vector3[]> _routes = new List<Vector3[]>();

    static void StyleRoutes(GameObject level, GameObject deco, LevelFile d) {
        _routes.Clear();
        StyleRoute(level, deco, d, "Route_A", RouteA, "A", "A2", "A3");
        StyleRoute(level, deco, d, "Route_B", RouteB, "B", "B3", "B4");
        StyleRoute(level, deco, d, "Route_C", RouteC, "C", "C3", "C4");
    }

    static void StyleRoute(GameObject level, GameObject deco, LevelFile d, string name, Color color,
                           string letter, string badgeFrom, string badgeTo) {
        var t = level.transform.Find("Routes/" + name);
        if (t == null) return;
        var lr = t.GetComponent<LineRenderer>();
        lr.sharedMaterial = _unlit;
        lr.widthMultiplier = 0.45f;
        lr.numCapVertices = lr.numCornerVertices = 6;
        lr.startColor = lr.endColor = color;
        lr.sortingOrder = ORoute;

        var pts = new Vector3[lr.positionCount];
        lr.GetPositions(pts);
        _routes.Add(pts);

        // белая «обводка» тропы, как на туристической карте
        var casing = new GameObject(DecoPrefix + "Casing").AddComponent<LineRenderer>();
        casing.transform.SetParent(t, false);
        casing.useWorldSpace = true;
        casing.positionCount = pts.Length;
        casing.SetPositions(pts);
        casing.sharedMaterial = _unlit;
        casing.widthMultiplier = 0.85f;
        casing.numCapVertices = casing.numCornerVertices = 6;
        casing.startColor = casing.endColor = new Color(1f, 1f, 1f, 0.92f);
        casing.sortingOrder = ORouteCase;

        // значок с буквой тропы — посередине ребра, где нет других объектов
        var a = System.Array.Find(d.nodes, n => n.id == badgeFrom);
        var b = System.Array.Find(d.nodes, n => n.id == badgeTo);
        if (a == null || b == null) return;
        var badge = new GameObject("Badge_" + letter);
        badge.transform.SetParent(deco.transform, false);
        badge.transform.position = new Vector3((a.x + b.x) / 2f, (a.y + b.y) / 2f, 0f);
        Shadow(badge.transform, 1.15f, OBadge - 2);
        Circle(badge.transform, "Ring", _disc, 1.15f, Color.white, OBadge - 1);
        Circle(badge.transform, "Fill", _disc, 0.95f, color, OBadge);
        Label(badge.transform, "Letter", letter, Vector2.zero, 1.2f, Color.white, TextAnchor.MiddleCenter, bold: true);
    }

    // --- лес (зоны укрытий) -----------------------------------------------------

    static void StyleForest(GameObject level, LevelFile d) {
        var obstacles = new List<Vector2>();
        foreach (var p in d.supplyPoints)          obstacles.Add(new Vector2(p.x, p.y));
        foreach (var o in d.viewpoints) obstacles.Add(new Vector2(o.x, o.y));
        obstacles.Add(new Vector2(d.hutZone.x, d.hutZone.y));
        var camp = new Vector2(d.campZone.x, d.campZone.y);
        var placed = new List<Vector2>();

        foreach (var z in level.GetComponentsInChildren<ShelterZone>()) {
            ClearVisuals(z.transform);
            Circle(z.transform, "Visual", _soft, z.radius * 1.15f, WithA(Forest, 0.25f + 0.35f * z.density), OForest);

            // деревья: число пропорционально плотности и площади, раскладка детерминированная
            var rng = new System.Random(StableHash(z.id));
            int target = Mathf.RoundToInt(z.density * z.radius * z.radius * 0.55f);
            Vector2 c = z.transform.position;
            for (int attempt = 0, made = 0; attempt < target * 12 && made < target; attempt++) {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float rr  = z.radius * 0.92f * Mathf.Sqrt((float)rng.NextDouble());
                var p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
                if (_clip != null && !_clip.Value.Contains(p)) continue;
                if (NearRoute(p, 1.0f) || Near(p, obstacles, 2.0f) || Near(p, placed, 1.05f)) continue;
                if (Vector2.Distance(p, camp) < 3.2f) continue;                 // у стоянки — поляна
                placed.Add(p);
                made++;
                float size = 0.55f + 0.3f * (float)rng.NextDouble();
                var tree = AddSprite(z.gameObject, DecoPrefix + "Tree", _tree, Color.white, OTree);
                tree.transform.position = p;
                tree.transform.localScale = new Vector3(size, size, 1f);
                tree.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }
    }

    // --- непогода (зоны угрозы) -----------------------------------------------------

    static void StyleWeather(GameObject level) {
        foreach (var z in level.GetComponentsInChildren<WeatherZone>()) {
            ClearVisuals(z.transform);
            Circle(z.transform, "Visual", _soft, z.rOuter, WithA(Weather, 0.22f + 0.50f * z.intensity), OWeather);
            Ring(z.transform, DecoPrefix + "Core", z.rInner, 0.09f, WithA(Weather, 0.45f + 0.4f * z.intensity), OWeatherRing, dashed: true);
            // облако: чем сильнее непогода, тем оно больше и темнее
            float s = 1.4f + 1.6f * z.intensity;
            float shade = Mathf.Lerp(1f, 0.72f, z.intensity);
            var cloud = Circle(z.transform, DecoPrefix + "Cloud", _cloud, s, new Color(shade, shade, Mathf.Min(1f, shade + 0.03f), 0.95f), OCloud);
            cloud.transform.localPosition = CloudOffset(level, z, s);
        }
    }

    // облако ставим в первую свободную точку внутри зоны: не на значках и не поверх троп
    static readonly Vector2[] CloudSpots = {
        new Vector2(0f, 1.2f), new Vector2(0f, -2.5f), new Vector2(-3f, 1.5f), new Vector2(3f, 1.5f),
        new Vector2(-3f, -2f), new Vector2(3f, -2f), new Vector2(0f, 3.5f), new Vector2(-4.5f, 0f), new Vector2(4.5f, 0f),
    };

    static Vector3 CloudOffset(GameObject level, WeatherZone z, float size) {
        var pins = new List<Vector2>();
        foreach (var p in level.GetComponentsInChildren<SupplyPoint>())          pins.Add(p.transform.position);
        foreach (var p in level.GetComponentsInChildren<Viewpoint>()) {
            pins.Add(p.transform.position);
            if (OverlapsPickup(p)) pins.Add((Vector2)p.transform.position + new Vector2(-1.6f, 1.3f));
        }
        Vector2 c = z.transform.position;
        foreach (var o in CloudSpots) {
            var p = c + o * Mathf.Min(1f, z.rInner / 4f);
            if (_clip != null && !_clip.Value.Contains(p + Vector2.up * size)) continue;
            if (Near(p, pins, size + 1.4f)) continue;
            if (NearRoute(p + Vector2.up * 0.3f * size, 0.45f * size)) continue;      // корпус облака не на тропе
            return o * Mathf.Min(1f, z.rInner / 4f);
        }
        return CloudSpots[0];
    }

    // --- стоянка и приют --------------------------------------------------------

    static void StyleCampAndHut(GameObject level) {
        var camp = level.GetComponentInChildren<CampZone>();
        if (camp != null) {
            ClearVisuals(camp.transform);
            Circle(camp.transform, "Visual", _soft, camp.radius + 0.8f, WithA(Camp, 0.32f), OCamp);
            Ring(camp.transform, DecoPrefix + "Ring", camp.radius, 0.12f, WithA(Camp, 0.8f), OCamp + 1, dashed: true);
            Pin(camp.transform, _tent, 1.5f, Vector2.zero);
            Caption(camp.transform, "СТОЯНКА", new Vector2(0f, -2.6f), Camp);
        }
        var goal = level.GetComponentInChildren<HutZone>();
        if (goal != null) {
            ClearVisuals(goal.transform);
            Circle(goal.transform, DecoPrefix + "Glow", _soft, goal.radius * 2.6f, WithA(Hex(0xFFD54F), 0.75f), OCamp);
            Ring(goal.transform, DecoPrefix + "Ring", goal.radius, 0.12f, Hut, OCamp + 1);
            Pin(goal.transform, _hut, 2.0f, Vector2.zero);
            Caption(goal.transform, "ГОРНЫЙ ПРИЮТ", new Vector2(0f, -3.2f), Hut);
        }
    }

    // --- узлы, смотровые точки, родники и ягодники ---------------------------------

    static void StyleWaypoints(GameObject level) {
        foreach (var w in level.GetComponentsInChildren<WaypointMarker>()) {
            ClearVisuals(w.transform);
            if (w.id == "HUT") continue;
            bool fork = w.id == "FORK";
            Circle(w.transform, DecoPrefix + "Outline", _disc, fork ? 0.6f : 0.3f, Ink, OWaypoint - 1);
            Circle(w.transform, "Visual", _disc, fork ? 0.45f : 0.2f, Color.white, OWaypoint);
        }
    }

    static void StyleViewpoints(GameObject level) {
        foreach (var p in level.GetComponentsInChildren<Viewpoint>()) {
            ClearVisuals(p.transform);
            Ring(p.transform, DecoPrefix + "Ring", 1.9f, 0.12f, WithA(View, 0.8f), OPin - 2, dashed: true);
            // если в той же точке лежит ягодник или родник, значок смотровой сдвигаем в сторону
            Pin(p.transform, _view, 1.15f, OverlapsPickup(p) ? new Vector2(-1.6f, 1.3f) : Vector2.zero);
        }
    }

    static void StyleSupplies(GameObject level) {
        foreach (var p in level.GetComponentsInChildren<SupplyPoint>()) {
            ClearVisuals(p.transform);
            bool berry = p.kind == SupplyKind.Berries;
            // SupplyPoint.Consume() гасит все дочерние SpriteRenderer — значок с тенью тоже исчезнет
            Pin(p.transform, berry ? _berry : _drop, 1.05f, Vector2.zero);
        }
    }

    static bool OverlapsPickup(Viewpoint post) {
        foreach (var p in Object.FindObjectsByType<SupplyPoint>(FindObjectsSortMode.None))
            if (Vector2.Distance(p.transform.position, post.transform.position) < 0.5f) return true;
        return false;
    }

    // значок-«пин» на белом круге с мягкой тенью
    static void Pin(Transform parent, Sprite icon, float radius, Vector2 offset) {
        var holder = new GameObject(DecoPrefix + "Pin").transform;
        holder.SetParent(parent, false);
        holder.localPosition = offset;
        Shadow(holder, radius, OPinShadow);
        Circle(holder, "Icon", icon, radius, Color.white, OPin);
    }

    static void Shadow(Transform parent, float radius, int order) {
        var sh = Circle(parent, DecoPrefix + "Shadow", _soft, radius * 1.35f, new Color(0.1f, 0.12f, 0.1f, 0.35f), order);
        sh.transform.localPosition = new Vector3(0.12f, -0.18f, 0f);
    }

    // --- подписи ----------------------------------------------------------------

    static void BuildTitle(GameObject deco, LevelFile d) {
        var b = d.worldBounds;
        float y = b.yMax + 1.6f;
        Label(deco.transform, "Title", "Путь к горному приюту", new Vector2(b.xMin, y), 1.3f, Ink, TextAnchor.MiddleLeft, bold: true);

        float x = b.xMax;
        x = RouteKey(deco, x, y, RouteC, "C", "напрямик по склону");
        x = RouteKey(deco, x, y, RouteB, "B", "мимо родников");
            RouteKey(deco, x, y, RouteA, "A", "защищённая тропа");
    }

    // рисует ключ тропы справа налево и возвращает новую правую границу
    static float RouteKey(GameObject deco, float right, float y, Color color, string letter, string text) {
        const float h = 0.8f;
        Label(deco.transform, "Key_" + letter, text, new Vector2(right, y), h, Ink, TextAnchor.MiddleRight);
        float cx = right - TextWidth(text, h, false) - 0.9f;
        var dot = new GameObject("KeyBadge_" + letter);
        dot.transform.SetParent(deco.transform, false);
        dot.transform.position = new Vector3(cx, y, 0f);
        Circle(dot.transform, "Fill", _disc, 0.6f, color, OBadge);
        Label(dot.transform, "Letter", letter, Vector2.zero, 0.75f, Color.white, TextAnchor.MiddleCenter, bold: true);
        return cx - 1.8f;
    }

    static void BuildLegend(GameObject deco, LevelFile d) {
        var root = new GameObject("Legend");
        root.transform.SetParent(deco.transform, false);
        float y = d.worldBounds.yMin - 1.6f, x = d.worldBounds.xMin + 0.6f;

        x = LegendItem(root, x, y, "непогода (темнее — сильнее)", t => {
            Circle(t, "Fill", _soft, 0.75f, WithA(Weather, 0.7f), OBadge);
            Circle(t, "Cloud", _cloud, 0.6f, Color.white, OBadge + 1);
        });
        x = LegendItem(root, x, y, "лес — укрытие", t => Circle(t, "Tree", _tree, 0.6f, Color.white, OBadge));
        x = LegendItem(root, x, y, "родник — вода", t => Circle(t, "Pin", _drop, 0.55f, Color.white, OBadge));
        x = LegendItem(root, x, y, "ягодник — силы", t => Circle(t, "Pin", _berry, 0.55f, Color.white, OBadge));
        x = LegendItem(root, x, y, "смотровая точка", t => Circle(t, "Pin", _view, 0.55f, Color.white, OBadge));
        x = LegendItem(root, x, y, "стоянка", t => Circle(t, "Pin", _tent, 0.55f, Color.white, OBadge));
            LegendItem(root, x, y, "приют", t => Circle(t, "Pin", _hut, 0.55f, Color.white, OBadge));
    }

    static float LegendItem(GameObject root, float x, float y, string text, System.Action<Transform> icon) {
        var item = new GameObject("Item");
        item.transform.SetParent(root.transform, false);
        item.transform.position = new Vector3(x, y, 0f);
        icon(item.transform);
        Label(item.transform, "Text", text, new Vector2(0.9f, 0f), 0.75f, Ink, TextAnchor.MiddleLeft);
        return x + 0.9f + TextWidth(text, 0.75f, false) + 1.9f;
    }

    // подпись на светлой «плашке», чтобы читалась поверх рельефа
    static void Caption(Transform parent, string text, Vector2 localPos, Color color) {
        const float h = 0.85f;
        var holder = new GameObject(DecoPrefix + "Caption").transform;
        holder.SetParent(parent, false);
        holder.localPosition = localPos;
        var pill = AddSprite(holder.gameObject, "Pill", _rrect, new Color(1f, 1f, 1f, 0.9f), OPill);
        pill.drawMode = SpriteDrawMode.Sliced;
        pill.size = new Vector2(TextWidth(text, h, true) + 1.0f, h + 0.45f);
        Label(holder, "Text", text, new Vector2(0f, 0.02f), h, color, TextAnchor.MiddleCenter, bold: true);
    }

    // --- камера и постобработка -------------------------------------------------

    static void SetupCameraAndPost(GameObject deco) {
        var cam = Camera.main;
        if (cam != null) {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Paper;
            var urp = cam.GetUniversalAdditionalCameraData();
            if (urp != null) urp.renderPostProcessing = true;
        }

        string profilePath = ArtFolder + "/PresentationVolume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null) {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        // на светлой карте bloom «пересвечивает» всё подряд — оставляем только мягкую виньетку
        if (profile.Has<Bloom>()) profile.Remove<Bloom>();
        if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.16f);
        vignette.smoothness.Override(0.6f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var vol = new GameObject("PostProcessVolume").AddComponent<Volume>();
        vol.transform.SetParent(deco.transform, false);
        vol.isGlobal = true;
        vol.sharedProfile = profile;
    }

    // --- утилиты: очистка ---------------------------------------------------------

    static void Cleanup(GameObject level) {
        var old = GameObject.Find(DecoRoot);
        if (old != null) Undo.DestroyObjectImmediate(old);
        foreach (var t in level.GetComponentsInChildren<Transform>(true)) {
            if (t == null) continue;
            for (int i = t.childCount - 1; i >= 0; i--) {
                var c = t.GetChild(i);
                if (c.name.StartsWith(DecoPrefix)) Undo.DestroyObjectImmediate(c.gameObject);
            }
        }
    }

    static void ClearVisuals(Transform t) {
        for (int i = t.childCount - 1; i >= 0; i--) {
            var c = t.GetChild(i);
            if (c.name == "Visual" || c.name.StartsWith(DecoPrefix)) Undo.DestroyObjectImmediate(c.gameObject);
        }
    }

    // --- утилиты: геометрия -------------------------------------------------------

    static bool NearRoute(Vector2 p, float dist) {
        foreach (var r in _routes)
            for (int i = 0; i + 1 < r.Length; i++)
                if (DistToSegment(p, r[i], r[i + 1]) < dist) return true;
        return false;
    }

    static bool Near(Vector2 p, List<Vector2> pts, float dist) {
        foreach (var q in pts) if ((p - q).sqrMagnitude < dist * dist) return true;
        return false;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b) {
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }

    // string.GetHashCode не обязан совпадать между запусками — считаем свой
    static int StableHash(string s) {
        unchecked {
            int h = 23;
            foreach (char ch in s) h = h * 31 + ch;
            return h;
        }
    }

    // --- утилиты: примитивы -------------------------------------------------------

    static SpriteRenderer AddSprite(GameObject parent, string name, Sprite sprite, Color color, int order) {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        sr.sharedMaterial = _unlit;
        return sr;
    }

    // спрайты кругов и значков имеют радиус 1 world unit, поэтому масштаб = радиус
    static SpriteRenderer Circle(Transform parent, string name, Sprite sprite, float radius, Color color, int order) {
        var sr = AddSprite(parent.gameObject, name, sprite, color, order);
        sr.transform.localScale = new Vector3(radius, radius, 1f);
        if (_clip != null) sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        return sr;
    }

    // кольцо из дуг постоянной толщины; dashed — пунктир. Дуги за границей карты пропускаются.
    static void Ring(Transform parent, string name, float radius, float width, Color color, int order, bool dashed = false) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        int arcs = dashed ? Mathf.Max(12, Mathf.RoundToInt(radius * 4f)) : 48;
        float step = Mathf.PI * 2f / arcs;
        float fill = dashed ? 0.6f : 1.02f;                     // доля шага, занятая штрихом
        Vector3 center = parent.position;
        for (int k = 0; k < arcs; k++) {
            float mid = (k + fill / 2f) * step;
            var mp = center + new Vector3(Mathf.Cos(mid), Mathf.Sin(mid), 0f) * radius;
            if (_clip != null && !_clip.Value.Contains(mp)) continue;
            var seg = new GameObject("Arc").AddComponent<LineRenderer>();
            seg.transform.SetParent(go.transform, false);
            seg.useWorldSpace = false;
            seg.sharedMaterial = _unlit;
            seg.widthMultiplier = width;
            seg.sortingOrder = order;
            seg.startColor = seg.endColor = color;
            const int m = 6;
            seg.positionCount = m;
            for (int i = 0; i < m; i++) {
                float a = k * step + step * fill * i / (m - 1);
                seg.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }
        }
    }

    static void Label(Transform parent, string name, string text, Vector2 localPos, float height,
                      Color color, TextAnchor anchor, bool bold = false) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        // подпись не должна масштабироваться вместе с кругом-родителем
        var ls = parent.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(ls.x, 1e-4f), 1f / Mathf.Max(ls.y, 1e-4f), 1f);
        var tm = go.AddComponent<TextMesh>();
        tm.font = _font;
        tm.text = text;
        tm.fontSize = 96;
        tm.characterSize = height / (tm.fontSize * 0.1f);
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = _font.material;
        mr.sortingOrder = OText;
    }

    // ширина строки в world units при высоте height (как её отрисует Label)
    static float TextWidth(string text, float height, bool bold) {
        const int fs = 96;
        var style = bold ? FontStyle.Bold : FontStyle.Normal;
        _font.RequestCharactersInTexture(text, fs, style);
        float w = 0f;
        foreach (char ch in text)
            if (_font.GetCharacterInfo(ch, out CharacterInfo ci, fs, style)) w += ci.advance;
        return w * height / fs;
    }

    // --- генерация текстур --------------------------------------------------------

    static void LoadArt(LevelFile d) {
        _unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMatPath);
        _font  = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(ArtFolder))   AssetDatabase.CreateFolder("Assets/Art", "Presentation");
        // текстуры прошлой, «неоновой» версии оформления больше не нужны
        foreach (var stale in new[] { "grid", "background" })
            AssetDatabase.DeleteAsset($"{ArtFolder}/{stale}.png");

        _disc   = MakeSprite("disc",   256, 256, 128f, Vector4.zero, (x, y) => Paint(Color.white, Disc(x, y, 0f, 0f, 1f, 256)));
        _soft   = MakeSprite("soft",   256, 256, 128f, Vector4.zero, (x, y) => {
            float r = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
            return new Color(1f, 1f, 1f, Mathf.Pow(1f - r, 1.4f) * 0.8f + Mathf.SmoothStep(0.5f, 0f, r) * 0.2f);
        });
        _square = MakeSprite("square", 4, 4, 4f, Vector4.zero, (x, y) => Color.white);
        _rrect  = MakeSprite("rrect",  64, 64, 64f, new Vector4(24, 24, 24, 24), (x, y) => {
            float px = Mathf.Abs(x) * 32f, py = Mathf.Abs(y) * 32f;           // радиус скругления 24 px из 64
            float dx = Mathf.Max(px - 8f, 0f), dy = Mathf.Max(py - 8f, 0f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(24f - Mathf.Sqrt(dx * dx + dy * dy)));
        });

        _tree  = MakeSprite("tree",  128, 128, 64f,  Vector4.zero, TreeIcon);
        _cloud = MakeSprite("cloud", 256, 256, 128f, Vector4.zero, CloudIcon);
        _drop  = MakeSprite("pin_spring", 192, 192, 96f, Vector4.zero, (x, y) => PinIcon(x, y, Spring, DropIcon));
        _berry = MakeSprite("pin_berry",  192, 192, 96f, Vector4.zero, (x, y) => PinIcon(x, y, Berry, BerryIcon));
        _view  = MakeSprite("pin_view",   192, 192, 96f, Vector4.zero, (x, y) => PinIcon(x, y, View, ViewIcon));
        _tent  = MakeSprite("pin_camp",   192, 192, 96f, Vector4.zero, (x, y) => PinIcon(x, y, Camp, TentIcon));
        _hut   = MakeSprite("pin_hut",    192, 192, 96f, Vector4.zero, (x, y) => PinIcon(x, y, Hut, HutIcon));

        _terrain = MakeTerrain(d);
    }

    // карта высот: гипсометрическая раскраска + горизонтали (каждая пятая — утолщённая)
    static Sprite MakeTerrain(LevelFile d) {
        var b = d.worldBounds;
        float w = b.xMax - b.xMin, h = b.yMax - b.yMin;
        const float ppu = 20f;
        int tw = Mathf.RoundToInt(w * ppu), th = Mathf.RoundToInt(h * ppu);

        float lo = float.MaxValue, hi = float.MinValue;
        for (int j = 0; j <= 72; j++)
            for (int i = 0; i <= 128; i++) {
                float v = Height(b.xMin + w * i / 128f, b.yMin + h * j / 72f);
                lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v);
            }
        const int levels = 16;
        float step = (hi - lo) / levels;
        Color c0 = Hex(0xCFE7B2), c1 = Hex(0xE2E8B0), c2 = Hex(0xEADBB4), c3 = Hex(0xF1E9DD), c4 = Hex(0xFAF8F3);
        Color contour = Hex(0x8C7356);

        return MakeSprite("terrain", tw, th, ppu, Vector4.zero, (u, v) => {
            float x = b.xMin + (u + 1f) * 0.5f * w, y = b.yMin + (v + 1f) * 0.5f * h;
            float hgt = Height(x, y);
            float t = Mathf.Clamp01((hgt - lo) / (hi - lo));
            Color col = t < 0.35f ? Color.Lerp(c0, c1, t / 0.35f)
                      : t < 0.65f ? Color.Lerp(c1, c2, (t - 0.35f) / 0.30f)
                      : t < 0.88f ? Color.Lerp(c2, c3, (t - 0.65f) / 0.23f)
                      :             Color.Lerp(c3, c4, (t - 0.88f) / 0.12f);
            // расстояние до ближайшей горизонтали в world units ≈ |frac| · step / |∇h|
            const float e = 0.05f;
            float gx = (Height(x + e, y) - Height(x - e, y)) / (2f * e);
            float gy = (Height(x, y + e) - Height(x, y - e)) / (2f * e);
            float grad = Mathf.Max(Mathf.Sqrt(gx * gx + gy * gy), 1e-4f);
            float q = (hgt - lo) / step;
            float distLine = Mathf.Abs(q - Mathf.Round(q)) * step / grad;
            bool index = Mathf.RoundToInt(q) % 5 == 0;
            float half = index ? 0.065f : 0.035f;
            float a = Mathf.Clamp01((half - distLine) * ppu + 0.5f) * (index ? 0.55f : 0.35f);
            return Color.Lerp(col, contour, a);
        });
    }

    // --- значки -----------------------------------------------------------------

    // белый круг с цветной каймой; внутри — иконка, нарисованная в своих координатах [-1, 1]
    static Color PinIcon(float x, float y, Color accent, System.Func<float, float, Color> icon) {
        float r = Mathf.Sqrt(x * x + y * y);
        const float px = 2f / 192f;
        float outer = Mathf.Clamp01((1f - r) / px);
        if (outer <= 0f) return Color.clear;
        float ring = Mathf.Clamp01((r - 0.82f) / px);
        Color c = Color.Lerp(Color.white, accent, ring);
        var ic = icon(x / 0.66f, y / 0.66f);
        c = Color.Lerp(c, new Color(ic.r, ic.g, ic.b, 1f), ic.a * (1f - ring));
        c.a = outer;
        return c;
    }

    static Color DropIcon(float x, float y) {
        // капля: круг снизу + сужение вверх
        const float px = 2f / 120f;
        float r = Mathf.Sqrt(x * x + (y + 0.3f) * (y + 0.3f));
        float inCircle = (0.6f - r) / px;
        float half = 0.6f * Mathf.Clamp01((0.95f - y) / 1.25f);
        float inCone = y > -0.3f ? (half - Mathf.Abs(x)) / px : -1f;
        float a = Mathf.Clamp01(Mathf.Max(inCircle, Mathf.Min(inCone, (0.95f - y) / px)));
        Color c = Color.Lerp(Spring, Hex(0x7FD0FF), Disc(x, y, -0.22f, -0.38f, 0.17f, 120));
        c.a = a;
        return c;
    }

    static Color BerryIcon(float x, float y) {
        Color c = Color.clear;
        c = Over(c, Hex(0x43A047), Ellipse(x, y, 0.28f, 0.62f, 0.32f, 0.16f, -0.5f));     // листок
        c = Over(c, Berry,             Disc(x, y, -0.33f, -0.15f, 0.42f, 120));
        c = Over(c, Hex(0x8E1E5A),     Disc(x, y,  0.33f, -0.15f, 0.42f, 120));
        c = Over(c, Hex(0xD81B60),     Disc(x, y,  0.0f,   0.25f, 0.42f, 120));
        c = Over(c, new Color(1f, 1f, 1f, 0.85f), Disc(x, y, -0.12f, 0.38f, 0.1f, 120));
        c = Over(c, new Color(1f, 1f, 1f, 0.85f), Disc(x, y, -0.45f, -0.02f, 0.09f, 120));
        return c;
    }

    static Color ViewIcon(float x, float y) {
        Color c = Color.clear;
        c = Over(c, View,          Triangle(x, y, new Vector2(-0.95f, -0.6f), new Vector2(0.95f, -0.6f), new Vector2(0.05f, 0.75f)));
        c = Over(c, Color.white,   Triangle(x, y, new Vector2(-0.22f, 0.4f), new Vector2(0.32f, 0.4f), new Vector2(0.05f, 0.75f)));
        c = Over(c, Hex(0x5E35B1), Triangle(x, y, new Vector2(-1.0f, -0.6f), new Vector2(0.1f, -0.6f), new Vector2(-0.48f, 0.15f)));
        return c;
    }

    static Color TentIcon(float x, float y) {
        Color c = Color.clear;
        c = Over(c, Camp,          Triangle(x, y, new Vector2(-0.95f, -0.6f), new Vector2(0.95f, -0.6f), new Vector2(0f, 0.8f)));
        c = Over(c, Hex(0x5D3A1A), Triangle(x, y, new Vector2(-0.3f, -0.6f), new Vector2(0.3f, -0.6f), new Vector2(0f, 0.15f)));
        c = Over(c, Hex(0x5D3A1A), Rect01(x, y, -1.0f, -0.72f, 1.0f, -0.6f));
        return c;
    }

    static Color HutIcon(float x, float y) {
        Color c = Color.clear;
        c = Over(c, Hex(0x8D5A3B), Rect01(x, y, -0.7f, -0.75f, 0.7f, 0.1f));                 // сруб
        c = Over(c, Hut,           Triangle(x, y, new Vector2(-0.95f, 0.05f), new Vector2(0.95f, 0.05f), new Vector2(0f, 0.85f)));
        c = Over(c, Hex(0x4E2F1E), Rect01(x, y, -0.18f, -0.75f, 0.18f, -0.2f));              // дверь
        c = Over(c, Hex(0xFFD54F), Rect01(x, y, 0.3f, -0.45f, 0.58f, -0.18f));               // светится окно
        return c;
    }

    static Color TreeIcon(float x, float y) {
        Color c = Color.clear;
        c = Over(c, new Color(0.1f, 0.2f, 0.1f, 0.3f), Disc(x, y, 0.12f, -0.12f, 0.85f, 128)); // тень
        c = Over(c, Hex(0x2E7D32), Disc(x, y, 0f, 0f, 0.82f, 128));
        c = Over(c, Hex(0x43A047), Disc(x, y, -0.18f, 0.18f, 0.55f, 128));
        c = Over(c, Hex(0x66BB6A), Disc(x, y, -0.3f, 0.32f, 0.25f, 128));
        return c;
    }

    static Color CloudIcon(float x, float y) {
        Color c = Color.clear;
        // капли дождя под облаком
        for (int k = -2; k <= 2; k++) {
            float cx = k * 0.3f + 0.05f;
            c = Over(c, Hex(0x3F87C9), Segment(x, y, new Vector2(cx, -0.25f), new Vector2(cx - 0.12f, -0.75f), 0.045f, 256));
        }
        float body = Mathf.Max(Mathf.Max(Disc(x, y, -0.45f, 0.2f, 0.36f, 256), Disc(x, y, 0.05f, 0.38f, 0.46f, 256)),
                     Mathf.Max(Disc(x, y, 0.5f, 0.18f, 0.34f, 256), Rect01(x, y, -0.8f, -0.12f, 0.82f, 0.2f)));
        float bodyIn = Mathf.Max(Mathf.Max(Disc(x, y, -0.45f, 0.2f, 0.29f, 256), Disc(x, y, 0.05f, 0.38f, 0.39f, 256)),
                       Mathf.Max(Disc(x, y, 0.5f, 0.18f, 0.27f, 256), Rect01(x, y, -0.73f, -0.05f, 0.75f, 0.2f)));
        c = Over(c, Hex(0x5A7896), body);           // контур
        c = Over(c, Hex(0xF4F7FA), bodyIn);         // заливка
        return c;
    }

    // --- примитивы растеризации: возвращают покрытие [0, 1] с АА-кромкой ----------

    static float Disc(float x, float y, float cx, float cy, float r, int res) {
        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        return Mathf.Clamp01((r - d) * res * 0.5f + 0.5f);
    }

    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry, float angle) {
        float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
        float dx = x - cx, dy = y - cy;
        float u = (dx * ca + dy * sa) / rx, v = (-dx * sa + dy * ca) / ry;
        return Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * 20f);
    }

    static float Rect01(float x, float y, float x0, float y0, float x1, float y1) {
        const float k = 60f;
        return Mathf.Clamp01(Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(y - y0, y1 - y)) * k + 0.5f);
    }

    static float Triangle(float x, float y, Vector2 a, Vector2 b, Vector2 c) {
        var p = new Vector2(x, y);
        float s = Mathf.Sign(Edge(c, a, b));
        float e = Mathf.Min(s * Edge(p, a, b), Mathf.Min(s * Edge(p, b, c), s * Edge(p, c, a)));
        return Mathf.Clamp01(e * 60f + 0.5f);
    }

    static float Edge(Vector2 p, Vector2 a, Vector2 b) {
        var ab = b - a;
        return (ab.x * (p.y - a.y) - ab.y * (p.x - a.x)) / ab.magnitude;     // знаковое расстояние до прямой
    }

    static float Segment(float x, float y, Vector2 a, Vector2 b, float halfWidth, int res) =>
        Mathf.Clamp01((halfWidth - DistToSegment(new Vector2(x, y), a, b)) * res * 0.5f + 0.5f);

    static Color Paint(Color c, float coverage) => new Color(c.r, c.g, c.b, c.a * coverage);

    // наложение цвета over с покрытием coverage поверх under (обычный alpha blending)
    static Color Over(Color under, Color over, float coverage) {
        float a = over.a * coverage;
        float outA = a + under.a * (1f - a);
        if (outA < 1e-5f) return Color.clear;
        var rgb = (new Vector3(over.r, over.g, over.b) * a +
                   new Vector3(under.r, under.g, under.b) * under.a * (1f - a)) / outA;
        return new Color(rgb.x, rgb.y, rgb.z, outA);
    }

    // f(x, y) получает координаты пикселя в [-1, 1] по обеим осям
    static Sprite MakeSprite(string name, int width, int height, float ppu, Vector4 border,
                             System.Func<float, float, Color> f) {
        string path = $"{ArtFolder}/{name}.png";
        var px = new Color32[width * height];
        for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
                px[j * width + i] = f((i + 0.5f) / width * 2f - 1f, (j + 0.5f) / height * 2f - 1f);
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.maxTextureSize = 2048;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.spriteBorder = border;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // --- прочее -----------------------------------------------------------------

    static LevelFile LoadLevelData() {
        var guids = AssetDatabase.FindAssets("level_corridor_risk_2d t:TextAsset");
        if (guids.Length == 0) return null;
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        return json != null ? JsonUtility.FromJson<LevelFile>(json.text) : null;
    }

    static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));

    static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}

}
