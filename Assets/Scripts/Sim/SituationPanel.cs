// SituationPanel.cs — панель «Поиграть ситуацией» прямо в окне игры (Шаг 10).
//
// Что умеет:
//   • пауза / продолжить (кнопка или пробел), «Новый поход»;
//   • текущий поход: вариант действий, ключ ситуации, пять переменных состояния, время;
//   • ползунки Demo Energy / Dist / Weather / Supplies / Shelter — те же поля, что
//     в инспекторе Episode Manager; следующий поход начнётся из заданной ситуации;
//   • эталонные ситуации из методички — одной кнопкой;
//   • редактор ключа ситуации из пяти цифр (например 2|2|1|1|1): на паузе выберите
//     цифры, посмотрите, что ответит таблица политики, и либо
//       – «Решить по ключу» — путешественник принимает решение так, будто он в этой
//         ситуации (ключ держится, пока его не сбросить), либо
//       – «Поход из ключа» — ползунки ставятся в середины бинов и поход начинается заново.
//
// Компонент добавляется сам (на объект с EpisodeManager), если его нет на сцене.
// Нарисован на IMGUI: не нужны ни Canvas, ни шрифты, ни префабы.
using System.Collections.Generic;
using UnityEngine;

namespace CorridorRisk {

public class SituationPanel : MonoBehaviour {

    public enum Side { Right, Left }

    [Header("Ссылки (если пусто — найдутся сами)")]
    [SerializeField] EpisodeManager   manager;
    [SerializeField] AgentState       state;
    [SerializeField] StrategyExecutor executor;
    [SerializeField] DecisionPolicy   policy;

    [Header("Вид")]
    public Side  side      = Side.Right;
    public float width     = 340f;
    [Tooltip("0 — масштаб по высоте экрана (эталон 1080 px).")]
    public float uiScale   = 0f;
    public bool  collapsed = false;
    [Tooltip("Сдвинуть камеру, чтобы панель не закрывала карту (приют справа, стоянка слева).")]
    public bool  fitCamera = true;

    static readonly string[] VarLabels = { "Energy", "Dist", "Weather", "Supplies", "Shelter" };
    static readonly string[] VarHints  = { "силы", "до приюта", "непогода", "запасы", "укрытие" };

    // Эталонные ситуации методички: energy, weather, supplies (dist и shelter не трогаем).
    static readonly (float e, float w, float s, string hint)[] Presets = {
        (0.9f, 0.2f, 0.8f, "напрямик по открытому склону (центр)"),
        (0.3f, 0.8f, 0.7f, "переждать в укрытии / вернуться"),
        (0.8f, 0.3f, 0.2f, "к родникам по нижней тропе"),
        (0.7f, 0.6f, 0.7f, "лесной тропой (верхняя)"),
    };

    int[]   _digits = { 1, 1, 1, 1, 1 };
    bool    _holdKey;
    float   _savedTimeScale = 1f;
    Vector2 _scroll;

    // история исходов
    readonly List<string> _history = new List<string>();
    int _success, _exhausted, _timeout;

    Camera _cam;
    Rect   _camRect0;
    float  _camSize0;
    bool   _camSaved;

    GUIStyle _box, _h1, _h2, _small, _key, _btn, _btnOn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate() {
        if (FindFirstObjectByType<SituationPanel>() != null) return;
        var m = FindFirstObjectByType<EpisodeManager>();
        if (m != null) m.gameObject.AddComponent<SituationPanel>();
    }

    void Awake() {
        if (manager  == null) manager  = FindFirstObjectByType<EpisodeManager>();
        if (state    == null) state    = FindFirstObjectByType<AgentState>();
        if (executor == null) executor = FindFirstObjectByType<StrategyExecutor>();
        if (policy   == null) policy   = FindFirstObjectByType<DecisionPolicy>();
    }

    bool Paused => Time.timeScale == 0f;

    void SetPaused(bool pause) {
        if (pause == Paused) return;
        if (pause) {
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale
                            : (manager != null ? manager.BaseTimeScale : 1f);
            Time.timeScale = 0f;
            TakeCurrentKey();                      // на паузе редактор ключа начинается с текущей ситуации
        } else {
            Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
        }
    }

