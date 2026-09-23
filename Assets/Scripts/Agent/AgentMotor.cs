// AgentMotor.cs — движение по ломаной. Никакой физики: kinematic Rigidbody2D
// и MovePosition, чтобы поведение было полностью детерминированным.
using System.Collections.Generic;
using UnityEngine;

namespace CorridorRisk {

[RequireComponent(typeof(Rigidbody2D))]
public class AgentMotor : MonoBehaviour {

    [SerializeField] CorridorRiskBalance balance;
    [SerializeField] AgentState state;

    Rigidbody2D _rb;
    List<Vector2> _path = new List<Vector2>();
    int _leg;
    float _speedMul = 1f;
    const float ArriveEps = 0.35f;

    public bool HasPath => _path != null && _leg < _path.Count;
    public Vector2 Velocity { get; private set; }

    void Awake() {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
    }

    public void SetPath(List<Vector2> path, float speedMultiplier) {
        _path = path ?? new List<Vector2>();
        _speedMul = speedMultiplier;
        _leg = 0;
        // Первая точка пути — ближайший узел, он может быть позади агента.
        // Пропускаем её, если мы уже фактически в ней.
        if (_path.Count > 0 && Vector2.Distance(_rb.position, _path[0]) < ArriveEps) _leg = 1;
    }

    public void Stop() { _path = new List<Vector2>(); _leg = 0; Velocity = Vector2.zero; }

    void FixedUpdate() {
        if (!HasPath) { Velocity = Vector2.zero; return; }

        Vector2 target = _path[_leg];
        Vector2 pos = _rb.position;
        float speed = balance.baseSpeed * _speedMul * state.AmmoFactor;
        float step = speed * Time.fixedDeltaTime;

        Vector2 delta = target - pos;
        if (delta.magnitude <= Mathf.Max(step, ArriveEps)) {
            _rb.MovePosition(target);
            Velocity = Vector2.zero;
            _leg++;
            return;
        }

        Vector2 dir = delta.normalized;
        _rb.MovePosition(pos + dir * step);
        Velocity = dir * speed;

        // Расход запаса пропорционален скорости.
        state.ammo -= balance.kDrain * 0.01f * balance.ammoMax * _speedMul * Time.fixedDeltaTime;
    }

    void OnDrawGizmos() {
        if (_path == null || _path.Count == 0) return;
        Gizmos.color = Color.cyan;
        for (int i = Mathf.Max(0, _leg); i < _path.Count - 1; i++)
            Gizmos.DrawLine(_path[i], _path[i + 1]);
        if (_leg < _path.Count) Gizmos.DrawSphere(_path[_path.Count - 1], 0.5f);
    }
}

}
