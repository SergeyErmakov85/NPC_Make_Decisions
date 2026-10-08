// CorridorRiskBalance.cs — все числа баланса в одном ScriptableObject,
// чтобы студенты правили значения, не трогая код.
// Create > CorridorRisk > Balance
using UnityEngine;

namespace CorridorRisk {

[CreateAssetMenu(fileName = "CorridorRiskBalance", menuName = "CorridorRisk/Balance")]
public class CorridorRiskBalance : ScriptableObject {

    [Header("Силы и запасы путешественника")]
    public float energyMax   = 100f;       // максимум сил
    public float suppliesMax = 100f;       // максимум запаса воды и еды

    [Header("Движение")]
    public float baseSpeed = 6.5f;          // ед./с при множителе 1.0 и полном запасе

    [Header("Принятие решений")]
    public float decisionInterval = 1.0f;   // с; ≈ один шаг мини-симулятора в ноутбуке
    public float hysteresis       = 0.02f;  // минимальный margin для смены стратегии

    [Header("Усталость и расход запасов")]
    public float kFatigue = 18f;            // потеря сил в секунду при weather01 = 1 и shelter01 = 0
    public float kDrain   = 2.2f;           // %/с расхода воды и еды при ходьбе

    [Header("Состояние")]
    public float smoothing = 0.5f;          // постоянная времени EMA для weather01 и shelter01

    [Header("Поход (эпизод)")]
    public float maxEpisodeTime = 45f;      // с; по истечении — «стемнело», поход не удался

    [Header("Ускорение прогона")]
    [Tooltip("Для замеров ставьте 20. Для показа на занятии — 1.")]
    public float timeScale = 1f;
}

}
