// CorridorRiskBalance.cs — все числа баланса в одном ScriptableObject,
// чтобы студенты правили значения, не трогая код.
// Create > CorridorRisk > Balance
using UnityEngine;

namespace CorridorRisk {

[CreateAssetMenu(fileName = "CorridorRiskBalance", menuName = "CorridorRisk/Balance")]
public class CorridorRiskBalance : ScriptableObject {

    [Header("Ресурсы агента")]
    public float hpMax   = 100f;
    public float ammoMax = 100f;

    [Header("Движение")]
    public float baseSpeed = 6.5f;          // ед./с при множителе 1.0 и полном запасе

    [Header("Принятие решений")]
    public float decisionInterval = 1.0f;   // с; ≈ один шаг мини-симулятора в ноутбуке
    public float hysteresis       = 0.02f;  // минимальный margin для смены стратегии

    [Header("Урон и расход")]
    public float kDamage = 18f;             // HP/с при threat01 = 1 и cover01 = 0
    public float kDrain  = 2.2f;            // %/с расхода запаса при движении

    [Header("Состояние")]
    public float smoothing = 0.5f;          // постоянная времени EMA для threat01 и cover01

    [Header("Эпизод")]
    public float maxEpisodeTime = 45f;      // с

    [Header("Ускорение прогона")]
    [Tooltip("Для замеров ставьте 20. Для показа на занятии — 1.")]
    public float timeScale = 1f;
}

}
