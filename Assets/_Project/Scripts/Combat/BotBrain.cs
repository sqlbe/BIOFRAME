using UnityEngine;
using Bioframe.Movement;

namespace Bioframe.Combat
{
    // 봇의 판단. 플레이어와 같은 조립·이동·전투 시스템을 쓴다.
    // 자기 무기 사거리에 맞춰 붙거나 물러나고, 옆으로 돌면서 싸운다.
    public class BotBrain : MonoBehaviour
    {
        public Damageable enemy;
        public float reactionTime = 0.15f;
        public float senseRange = 6f;        // 투명한 상대를 알아채는 거리

        FrameController _ctrl;
        FrameCombat _combat;
        SurfaceMotor _motor;
        Damageable _self;

        float _strafeTimer;
        float _strafeDir = 1f;
        float _think;
        float _jumpCooldown;

        void Awake()
        {
            _ctrl = GetComponent<FrameController>();
            _combat = GetComponent<FrameCombat>();
            _motor = GetComponent<SurfaceMotor>();
            _self = GetComponent<Damageable>();
        }

        void Update()
        {
            if (_ctrl == null || _combat == null || _motor == null) return;

            float dt = Time.deltaTime;
            if (_jumpCooldown > 0f) _jumpCooldown -= dt;

            if (_self != null && !_self.Alive) { _ctrl.botWish = Vector3.zero; return; }
            if (enemy == null || !enemy.Alive)
            {
                _ctrl.botWish = Vector3.zero;
                _combat.ClearTarget();
                return;
            }

            // 속박당하면 못 움직인다
            if (_self != null && _self.Rooted)
            {
                _ctrl.botWish = Vector3.zero;
                _ctrl.FaceTowards(enemy.transform.position);
                _combat.SetTarget(enemy, false);
                return;
            }

            _think -= dt;
            if (_think > 0f) return;
            _think = reactionTime;

            // 상대가 투명이면 가까이 오기 전까지 못 본다
            var enemyCombat = enemy.GetComponent<FrameCombat>();
            bool hidden = enemyCombat != null && enemyCombat.Cloaked
                          && Vector3.Distance(transform.position, enemy.transform.position) > senseRange;
            if (hidden)
            {
                _ctrl.botWish = Vector3.zero;
                _combat.ClearTarget();
                return;
            }

            _combat.SetTarget(enemy, false);

            Vector3 up = _motor.Up;
            Vector3 to = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, up);
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;
            Vector3 side = Vector3.Cross(up, dir).normalized * _strafeDir;

            _strafeTimer -= reactionTime;
            if (_strafeTimer <= 0f)
            {
                _strafeTimer = Random.Range(1.0f, 2.5f);
                _strafeDir = Random.value < 0.5f ? -1f : 1f;
            }

            float range = Mathf.Max(2.2f, _combat.AttackRange);
            Vector3 wish;
            bool sprint = false;

            if (dist > range * 0.95f)
            {
                // 붙는다. 멀면 질주한다.
                wish = dir * 1.0f + side * 0.25f;
                sprint = dist > range * 2.2f;
            }
            else if (dist < range * 0.55f)
            {
                // 너무 붙었으면 조금 물러나며 돈다
                wish = -dir * 0.7f + side * 0.6f;
            }
            else
            {
                // 사거리 안에서는 옆으로 돌며 때린다
                wish = side * 0.8f;
                _ctrl.FaceTowards(enemy.transform.position);
            }

            _ctrl.botWish = wish;
            _ctrl.botSprint = sprint;

            // 높이 차가 크면 뛴다
            float heightGap = Vector3.Dot(enemy.transform.position - transform.position, up);
            if (heightGap > 1.2f && dist < range * 3f && _jumpCooldown <= 0f && _motor.Grounded)
            {
                _ctrl.botJump = true;
                _jumpCooldown = 2f;
            }

            // 머리 파츠는 조건이 맞을 때 알아서 쓴다
            _combat.TryUseHead();
        }
    }
}
