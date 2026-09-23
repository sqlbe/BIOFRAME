using UnityEngine;
using Bioframe.Movement;

namespace Bioframe.Combat
{
    // 이동에 붙는 연출: 질주 먼지, 착지 충격파, 벽을 탈 때 남는 흔적.
    // 플레이어와 봇 양쪽에 똑같이 붙인다.
    public class FrameFx : MonoBehaviour
    {
        public Color dustColor = new Color(0.86f, 0.82f, 0.72f);
        public Color markColor = new Color(0.72f, 0.76f, 0.78f);
        public float dustSpeedThreshold = 6f;   // 이 속도를 넘으면 먼지가 난다
        public float dustInterval = 0.11f;
        public float markInterval = 0.18f;

        SurfaceMotor _motor;
        FrameController _ctrl;
        SurfaceState _prevState = SurfaceState.Ground;
        float _dustTimer, _markTimer;
        float _peakFall;

        void Awake()
        {
            _motor = GetComponent<SurfaceMotor>();
            _ctrl = GetComponent<FrameController>();
        }

        void Update()
        {
            if (_motor == null || _ctrl == null) return;

            float dt = Time.deltaTime;
            float speed = _ctrl.PlanarSpeed;
            Vector3 feet = transform.position + _motor.Up * 0.08f;

            // 공중에 있는 동안 최고 낙하 속도를 기억해 둔다
            if (_motor.State == SurfaceState.Air)
                _peakFall = Mathf.Max(_peakFall, _motor.FallSpeed);

            // 착지
            if (_prevState == SurfaceState.Air && _motor.State != SurfaceState.Air)
            {
                if (_peakFall > 6f)
                {
                    float power = Mathf.Clamp(_peakFall / 14f, 0.4f, 2.2f);
                    Fx.Dust(feet, Vector3.up * 0.2f, dustColor, Mathf.RoundToInt(4 + power * 4f), 1.6f * power);
                    Fx.Ring(feet, _motor.Up, dustColor, 1.2f + power, 0.28f);
                    if (_peakFall > 12f && Fx.IsCameraTarget(transform))
                        Fx.Shake(Mathf.Clamp(_peakFall * 0.012f, 0.05f, 0.25f), 0.14f);
                }
                _peakFall = 0f;
            }
            _prevState = _motor.State;

            // 질주 먼지
            if (_motor.State == SurfaceState.Ground && speed > dustSpeedThreshold)
            {
                _dustTimer -= dt;
                if (_dustTimer <= 0f)
                {
                    _dustTimer = dustInterval;
                    Vector3 back = -transform.forward;
                    Fx.Dust(feet, back, dustColor, 2, 1.1f + speed * 0.05f);
                    if (_ctrl.Sprinting) Fx.Afterimage(gameObject, new Color(0.8f, 0.9f, 0.88f), 0.12f, 0.06f);
                }
            }

            // 벽·천장을 탈 때 남는 흔적
            if (_motor.Attached && speed > 0.8f)
            {
                _markTimer -= dt;
                if (_markTimer <= 0f)
                {
                    _markTimer = markInterval;
                    Vector3 surface = transform.position - _motor.Up * 0.1f;
                    Fx.Dust(surface, -_motor.Up * 0.3f, markColor, 1, 0.5f);
                }
            }
        }
    }
}
