using UnityEngine;
using Bioframe.Rules;

namespace Bioframe.Movement
{
    public enum ControlMode
    {
        Direct,     // WASD + 마우스 시점 (3인칭 액션)
        ClickMove   // 좌클릭한 지점으로 이동 (쿼터뷰 전략)
    }

    // 조립 결과(FrameStats)를 조작감으로 옮긴다.
    // 벽면을 클릭하면 벽까지 걸어가 자동으로 기어오른다.
    [RequireComponent(typeof(SurfaceMotor))]
    public class FrameController : MonoBehaviour
    {
        public const float CeilingWeightLimit = 200f;   // 규격서 C3
        public const float WallSpeedMul = 0.7f;         // 벽에서는 70% 속도

        public ControlMode mode = ControlMode.ClickMove;
        public float accel = 16f;
        public float arriveDistance = 0.7f;
        public float detachLockTime = 0.25f;
        public Transform cameraPivot;

        SurfaceMotor _motor;
        FrameStats _stats;
        Vector3 _planar;
        Vector3 _destination;
        bool _hasDestination;
        float _en;
        bool _sprinting;
        float _lock;
        float _stuck;
        SurfaceState _prevState = SurfaceState.Air;
        Vector3 _lastPos;
        Transform _marker;

        public FrameStats Stats { get { return _stats; } }
        public float Energy { get { return _en; } }
        public bool Sprinting { get { return _sprinting; } }
        public float PlanarSpeed { get { return _planar.magnitude; } }
        public SurfaceState State { get { return _motor != null ? _motor.State : SurfaceState.Air; } }
        public bool CanCeiling { get { return _motor != null && _motor.canCeiling; } }
        public bool HasDestination { get { return _hasDestination; } }

        void Awake() { _motor = GetComponent<SurfaceMotor>(); }

        public void Init(FrameStats stats, Transform camPivot)
        {
            _stats = stats;
            _en = stats.enMax;
            cameraPivot = camPivot;
            if (_motor == null) _motor = GetComponent<SurfaceMotor>();

            _motor.canWallClimb = stats.wallClimb;
            _motor.canCeiling = stats.ceiling && stats.totalWeight <= CeilingWeightLimit;
        }

        public void SetMode(ControlMode m)
        {
            mode = m;
            ClearDestination();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_lock > 0f) _lock -= dt;

            Vector3 up = _motor.Up;
            Vector2 input = InputReader.Move;
            if (input.sqrMagnitude > 1f) input.Normalize();

            float yaw = cameraPivot != null ? cameraPivot.eulerAngles.y : transform.eulerAngles.y;
            Vector3 camFwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 camRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 wish = Vector3.ProjectOnPlane(camFwd * input.y + camRight * input.x, up);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            if (mode == ControlMode.ClickMove)
            {
                if (input.sqrMagnitude > 0.01f) ClearDestination();
                else
                {
                    if (InputReader.ClickHeld) PickDestination();
                    if (_hasDestination)
                    {
                        Vector3 to = _destination - transform.position;
                        if (to.magnitude <= arriveDistance) ClearDestination();
                        else
                        {
                            // 목적지가 벽 위면, 벽에 닿는 순간 모터가 벽으로 옮겨 붙는다
                            Vector3 along = Vector3.ProjectOnPlane(to, up);
                            wish = along.sqrMagnitude > 0.0004f ? along.normalized : Vector3.zero;
                        }
                    }
                }
            }

            if (_lock > 0f) wish = Vector3.zero;

            bool moving = wish.sqrMagnitude > 0.01f;
            bool wantSprint = InputReader.Sprint && moving && !_motor.Attached;
            _sprinting = wantSprint && (_stats.sprintUpkeep <= 0f || _en > 0.5f);
            if (_sprinting && _stats.sprintUpkeep > 0f) _en -= _stats.sprintUpkeep * dt;
            else _en += _stats.enRegen * dt;
            _en = Mathf.Clamp(_en, 0f, Mathf.Max(1f, _stats.enMax));

            float speed = _sprinting ? _stats.sprintSpeed : _stats.speed;
            if (_motor.Attached) speed *= WallSpeedMul;

            _planar = Vector3.MoveTowards(_planar, wish * speed, accel * dt);
            _planar = Vector3.ProjectOnPlane(_planar, up);
            if (_planar.sqrMagnitude > 0.05f) _motor.Facing = _planar.normalized;

            if (InputReader.JumpPressed)
            {
                float jumpSpeed = Mathf.Sqrt(2f * _motor.gravity * Mathf.Max(0.2f, _stats.jumpHeight));
                bool wasAttached = _motor.Attached;
                _motor.Jump(jumpSpeed);
                if (wasAttached) { _lock = detachLockTime; ClearDestination(); }
            }
            else if (_motor.Attached && InputReader.DownHeld)
            {
                _motor.Detach();
                _lock = detachLockTime * 0.5f;
                ClearDestination();
            }

            _motor.Move(_planar, dt);

            // 벽에서 땅으로 내려선 순간, 가려던 쪽을 바로 보게 한다.
            // 이걸 안 하면 착지 후에 목적지 쪽으로 몸을 다시 돌리느라 한 바퀴 도는 것처럼 보인다.
            bool wasOnWall = _prevState == SurfaceState.Wall || _prevState == SurfaceState.Ceiling;
            if (wasOnWall && _motor.State == SurfaceState.Ground)
            {
                Vector3 dir = _hasDestination ? _destination - transform.position : _planar;
                _motor.SnapFacing(dir);
                _planar = Vector3.ProjectOnPlane(_planar, _motor.Up);
            }
            _prevState = _motor.State;

            // 벽 사이에 끼여 움직이지 못하면 스스로 떨어져 나온다
            if (moving && (transform.position - _lastPos).sqrMagnitude < 0.0004f * dt)
            {
                _stuck += dt;
                if (_stuck > 1.0f)
                {
                    _motor.Detach(3f);
                    ClearDestination();
                    _stuck = 0f;
                }
            }
            else _stuck = 0f;
            _lastPos = transform.position;
        }

        void PickDestination()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(InputReader.MousePosition);
            var hits = Physics.RaycastAll(ray, 250f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            Vector3 point = Vector3.zero, normal = Vector3.up;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    point = hits[i].point;
                    normal = hits[i].normal;
                    found = true;
                }
            }
            if (!found) return;

            _destination = point + normal * 0.3f;
            _hasDestination = true;
            ShowMarker(true, normal);
        }

        void ClearDestination()
        {
            _hasDestination = false;
            if (_marker != null) _marker.gameObject.SetActive(false);
        }

        void ShowMarker(bool on, Vector3 normal)
        {
            if (_marker == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "MoveMarker";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.localScale = new Vector3(0.6f, 0.03f, 0.6f);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var block = new MaterialPropertyBlock();
                    var c = new Color(0.36f, 0.78f, 0.72f);
                    block.SetColor("_BaseColor", c);
                    block.SetColor("_Color", c);
                    mr.SetPropertyBlock(block);
                }
                _marker = go.transform;
            }

            _marker.gameObject.SetActive(on);
            if (!on) return;
            _marker.position = _destination;
            _marker.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        }

        void OnDestroy()
        {
            if (_marker != null) Destroy(_marker.gameObject);
        }
    }
}