    void OnEnable()  { if (manager != null) manager.EpisodeFinished += OnEpisodeFinished; }
    void OnDisable() { if (manager != null) manager.EpisodeFinished -= OnEpisodeFinished; }

    void OnEpisodeFinished(int episode, string result) {
        if (result == "SUCCESS") _success++; else if (result == "EXHAUSTED") _exhausted++; else _timeout++;
        string strat = executor != null ? executor.CurrentStrategy : "?";
        _history.Insert(0, $"#{episode}  {result}  ({strat})");
        if (_history.Count > 6) _history.RemoveAt(_history.Count - 1);
    }

    // --- ключ ситуации ---------------------------------------------------------

    string EditedKey() => string.Join("|", _digits);

    void TakeCurrentKey() {
        if (policy == null || state == null) return;
        string k = policy.StateKey(state.Energy01, state.Dist01, state.Weather01,
                                   state.Supplies01, state.Shelter01);
        var parts = k.Split('|');
        for (int i = 0; i < _digits.Length && i < parts.Length; i++)
            int.TryParse(parts[i], out _digits[i]);
    }

    void ApplyKeyToSliders() {
        var vars = policy.StateVars;
        float[] v = new float[5];
        for (int i = 0; i < 5; i++) v[i] = policy.BinCenter(vars[i], _digits[i]);
        manager.demoEnergy = v[0]; manager.demoDist = v[1]; manager.demoWeather = v[2];
        manager.demoSupplies = v[3]; manager.demoShelter = v[4];
    }

    // --- отрисовка -------------------------------------------------------------

    void OnGUI() {
        if (manager == null) return;
        InitStyles();

        // горячая клавиша: пробел — пауза
        var ev = Event.current;
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Space) { SetPaused(!Paused); ev.Use(); }

        float scale = Scale();
        var old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float sw = Screen.width / scale, sh = Screen.height / scale;

        float w = collapsed ? 150f : width;
        float x = side == Side.Right ? sw - w - 10f : 10f;
        var rect = new Rect(x, 10f, w, collapsed ? 70f : sh - 20f);
        if (!collapsed && fitCamera) {          // глухая полоса под панелью: камера её не рисует
            float sx = side == Side.Right ? sw - w - 20f : 0f;
            GUI.DrawTexture(new Rect(sx, 0f, w + 20f, sh), Texture2D.whiteTexture, ScaleMode.StretchToFill,
                            false, 0, new Color(0.08f, 0.09f, 0.12f, 1f), 0, 0);
        }

        GUILayout.BeginArea(rect, _box);
        DrawHeader();
        if (!collapsed) {
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawCurrent();
            DrawKeyEditor();
            DrawSliders();
            DrawPresets();
            DrawHistory();
            GUILayout.EndScrollView();
        }
        GUILayout.EndArea();
        GUI.matrix = old;
    }

    float Scale() => uiScale > 0f ? uiScale : Mathf.Max(0.6f, Screen.height / 1080f);

    // Камера рисует только часть экрана рядом с панелью; орторазмер увеличиваем так,
    // чтобы ширина видимой области мира не изменилась и карта влезла целиком.
    void LateUpdate() {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        if (!_camSaved) { _camRect0 = _cam.rect; _camSize0 = _cam.orthographicSize; _camSaved = true; }
        if (!fitCamera || collapsed) { RestoreCamera(); return; }

        float frac = Mathf.Clamp((width + 20f) * Scale() / Mathf.Max(1, Screen.width), 0f, 0.6f);
        _cam.rect = side == Side.Right ? new Rect(0f, 0f, 1f - frac, 1f) : new Rect(frac, 0f, 1f - frac, 1f);
        if (_cam.orthographic) _cam.orthographicSize = _camSize0 / (1f - frac);
    }

    void RestoreCamera() {
        if (!_camSaved || _cam == null) return;
        _cam.rect = _camRect0;
        _cam.orthographicSize = _camSize0;
    }

