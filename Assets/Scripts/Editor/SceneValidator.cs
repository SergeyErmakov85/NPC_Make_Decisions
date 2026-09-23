// SceneValidator.cs — проверка, что сцена собрана правильно.
// Меню: CorridorRisk → Проверить сцену
//
// Девять из десяти проблем на занятии — это незаполненная ссылка в инспекторе.
// Пусть их находит скрипт, а не преподаватель на глазах у группы.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CorridorRisk.EditorTools {

public static class SceneValidator {

    [MenuItem("CorridorRisk/Проверить сцену")]
    public static void Validate() {
        var problems = new List<string>();
        var ok = new List<string>();

        var graph = Object.FindFirstObjectByType<LevelGraph>();
        Check(graph != null, "LevelGraph на сцене", problems, ok);

        var state = Object.FindFirstObjectByType<AgentState>();
        Check(state != null, "AgentState на агенте", problems, ok);

        var motor = Object.FindFirstObjectByType<AgentMotor>();
        Check(motor != null, "AgentMotor на агенте", problems, ok);

        var policy = Object.FindFirstObjectByType<DecisionPolicy>();
        Check(policy != null, "DecisionPolicy на агенте", problems, ok);

        var exec = Object.FindFirstObjectByType<StrategyExecutor>();
        Check(exec != null, "StrategyExecutor на агенте", problems, ok);

        var manager = Object.FindFirstObjectByType<EpisodeManager>();
        Check(manager != null, "EpisodeManager в Systems", problems, ok);

        var logger = Object.FindFirstObjectByType<EpisodeLogger>();
        Check(logger != null, "EpisodeLogger в Systems", problems, ok);

        Check(Object.FindFirstObjectByType<GoalZone>() != null, "GoalZone", problems, ok);
        Check(Object.FindFirstObjectByType<SafeZone>() != null, "SafeZone", problems, ok);

        int threats = Object.FindObjectsByType<ThreatZone>(FindObjectsSortMode.None).Length;
        Check(threats >= 7, $"зон угрозы: {threats} (ожидается 7)", problems, ok);

        int covers = Object.FindObjectsByType<CoverZone>(FindObjectsSortMode.None).Length;
        Check(covers >= 10, $"зон укрытий: {covers} (ожидается 10)", problems, ok);

        int waypoints = Object.FindObjectsByType<WaypointMarker>(FindObjectsSortMode.None).Length;
        Check(waypoints >= 25, $"путевых точек: {waypoints} (ожидается 25)", problems, ok);

        if (state != null) {
            var rb = state.GetComponent<Rigidbody2D>();
            Check(rb != null, "Rigidbody2D на агенте", problems, ok);
            Check(state.GetComponent<Collider2D>() != null, "Collider2D на агенте", problems, ok);
        }

        var sb = new StringBuilder();
        sb.AppendLine(problems.Count == 0 ? "СЦЕНА СОБРАНА ПРАВИЛЬНО" : "НАЙДЕНЫ ПРОБЛЕМЫ:");
        foreach (var p in problems) sb.AppendLine("  ✗ " + p);
        sb.AppendLine("Проверено успешно: " + ok.Count + " пунктов");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("CorridorRisk — проверка сцены", sb.ToString(), "Ок");
    }

    static void Check(bool condition, string what, List<string> problems, List<string> ok) {
        if (condition) ok.Add(what); else problems.Add("не найдено: " + what);
    }
}

}
