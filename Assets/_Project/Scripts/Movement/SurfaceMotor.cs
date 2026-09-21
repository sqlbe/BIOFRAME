using UnityEngine;

namespace Bioframe.Movement
{
    public enum SurfaceState
    {
        Ground,     // 지면
        Wall,       // 벽면에 붙음
        Ceiling,    // 천장에 붙음
        Air         // 공중
    }

    // 중력 방향을 표면에 맞춰 바꾸는 이동 처리기.
    // Unity 기본 CharacterController는 중력이 항상 아래로 고정이라 벽·천장을 못 걷는다.
    // 비행(M1 이후)도 이 틀 위에 얹는다.
    public class SurfaceMotor : MonoBehaviour
    {
        [Header("몸체")]
        public float radius = 0.5f;
        public float height = 1.3f;
        public float skin = 0.03f;

        [Header("이동")]
        public float gravity = 22f;
        public float maxGroundAngle = 55f;      // 이 각도 이하면 지면으로 본다
        public float snapDistance = 0.45f;      // 표면에 붙어 있다고 보는 거리
        public float stickAccel = 30f;          // 표면 쪽으로 눌러 붙이는 힘
        public float alignSpeed = 12f;          // 몸이 표면 기울기를 따라가는 속도
        public LayerMask mask = ~0;

        [Header("파츠 능력")]
        public bool canWallClimb;
        public bool canCeiling;

        public Vector3 Up { get; private set; }
        public Vector3 SurfaceNormal { get; private set; }
        public SurfaceState State { get; private set; }
        public bool Attached { get { return State == SurfaceState.Wall || State == SurfaceState.Ceiling; } }
        public bool Grounded { get { return State != SurfaceState.Air; } }
        public Vector3 Facing { get; set; }

        float _fallSpeed;           // -Up 방향 속도
        float _airTime;
        float _detachTimer;         // 이 시간 동안은 표면에 다시 붙지 않는다
        SphereCollider _shape;      // 겹침 밀어내기 계산용
        Vector3 _pendingNormal;
        bool _hasPending;

        void Awake()
        {
            _shape = gameObject.AddComponent<SphereCollider>();
            _shape.radius = radius - skin;
            _shape.center = new Vector3(0f, height * 0.5f, 0f);
            _shape.isTrigger = true;    // 물리 충돌은 쓰지 않고 겹침 계산에만 쓴다

            Up = Vector3.up;
            SurfaceNormal = Vector3.up;
            State = SurfaceState.Air;
            Facing = transform.forward;
        }

        // 바깥에서 몸 방향을 즉시 지정한다(표면 전환 직후 목적지 쪽을 보게 할 때 쓴다).
        public void SnapFacing(Vector3 worldDir)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(worldDir, Up);
            if (fwd.sqrMagnitude < 0.0004f) return;
            fwd.Normalize();
            Facing = fwd;
            transform.rotation = Quaternion.LookRotation(fwd, Up);
        }

        public void Detach(float upwardPush = 0f)
        {
            // 붙어 있던 표면에서 살짝 떨어뜨려 놓는다. 안 그러면 다음 프레임에 다시 붙는다.
            if (Attached) transform.position += SurfaceNormal * 0.25f;
            State = SurfaceState.Air;
            SurfaceNormal = Vector3.up;
            _fallSpeed = -upwardPush;
            _airTime = 0f;
            _detachTimer = 0.4f;
        }

        public void Jump(float speed)
        {
            if (!Grounded) return;
            // 벽에서는 벽을 차고 나가면서 지면 중력으로 돌아간다
            if (Attached)
            {
                Vector3 push = SurfaceNormal * speed * 0.6f;
                transform.position += push * 0.05f;
                Detach(speed * 0.8f);
            }
            else
            {
                _fallSpeed = -speed;
                State = SurfaceState.Air;
                // 뛴 직후 바닥 감지가 다시 달라붙어 점프를 지워버리지 않게 잠깐 멈춘다
                _detachTimer = 0.18f;
            }
        }