    void OnDestroy() { RestoreCamera(); }

    void DrawHeader() {
        GUILayout.BeginHorizontal();
        GUILayout.Label(collapsed ? "Ситуация" : "Поиграть ситуацией", _h1);
        GUILayout.FlexibleSpace();
        if (!collapsed && GUILayout.Button(side == Side.Right ? "<" : ">", _btn, GUILayout.Width(26)))
            side = side == Side.Right ? Side.Left : Side.Right;
        if (GUILayout.Button(collapsed ? "+" : "–", _btn, GUILayout.Width(26))) collapsed = !collapsed;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Paused ? "Продолжить (пробел)" : "Пауза (пробел)", Paused ? _btnOn : _btn)) SetPaused(!Paused);
        if (!collapsed && GUILayout.Button("Новый поход", _btn)) {
            manager.RestartEpisode();
            if (Paused) TakeCurrentKey();
        }
        GUILayout.EndHorizontal();
    }

    void DrawCurrent() {
        Section("Сейчас");
        if (manager.mode != RunMode.Demo) {
            GUILayout.Label($"Режим {manager.mode}: ползунки не действуют.", _small);
            if (GUILayout.Button("Переключить в Demo", _btn)) manager.mode = RunMode.Demo;
        }
        string strat = executor != null ? executor.CurrentStrategy : "?";
        string key   = executor != null ? executor.LastStateKey   : "";
        var c = StrategyLabel.ColorOf(strat);
        var prev = GUI.contentColor;
        GUI.contentColor = c;
        GUILayout.Label(strat, _h2);
        GUI.contentColor = prev;
        GUILayout.Label($"ключ  {key}" + (_holdKey ? "   (задан вручную)" : ""), _key);
        GUILayout.Label($"Поход #{manager.EpisodeIndex}   {manager.Elapsed:0.0} / {manager.MaxTime:0} с   " +
                        $"смен решения: {(executor != null ? executor.StrategyChanges : 0)}", _small);
        if (state != null) {
            Bar("Energy",   state.Energy01);
            Bar("Dist",     state.Dist01);
            Bar("Weather",  state.Weather01);
            Bar("Supplies", state.Supplies01);
            Bar("Shelter",  state.Shelter01);
        }
    }

    void DrawKeyEditor() {
        if (policy == null || policy.StateVars == null) return;
        Section("Ключ ситуации (5 цифр)");
        GUILayout.Label(Paused ? "Выберите цифры и примените." : "Поставьте на паузу, чтобы спокойно подобрать ключ.", _small);

        var vars = policy.StateVars;
        for (int i = 0; i < 5 && i < vars.Length; i++) {
            int n = policy.BinCount(vars[i]);
            var opts = new string[n];
            for (int b = 0; b < n; b++) opts[b] = b.ToString();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{VarLabels[i]}", _small, GUILayout.Width(70));
            _digits[i] = GUILayout.Toolbar(Mathf.Clamp(_digits[i], 0, n - 1), opts, GUILayout.Width(120));
            GUILayout.Label(VarHints[i], _small);
            GUILayout.EndHorizontal();
        }

        string k = EditedKey();
        GUILayout.Label($"ключ  {k}", _key);
        if (policy.TryGetEntry(k, out var e)) {
            var prev = GUI.contentColor;
            GUI.contentColor = StrategyLabel.ColorOf(e.strategy);
            GUILayout.Label($"политика: {e.strategy}" + (e.ambiguous ? " ?" : ""), _h2);
            GUI.contentColor = prev;
            GUILayout.Label($"второе место: {e.runnerUp}, отрыв {e.margin:0.000}", _small);
            if (e.scores != null) {
                var names = policy.Strategies;
                for (int s = 0; s < e.scores.Length && s < names.Length; s++)
                    Bar(names[s], e.scores[s], StrategyLabel.ColorOf(names[s]));
            }
        } else {
            GUILayout.Label("такого ключа нет в таблице: сработает fallback", _small);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Взять текущий", _btn)) TakeCurrentKey();
        if (GUILayout.Button("Решить по ключу", _btn) && executor != null) {
            _holdKey = true;
            executor.SetOverrideKey(k);
            executor.DecideNow(true);
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Поход из ключа", _btn)) {
            ReleaseKey();
            ApplyKeyToSliders();
            manager.mode = RunMode.Demo;
            manager.RestartEpisode();
        }
        GUI.enabled = _holdKey;
        if (GUILayout.Button("Сбросить ключ", _btn)) { ReleaseKey(); executor?.DecideNow(true); }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        if (_holdKey && executor != null && executor.OverrideKey != k)
            GUILayout.Label($"держится ключ {executor.OverrideKey}; нажмите «Решить по ключу», чтобы заменить", _small);
    }

    void ReleaseKey() {
        _holdKey = false;
        if (executor != null) executor.SetOverrideKey("");
    }

    void DrawSliders() {
        Section("Demo: старт следующего похода");
        manager.demoEnergy   = Slider("Energy",   manager.demoEnergy);
        manager.demoDist     = Slider("Dist",     manager.demoDist);
        manager.demoWeather  = Slider("Weather",  manager.demoWeather);
        manager.demoSupplies = Slider("Supplies", manager.demoSupplies);
        manager.demoShelter  = Slider("Shelter",  manager.demoShelter);
        GUILayout.Label("Ключ старта: " + (policy != null
            ? policy.StateKey(manager.demoEnergy, manager.demoDist, manager.demoWeather,
                              manager.demoSupplies, manager.demoShelter) : "?"), _small);
    }

    void DrawPresets() {
        Section("Эталонные ситуации (E / W / S)");
        foreach (var p in Presets) {
            if (GUILayout.Button($"{p.e:0.0} / {p.w:0.0} / {p.s:0.0}  -  {p.hint}", _btn)) {
                ReleaseKey();
                manager.mode = RunMode.Demo;
                manager.demoEnergy = p.e; manager.demoWeather = p.w; manager.demoSupplies = p.s;
                manager.RestartEpisode();
            }
        }
    }

    void DrawHistory() {
        Section("Исходы походов");
        GUILayout.Label($"SUCCESS {_success}   EXHAUSTED {_exhausted}   TIMEOUT {_timeout}", _small);
        foreach (var h in _history) GUILayout.Label(h, _small);
    }

    // --- мелочи ----------------------------------------------------------------

    void Section(string title) {
        GUILayout.Space(8);
        GUILayout.Label(title, _h2);
    }

    float Slider(string name, float v) {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, _small, GUILayout.Width(80));
        v = GUILayout.HorizontalSlider(v, 0f, 1f, GUILayout.ExpandWidth(true));
        GUILayout.Label(v.ToString("0.00"), _small, GUILayout.Width(36));
        GUILayout.EndHorizontal();
        return Mathf.Round(v * 100f) / 100f;
    }

    void Bar(string name, float v) => Bar(name, v, new Color(0.75f, 0.8f, 0.9f));

    void Bar(string name, float v, Color color) {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, _small, GUILayout.Width(80));
        var r = GUILayoutUtility.GetRect(100f, 12f, GUILayout.ExpandWidth(true));
        r.y += 3f;
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0,
                        new Color(1f, 1f, 1f, 0.12f), 0, 0);
        var f = new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height);
        GUI.DrawTexture(f, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, color, 0, 0);
        GUILayout.Label(v.ToString("0.00"), _small, GUILayout.Width(36));
        GUILayout.EndHorizontal();
    }

    void InitStyles() {
        if (_box != null) return;
        var bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.12f, 0.88f));
        bg.Apply();
        _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
        _box.normal.background = bg;
        _h1    = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        _h2    = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        _key   = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        _key.normal.textColor = new Color(1f, 0.92f, 0.6f);
        _btn   = new GUIStyle(GUI.skin.button) { fontSize = 12, wordWrap = true };
        _btnOn = new GUIStyle(_btn);
        _btnOn.normal.textColor = new Color(1f, 0.85f, 0.3f);
    }
}

}
