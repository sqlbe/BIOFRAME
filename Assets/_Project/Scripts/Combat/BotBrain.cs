using UnityEngine;
using Bioframe.Movement;

namespace Bioframe.Combat
{
    public enum BotTactic
    {
        Melee,      // 붙어서 때린다
        Ranged,     // 거리를 유지하며 쏜다
        Climber,    // 벽을 타고 위에서 덮친다
        Stealth,    // 투명으로 접근해 기습한다
        Turtle      // 느리고 단단하다. 자리를 지킨다
    }

    // 봇의 판단. 자기가 단 파츠를 읽고 전술을 고른다.
    // 플레이어와 같은 조립·이동·전투 시스템을 쓴다.
    public class BotBrain : MonoBehaviour
    {
        public Damageable enemy;
        public float reactionTime = 0.15f;
        public float senseRange = 6f;        // 투명한 상대를 알아채는 거리

        public BotTactic Tactic { get; private set; }

        FrameController _ctrl;
        FrameCombat _combat;
        SurfaceMotor _motor;
        Damageable _self;

        float _strafeTimer;
        float _strafeDir = 1f;
        float _think;
        float _jumpCooldown;
        float _climbTimer;
        Vector3 _wallPoint;
        bool _hasWallPoint;

        void Awake()
        {
            _ctrl = GetComponent<FrameController>();
            _combat = GetComponent<FrameCombat>();
            _motor = GetComponent<SurfaceMotor>();
            _self = GetComponent<Damageable>();
        }

        void Start() { DecideTactic(); }

        // 파츠 구성을 보고 싸우는 방식을 정한다
        public void DecideTactic()
        {
            if (_ctrl == null || _combat == null) return;

            var stats = _ctrl.Stats;
            float range = _combat.PreferredRange;

            if (_combat.HasCloak) Tactic = BotTactic.Stealth;
            else if (stats.wallClimb) Tactic = BotTactic.Climber;
            else if (range >= 9f) Tactic = BotTactic.Ranged;
            else if (stats.speed < 5f && stats.armor != null && stats.armor.front >= 60f) Tactic = BotTactic.Turtle;
            else Tactic = BotTactic.Melee;
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
                _ctrl.botFace = enemy.transform.position;
                _ctrl.botHasFace = true;
                _combat.SetTarget(enemy, false);
                return;
            }

            _think -= dt;
            if (_think > 0f) return;
            _think = reactionTime;

            // 상대가 투명이면 가까이 오기 전까지 못 본다
            var enemyCombat = enemy.GetComponent<FrameCombat>();
            float dist3d = Vector3.Distance(transform.position, enemy.transform.position);
            if (enemyCombat != null && enemyCombat.Cloaked && dist3d > senseRange)
            {
                _ctrl.botWish = Vector3.zero;
                _ctrl.botHasFace = false;      // 놓쳤으면 가는 방향을 본다
                _combat.ClearTarget();
                return;
            }

            _combat.SetTarget(enemy, false);
            _ctrl.botFace = enemy.transform.position;   // 싸우는 동안은 늘 상대를 본다
            _ctrl.botHasFace = true;

            Vector3 up = _motor.Up;
            Vector3 to = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, up);
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;

            _strafeTimer -= reactionTime;
            if (_strafeTimer <= 0f)
            {
                _strafeTimer = Random.Range(1.0f, 2.5f);
                _strafeDir = Random.value < 0.5f ? -1f : 1f;
            }
            Vector3 side = Vector3.Cross(up, dir).normalized * _strafeDir;

            switch (Tactic)
            {
                case BotTactic.Ranged: ActRanged(dir, side, dist); break;
                case BotTactic.Climber: ActClimber(dir, side, dist); break;
                case BotTactic.Stealth: ActStealth(dir, side, dist); break;
                case BotTactic.Turtle: ActTurtle(dir, side, dist); break;
                default: ActMelee(dir, side, dist); break;
            }

            UseAbilities(dist);
            TryEscape(dir, dist);

