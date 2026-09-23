using UnityEngine;

namespace Bioframe.Combat
{
    // 날아가는 발사체. 맛보기 구현이라 유도는 없다.
    // 쏜 순간의 위치를 향해 날아가므로, 상대가 움직이면 빗나가고 엄폐물에 막힌다.
    public class Projectile : MonoBehaviour
    {
        public enum Result { Hit, Miss, Blocked }

        Transform _owner;
        Damageable _target;
        Vector3 _aimPoint;
        Vector3 _tip;
        float _speed;
        float _hitRadius;
        System.Action<Damageable, Result> _onArrive;
        LineRenderer _line;
        float _life = 3f;
        float _traveled;
        const float IgnoreBlockDistance = 1.2f;   // 출발 직후에는 지형에 막히지 않는다
        bool _done;
        float _fade;

        // 대상 없이 한 지점을 향해 쏜다. 가는 길에 누구든 맞으면 명중이다.
        public static Projectile Spawn(Transform owner, Vector3 from, Vector3 aimPoint,
                                       float speed, float hitRadius, Color color, float width,
                                       System.Action<Damageable, Result> onArrive)
        {
            var p = Spawn(owner, from, null, speed, hitRadius, color, width, onArrive);
            p._aimPoint = aimPoint;
            return p;
        }

        public static Projectile Spawn(Transform owner, Vector3 from, Damageable target,
                                       float speed, float hitRadius, Color color, float width,
                                       System.Action<Damageable, Result> onArrive)
        {
            var go = new GameObject("Projectile");
            var p = go.AddComponent<Projectile>();
            p._owner = owner;
            p._target = target;
            p._aimPoint = target != null ? target.transform.position + Vector3.up * 1.0f
                                        : from + (owner != null ? owner.forward : Vector3.forward) * 12f;
            p._tip = from;
            p._speed = speed;
            p._hitRadius = hitRadius;
            p._onArrive = onArrive;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width * 0.7f;
            lr.numCapVertices = 2;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = color;
            lr.sharedMaterial = mat;
            lr.SetPosition(0, from);
            lr.SetPosition(1, from);
            p._line = lr;

            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (_done)
            {
                // 줄이 잠깐 남았다가 사라진다
                _fade -= dt;
                if (_fade <= 0f) Destroy(gameObject);
                return;
            }

            _life -= dt;
            if (_life <= 0f) { Finish(Result.Miss); return; }

            Vector3 prev = _tip;
            Vector3 dir = _aimPoint - _tip;
            float dist = dir.magnitude;
            float step = _speed * dt;

            if (dist <= step)
            {
                _tip = _aimPoint;
                Draw();
                Finish(CheckHit() ? Result.Hit : Result.Miss);
                return;
            }

            _tip += dir / dist * step;
            _traveled += step;
            Draw();

            // 날아가는 길에 있는 대상과 부딪히는지 본다
            var swept = Physics.OverlapSphere(_tip, _hitRadius * 0.7f, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < swept.Length; i++)
            {
                if (_owner != null && swept[i].transform.IsChildOf(_owner)) continue;
                var d = swept[i].GetComponentInParent<Damageable>();
                if (d == null || !d.Alive) continue;
                _target = d;
                Finish(Result.Hit);
                return;
            }

            // 가는 길에 지형이 막으면 거기서 멈춘다
            RaycastHit hit;
            if (_traveled > IgnoreBlockDistance && Physics.Linecast(prev, _tip, out hit, ~0, QueryTriggerInteraction.Ignore))
            {
                bool self = _owner != null && hit.transform.IsChildOf(_owner);
                bool onTarget = _target != null && hit.transform.IsChildOf(_target.transform);
                if (!self && !onTarget)
                {
                    _tip = hit.point;
                    Draw();
                    Finish(Result.Blocked);
                    return;
                }
                if (onTarget) { Finish(Result.Hit); return; }
            }
        }

        bool CheckHit()
        {
            if (_target == null || !_target.Alive) return false;
            Vector3 center = _target.transform.position + Vector3.up * 1.0f;
            return Vector3.Distance(center, _tip) <= _hitRadius;
        }

        void Draw()
        {
            if (_line == null || _owner == null) return;
            _line.SetPosition(0, _owner.position + Vector3.up * 0.9f);   // 입에서 뻗어 나가는 줄
            _line.SetPosition(1, _tip);
        }

        void Finish(Result r)
        {
            _done = true;
            if (r == Result.Hit) Fx.Spark(_tip, Vector3.up, new Color(0.95f, 0.96f, 1f), 8, 3.5f);
            else if (r == Result.Blocked) Fx.Spark(_tip, -(_tip - transform.position).normalized, new Color(0.8f, 0.8f, 0.85f), 4, 2.5f);
            _fade = r == Result.Hit ? 0.35f : 0.15f;
            if (_onArrive != null) _onArrive(_target, r);
        }
    }
}