        // planarVelocity: 현재 표면을 따라가는 월드 속도
        public void Move(Vector3 planarVelocity, float dt)
        {
            if (dt <= 0f) return;

            Vector3 up = Up;
            planarVelocity = Vector3.ProjectOnPlane(planarVelocity, up);

            if (_detachTimer > 0f) _detachTimer -= dt;

            // 표면을 찾는다
            RaycastHit hit = new RaycastHit();
            bool found = _detachTimer <= 0f && Probe(up, out hit);

            // 벽 끝을 넘어갔을 때: 발밑에 아무것도 없으면 세계 기준 아래쪽을 확인해 지면으로 복귀한다
            if (!found && _detachTimer <= 0f && Attached)
            {
                RaycastHit down;
                if (Probe(Vector3.up, out down) && Vector3.Angle(down.normal, Vector3.up) <= maxGroundAngle)
                {
                    Vector3 awayFromWall = SurfaceNormal;
                    up = Vector3.up;
                    Up = Vector3.up;
                    hit = down;
                    found = true;
                    _fallSpeed = 0f;
                    SnapOrientation(awayFromWall, planarVelocity);
                }
            }

            if (found)
            {
                _airTime = 0f;
                SurfaceNormal = hit.normal;
                float angle = Vector3.Angle(hit.normal, Vector3.up);
                if (angle <= maxGroundAngle) State = SurfaceState.Ground;
                else if (angle < 120f) State = SurfaceState.Wall;
                else State = SurfaceState.Ceiling;

                // 표면에서 떨어지지 않게 살짝 눌러 붙인다
                _fallSpeed = Mathf.Max(0f, _fallSpeed);
                float gap = hit.distance - (height * 0.5f - radius) - skin;
                if (gap > 0.001f) _fallSpeed += stickAccel * dt;
                else _fallSpeed = 0f;
            }
            else
            {
                _airTime += dt;
                if (_airTime > 0.12f)
                {
                    State = SurfaceState.Air;
                    SurfaceNormal = Vector3.up;
                }
                _fallSpeed += gravity * dt;
            }

            // 벽을 타고 내려오다 바닥에 닿으면 지면으로 넘어간다.
            // 올라가는 중에는 적용하지 않는다. 안 그러면 벽에 붙자마자 도로 땅으로 떨어진다.
            if (State == SurfaceState.Wall && Vector3.Dot(planarVelocity, Vector3.down) > 0.01f)
            {
                RaycastHit floor;
                if (ProbeDown(1.0f, out floor) && Vector3.Angle(floor.normal, Vector3.up) <= maxGroundAngle)
                {
                    Vector3 awayFromWall = SurfaceNormal;   // 바꾸기 전의 벽 법선
                    up = Vector3.up;
                    Up = Vector3.up;
                    SurfaceNormal = floor.normal;
                    State = SurfaceState.Ground;
                    _fallSpeed = 0f;
                    planarVelocity = Vector3.ProjectOnPlane(planarVelocity, up);
                    SnapOrientation(awayFromWall, planarVelocity);
                }
            }

            Vector3 motion = planarVelocity * dt - up * (_fallSpeed * dt);
            CollideAndSlide(ref motion, up);
            transform.position += motion;

            // 벽에 부딪혔고 벽 타기가 가능하면 그 벽으로 옮겨 붙는다
            if (_detachTimer <= 0f && _hasPending && CanAttachTo(_pendingNormal))
            {
                SurfaceNormal = _pendingNormal;
                float a = Vector3.Angle(_pendingNormal, Vector3.up);
                State = a <= maxGroundAngle ? SurfaceState.Ground : (a < 120f ? SurfaceState.Wall : SurfaceState.Ceiling);
                _fallSpeed = 0f;
            }
            _hasPending = false;

            ResolveOverlap();

            // 몸의 위쪽 방향을 표면에 맞춘다
            Vector3 targetUp = State == SurfaceState.Air ? Vector3.up : SurfaceNormal;
            Up = Vector3.Slerp(Up, targetUp, 1f - Mathf.Exp(-alignSpeed * dt)).normalized;

            Vector3 fwd = Vector3.ProjectOnPlane(Facing.sqrMagnitude > 0.001f ? Facing : transform.forward, Up);
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.ProjectOnPlane(transform.forward, Up);
            if (fwd.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(fwd.normalized, Up), 1f - Mathf.Exp(-alignSpeed * dt));
        }