            // 상대가 위에 있으면 뛴다
            float heightGap = Vector3.Dot(enemy.transform.position - transform.position, up);
            if (heightGap > 1.2f && dist < 10f && _jumpCooldown <= 0f && _motor.Grounded && !_motor.Attached)
            {
                _ctrl.botJump = true;
                _jumpCooldown = 2f;
            }
        }

        void ActMelee(Vector3 dir, Vector3 side, float dist)
        {
            float range = Mathf.Max(2.2f, _combat.AttackRange);
            if (dist > range * 0.95f)
            {
                _ctrl.botWish = dir + side * 0.25f;
                _ctrl.botSprint = dist > range * 2.2f;
            }
            else if (dist < range * 0.55f) _ctrl.botWish = -dir * 0.7f + side * 0.6f;
            else
            {
                _ctrl.botWish = side * 0.8f;
            }
        }

        // 자기 사거리의 70% 근처를 유지한다. 붙으면 물러난다.
        void ActRanged(Vector3 dir, Vector3 side, float dist)
        {
            float range = _combat.PreferredRange;
            float keep = range * 0.7f;

            if (dist > keep * 1.15f)
            {
                _ctrl.botWish = dir * 0.9f + side * 0.2f;
                _ctrl.botSprint = dist > range * 1.4f;
            }
            else if (dist < keep * 0.6f)
            {
                _ctrl.botWish = -dir * 1.0f + side * 0.5f;   // 붙으면 뒷걸음질
                _ctrl.botSprint = true;
            }
            else
            {
                _ctrl.botWish = side * 0.7f;
                _ctrl.botSprint = false;
            }
        }

        // 벽을 타고 올라가 위에서 접근한다
        void ActClimber(Vector3 dir, Vector3 side, float dist)
        {
            float range = Mathf.Max(2.2f, _combat.AttackRange);

            if (_motor.Attached)
            {
                // 벽에 붙은 채 상대 쪽으로 가다가, 충분히 가까우면 떨어져 덮친다
                if (dist < range * 1.6f)
                {
                    _motor.Detach(2f);
                    _ctrl.botWish = dir;
                }
                else _ctrl.botWish = dir * 0.9f;
                return;
            }

            _climbTimer -= reactionTime;
            if (dist > range * 2.5f)
            {
                if (!_hasWallPoint || _climbTimer <= 0f)
                {
                    _hasWallPoint = FindWall(out _wallPoint);
                    _climbTimer = 3f;
                }

                if (_hasWallPoint)
                {
                    Vector3 toWall = Vector3.ProjectOnPlane(_wallPoint - transform.position, _motor.Up);
                    if (toWall.magnitude > 1.2f) { _ctrl.botWish = toWall.normalized; return; }
                    _hasWallPoint = false;   // 벽에 닿으면 모터가 알아서 붙는다
                }
            }

            ActMelee(dir, side, dist);
        }

        // 투명으로 붙었다가 기습한다
        void ActStealth(Vector3 dir, Vector3 side, float dist)
        {
            float range = Mathf.Max(2.2f, _combat.AttackRange);

            if (dist > range * 1.2f && !_combat.Cloaked) _combat.TryUseSlot("SK");
            if (dist > range * 0.95f)
            {
                _ctrl.botWish = dir;
                _ctrl.botSprint = !_combat.Cloaked && dist > range * 3f;   // 투명 중에는 천천히
            }
            else _ctrl.botWish = side * 0.6f;
        }

        // 느리고 단단하다. 멀리 쫓지 않는다.
        void ActTurtle(Vector3 dir, Vector3 side, float dist)
        {
            float range = Mathf.Max(2.2f, _combat.AttackRange);
            if (dist > 12f) { _ctrl.botWish = Vector3.zero; return; }

            _ctrl.botWish = dist > range * 0.9f ? dir * 0.8f : Vector3.zero;
            _ctrl.botSprint = false;
        }

        void UseAbilities(float dist)
        {
            _combat.TryUseHead();
            if (dist < _combat.SlotRange("TL") + 0.5f) _combat.TryUseSlot("TL");
            if (_combat.SlotAction("DS") == "DRONE") _combat.TryUseSlot("DS");
        }

        // 몰리면 뒤로 분사하고 달아난다
        void TryEscape(Vector3 dir, float dist)
        {
            if (!_combat.HasSpray) return;
            if (_self == null || _self.Hp > _self.maxHp * 0.45f) return;
            if (dist > _combat.SlotRange("DS")) return;

            _ctrl.botFace = transform.position - dir * 6f;   // 등을 상대 쪽으로 돌린다
            _combat.TryUseSlot("DS");
            _ctrl.botWish = -dir;
            _ctrl.botSprint = true;
        }

        // 주위에서 올라갈 만한 벽을 찾는다
        bool FindWall(out Vector3 point)
        {
            point = Vector3.zero;
            Vector3 origin = transform.position + Vector3.up * 0.8f;
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                RaycastHit hit;
                if (!Physics.Raycast(origin, d, out hit, 16f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.transform.IsChildOf(transform)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) < 60f) continue;   // 벽이 아니면 통과

                if (hit.distance < best) { best = hit.distance; point = hit.point; found = true; }
            }
            return found;
        }
    }
}
