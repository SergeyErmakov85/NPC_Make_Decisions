// EpisodeManager.cs — генератор стартовых ситуаций и жизненный цикл похода (эпизода).
//
// Поход заканчивается одним из трёх исходов:
//   SUCCESS   — путешественник дошёл до приюта;
//   EXHAUSTED — выбился из сил (силы упали до нуля, его забирают спасатели);
//   TIMEOUT   — стемнело, а до приюта он так и не дошёл.
//
// Главное требование: MCDA-политика и обученный PPO должны прогоняться
// на ОДНОМ И ТОМ ЖЕ наборе стартов. Поэтому всё детерминировано:
// baseSeed + episodeIndex однозначно задают старт.
using System.Collections.Generic;
using UnityEngine;

namespace CorridorRisk {

public enum RunMode { Benchmark, Training, Demo }

public class EpisodeManager : MonoBehaviour {

    [Header("Ссылки")]
    [SerializeField] LevelGraph          graph;
    [SerializeField] AgentState          state;
    [SerializeField] AgentMotor          motor;
    [SerializeField] AgentTriggers       triggers;
    [SerializeField] StrategyExecutor    executor;
    [SerializeField] EpisodeLogger       logger;
    [SerializeField] CorridorRiskBalance balance;

    [Header("Режим прогона")]
    public RunMode mode = RunMode.Demo;
    public int  baseSeed  = 20260918;
    public int  episodeCount = 500;               // для Benchmark
    public string policySource = "MCDA";          // пишется в лог: MCDA / PPO / FORCED

    [Header("Demo: ситуация задаётся вручную")]
    [Range(0f,1f)] public float demoEnergy   = 0.8f;
    [Range(0f,1f)] public float demoDist     = 0.85f;
    [Range(0f,1f)] public float demoWeather  = 0.5f;
    [Range(0f,1f)] public float demoSupplies = 0.7f;
    [Range(0f,1f)] public float demoShelter  = 0.4f;

    [Header("Уровень B: эпизодами управляет ML-Agents")]
    [Tooltip("Если включено, менеджер не запускает следующий эпизод сам — этим занимается DecisionAgent.")]
    public bool externalEpisodeControl = false;

    [Header("Обучение: фоновая непогода из учебного плана")]
    public bool useCurriculumAmbient = false;
    [Range(0f,0.5f)] public float curriculumAmbient = 0.2f;

    int   _episode;
    float _t0;
    EpisodeRecord _rec;
    List<int> _spawnNodes = new List<int>();
    bool  _active;

    public string LastResult { get; private set; } = "";
    public bool   IsActive => _active;
    public int    EpisodeIndex  => _episode;
    public float  Elapsed       => _active ? Time.time - _t0 : 0f;
    public float  MaxTime       => balance.maxEpisodeTime;
    public float  BaseTimeScale => Mathf.Max(0.1f, balance.timeScale);

    /// Поход закончился: (номер похода, исход). Нужно панели SituationPanel.
    public event System.Action<int, string> EpisodeFinished;

    /// Прервать текущий поход (без записи в лог) и начать следующий из заданной ситуации.
    public void RestartEpisode() { if (!externalEpisodeControl) StartEpisode(_episode + 1); }

    void Start() {
        LevelRegistry.EnsureBuilt();
        Time.timeScale = Mathf.Max(0.1f, balance.timeScale);
        CacheSpawnNodes();
        if (!externalEpisodeControl) StartEpisode(0);
    }

    void CacheSpawnNodes() {
        _spawnNodes.Clear();
        var ids = graph.Data != null ? graph.Data.spawnNodes : null;
        if (ids == null || ids.Length == 0) {
            for (int i = 0; i < graph.NodeCount; i++)
                if (graph.IdOf(i) != "HUT") _spawnNodes.Add(i);
        } else {
            foreach (var id in ids) { int i = graph.IndexOf(id); if (i >= 0) _spawnNodes.Add(i); }
        }
        Debug.Log($"[EpisodeManager] точек спавна: {_spawnNodes.Count}");
    }

    // --- генератор стартового состояния ---------------------------------------