        // 지형에 파묻히면 캐스트가 전부 거리 0으로 잡혀 조작이 먹통이 된다. 매 프레임 밀어낸다.
        void ResolveOverlap()
        {
            if (_shape == null) return;
            _shape.radius = radius - skin;
            _shape.center = new Vector3(0f, height * 0.5f, 0f);

            Vector3 center = transform.TransformPoint(_shape.center);
            var cols = Physics.OverlapSphere(center, _shape.radius, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || cols[i].transform.IsChildOf(transform)) continue;

                Vector3 dir;
                float dist;
                if (Physics.ComputePenetration(_shape, transform.position, transform.rotation,
                                               cols[i], cols[i].transform.position, cols[i].transform.rotation,
                                               out dir, out dist))
                {
                    transform.position += dir * (dist + 0.001f);
                }
            }
        }

        // 표면이 바뀌는 순간 몸 방향을 새 평면에 맞춰 바로 고쳐 준다.
        // 그냥 두면 벽에서 아래를 보던 방향이 땅에서 한 바퀴 도는 것처럼 보인다.
        void SnapOrientation(Vector3 fallback, Vector3 planarVelocity)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(planarVelocity, Up);
            if (fwd.sqrMagnitude < 0.04f) fwd = Vector3.ProjectOnPlane(Facing, Up);
            if (fwd.sqrMagnitude < 0.04f) fwd = Vector3.ProjectOnPlane(fallback, Up);
            if (fwd.sqrMagnitude < 0.0001f) return;

            fwd.Normalize();
            Facing = fwd;
            transform.rotation = Quaternion.LookRotation(fwd, Up);
        }

        bool CanAttachTo(Vector3 normal)
        {
            float a = Vector3.Angle(normal, Vector3.up);
            if (a <= maxGroundAngle) return true;
            if (a < 120f) return canWallClimb;
            return canCeiling;
        }

        // 세계 기준 아래쪽 바닥을 조금 더 멀리까지 찾는다. 벽에서 내려올 때 쓴다.
        bool ProbeDown(float extra, out RaycastHit hit)
        {
            Vector3 center = transform.position + Vector3.up * (height * 0.5f);
            float dist = (height * 0.5f - radius) + snapDistance + extra;
            if (Physics.SphereCast(center, radius - skin, Vector3.down, out hit, dist, mask, QueryTriggerInteraction.Ignore))
                return !hit.transform.IsChildOf(transform);
            return false;
        }

        bool Probe(Vector3 up, out RaycastHit hit)
        {
            Vector3 center = transform.position + up * (height * 0.5f);
            float dist = (height * 0.5f - radius) + snapDistance;
            if (Physics.SphereCast(center, radius - skin, -up, out hit, dist, mask, QueryTriggerInteraction.Ignore))
                return !hit.transform.IsChildOf(transform);
            return false;
        }

        void CollideAndSlide(ref Vector3 motion, Vector3 up)
        {
            for (int i = 0; i < 4; i++)
            {
                float len = motion.magnitude;
                if (len < 0.0001f) return;

                Vector3 dir = motion / len;
                Vector3 center = transform.position + up * (height * 0.5f);

                RaycastHit hit;
                if (!SphereCastIgnoringSelf(center, dir, len + skin, out hit)) return;

                float travel = Mathf.Max(0f, hit.distance - skin);
                Vector3 moved = dir * travel;
                Vector3 remaining = motion - moved;

                // 벽 타기 후보로 기억해 둔다
                if (Vector3.Angle(hit.normal, Vector3.up) > maxGroundAngle)
                {
                    _pendingNormal = hit.normal;
                    _hasPending = true;
                }

                motion = moved + Vector3.ProjectOnPlane(remaining, hit.normal);
                transform.position += moved;
                motion -= moved;
            }
        }

        bool SphereCastIgnoringSelf(Vector3 center, Vector3 dir, float dist, out RaycastHit best)
        {
            best = new RaycastHit();
            var hits = Physics.SphereCastAll(center, radius - skin, dir, dist, mask, QueryTriggerInteraction.Ignore);
            float min = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;
                if (hits[i].distance < min) { min = hits[i].distance; best = hits[i]; found = true; }
            }
            return found;
        }
    }
}