    public void StartEpisode(int index) {
        // Подстраховка: на уровне B эпизод может начаться раньше, чем отработает Start().
        if (_spawnNodes.Count == 0) CacheSpawnNodes();

        _episode = index;
        int seed = baseSeed * 7919 + index;
        var rng = new System.Random(seed);

        float tEnergy, tDist, tWeather, tSupplies, tShelter;
        if (mode == RunMode.Demo) {
            tEnergy = demoEnergy; tDist = demoDist; tWeather = demoWeather;
            tSupplies = demoSupplies; tShelter = demoShelter;
        } else {
            // Те же диапазоны, что в функции evaluate() мини-симулятора ноутбука.
            tEnergy   = Lerp(rng, 0.45f, 1.00f);
            tDist     = Lerp(rng, 0.55f, 0.95f);
            tWeather  = Lerp(rng, 0.15f, 0.85f);
            tSupplies = Lerp(rng, 0.30f, 0.90f);
            tShelter  = Lerp(rng, 0.10f, 0.90f);
        }
        if (useCurriculumAmbient) tWeather = Mathf.Clamp01(tWeather * 0.5f + curriculumAmbient);

        // 1. Узел, ближайший по (dist01, shelter01).
        //    Вес расстояния выше: оно сильнее влияет на длительность похода.
        int best = _spawnNodes[0]; float bestErr = float.MaxValue;
        foreach (int i in _spawnNodes) {
            float err = 2.0f * Sqr(graph.NodeDist01(i) - tDist)
                      + 1.0f * Sqr(graph.NodeShelter(i) - tShelter);
            if (err < bestErr) { bestErr = err; best = i; }
        }

        // 2. Точка спавна — джиттер вокруг узла.
        Vector2 pos = graph.PositionOf(best) + RandomInCircle(rng, 1.5f);

        // 3. Непогода: сначала усиливаем/ослабляем участки непогоды, остаток добираем фоном.
        float raw = LevelRegistry.RawWeather(pos);
        float scale, ambient;
        if (raw >= 0.05f) {
            scale   = Mathf.Clamp(tWeather / raw, 0.30f, 2.00f);
            ambient = Mathf.Clamp01(tWeather - raw * scale);
        } else {
            scale   = 1f;
            ambient = tWeather;
        }

        // --- применяем ---------------------------------------------------------
        LevelRegistry.ResetAll();
        motor.Stop();
        state.transform.position = pos;
        state.ResetState(tEnergy, tSupplies, scale, ambient);
        triggers.ResetCounters();
        executor.BeginEpisode();
        logger.SetEpisode(index);

        _rec = new EpisodeRecord {
            episode = index, seed = seed, policySource = policySource,
            spawnNode = graph.IdOf(best),
            targetEnergy = tEnergy, targetDist = tDist, targetWeather = tWeather,
            targetSupplies = tSupplies, targetShelter = tShelter,
            actualEnergy0 = state.Energy01, actualDist0 = state.Dist01, actualWeather0 = state.Weather01,
            actualSupplies0 = state.Supplies01, actualShelter0 = state.Shelter01,
            globalWeatherScale = scale, ambientWeather = ambient
        };
        _t0 = Time.time;
        _active = true;
    }

    void Update() {
        if (!_active) return;

        Vector2 p = state.transform.position;
        string result = null;

        if (state.energy <= 0.001f)                                           result = "EXHAUSTED";
        else if (LevelRegistry.Hut != null && LevelRegistry.Hut.Contains(p)) result = "SUCCESS";
        else if (Time.time - _t0 >= balance.maxEpisodeTime)                   result = "TIMEOUT";

        if (result != null) FinishEpisode(result);
    }

    void FinishEpisode(string result) {
        _active = false;
        LastResult = result;
        executor.EndEpisode();

        _rec.result            = result;
        _rec.decisions         = executor.Decisions;
        _rec.durationSec       = Time.time - _t0;
        _rec.finalEnergy       = state.Energy01;
        _rec.finalSupplies     = state.Supplies01;
        _rec.suppliesCollected = triggers.SuppliesCollected;
        _rec.viewpointsVisited = triggers.ViewpointsVisited;
        _rec.energyLost        = balance.energyMax * (_rec.targetEnergy - state.Energy01);
        _rec.strategyChanges   = executor.StrategyChanges;
        logger.LogEpisode(_rec);
        EpisodeFinished?.Invoke(_episode, result);

        if (externalEpisodeControl) return;          // следующий эпизод запустит DecisionAgent

        if (mode == RunMode.Benchmark && _episode + 1 >= episodeCount) {
            Debug.Log($"[EpisodeManager] бенчмарк завершён: {episodeCount} эпизодов, " +
                      $"лог в {logger.Folder}");
            Time.timeScale = 0f;
            return;
        }
        StartEpisode(_episode + 1);
    }

    // --- утилиты ---------------------------------------------------------------
    static float Lerp(System.Random r, float a, float b) => a + (b - a) * (float)r.NextDouble();
    static float Sqr(float v) => v * v;

    static Vector2 RandomInCircle(System.Random r, float radius) {
        double ang = r.NextDouble() * System.Math.PI * 2.0;
        double rad = radius * System.Math.Sqrt(r.NextDouble());
        return new Vector2((float)(rad * System.Math.Cos(ang)), (float)(rad * System.Math.Sin(ang)));
    }
}

}
