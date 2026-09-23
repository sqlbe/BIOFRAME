using System.Collections.Generic;
using UnityEngine;
using Bioframe.Rules;
using Bioframe.Assembly;
using Bioframe.Movement;

namespace Bioframe.Combat
{
    // 앞다리(FL) 파츠로 대상을 공격한다.
    // 대상 지정 방식: 적을 클릭하면 다가가 사거리 안에서 자동으로 때린다.
    public class FrameCombat : MonoBehaviour
    {
        class Weapon
        {
            public PartData part;
            public Transform visual;        // 박스 파츠. 휘두르는 동작에 쓴다
            public Quaternion restRotation;
            public float cooldown;
            public int comboIndex;
            public float comboWindow;
            public float swing;             // 0~1 동작 진행도
        }

        readonly List<Weapon> _weapons = new List<Weapon>();
        Weapon _head;               // 머리 파츠 (Q 키)
        FrameStats _stats;
        FrameController _controller;
        Vector3 _dashTo;
        float _dashTime;
        float _aim;                 // Q를 누른 뒤 대상을 클릭할 수 있는 시간
        public const float AimWindow = 0.5f;

        public bool AimingHead { get { return _aim > 0f; } }

        float _attackAim;           // A를 누른 뒤 대상을 클릭할 수 있는 시간
        public const float AttackAimWindow = 1.5f;
        public bool AimingAttack { get { return _attackAim > 0f; } }

        // 기술을 쏜 클릭이 버튼을 떼기 전까지 이동·대상 지정으로 새어 나가지 않게 막는다
        bool _blockClick;
        public bool SuppressClicks { get { return _blockClick; } }

        public Damageable Target { get; private set; }
        // 좌클릭으로 고른 대상만 다가가서 때린다.
        // Q로 지정한 원거리 기술 대상은 제자리에서 쓰고 움직이지 않는다.
        public bool AutoApproach { get; private set; }
        public bool HasTarget { get { return Target != null && Target.Alive; } }
        public Vector3 TargetPosition { get { return Target != null ? Target.transform.position : transform.position; } }
        public float AttackRange { get; private set; }
        public string LastLog { get; private set; }

        public void Init(List<PartData> parts, FrameVisual visual, FrameStats stats, FrameController controller)
        {
            _stats = stats;
            _controller = controller;
            _weapons.Clear();
            _head = null;
            AttackRange = 0f;

            for (int i = 0; i < parts.Count; i++)
            {
                var hp = parts[i];
                if (hp == null || hp.socket != "HD" || hp.ability == null) continue;
                _head = new Weapon();
                _head.part = hp;
                _head.visual = visual != null ? visual.GetAttachedPart("mount_HD_0") : null;
                if (_head.visual != null) _head.restRotation = _head.visual.localRotation;
                break;
            }

            int index = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null || p.socket != "FL" || p.ability == null) continue;

                var w = new Weapon();
                w.part = p;
                w.visual = visual != null ? visual.GetAttachedPart(index == 0 ? "mount_FL_L" : "mount_FL_R") : null;
                if (w.visual != null) w.restRotation = w.visual.localRotation;
                _weapons.Add(w);
                AttackRange = Mathf.Max(AttackRange, p.ability.range);
                index++;
            }
        }

        public void SetTarget(Damageable d) { SetTarget(d, true); }

        public void SetTarget(Damageable d, bool approach)
        {
            Target = d;
            AutoApproach = approach;
            if (d != null && approach) LastLog = d.displayName + " 지정";
        }

        public void ClearTarget() { Target = null; AutoApproach = false; }

        public string HeadName { get { return _head != null ? _head.part.name : "없음"; } }
        public float HeadCooldown { get { return _head != null ? Mathf.Max(0f, _head.cooldown) : 0f; } }

        void Update()
        {
            float dt = Time.deltaTime;

            // 덮치기 이동
            if (_dashTime > 0f)
            {
                _dashTime -= dt;
                transform.position = Vector3.MoveTowards(transform.position, _dashTo, 26f * dt);
            }

            if (_head != null)
            {
                if (_head.cooldown > 0f) _head.cooldown -= dt;
                if (_head.swing > 0f && _head.visual != null)
                {
                    _head.swing = Mathf.Max(0f, _head.swing - dt / 0.22f);
                    float hk = Mathf.Sin((1f - _head.swing) * Mathf.PI);
                    _head.visual.localRotation = _head.restRotation * Quaternion.Euler(35f * hk, 0f, 0f);
                }
            }

            if (_blockClick && !InputReader.ClickHeld) _blockClick = false;

            // A -> 대상을 클릭하면 다가가서 앞다리로 공격한다
            if (InputReader.AttackKeyPressed && _aim <= 0f)
            {
                _attackAim = AttackAimWindow;
                LastLog = "공격 대상 지정 — 적을 클릭";
            }
            else if (_attackAim > 0f)
            {
                _attackAim -= dt;
                if (InputReader.ClickPressed)
                {
                    var picked = PickDamageableUnderCursor();
                    _attackAim = 0f;
                    _blockClick = true;
                    if (picked != null) SetTarget(picked, true);
                    else LastLog = "적이 아니다. 취소";
                }
                else if (_attackAim <= 0f) LastLog = "공격 지정 시간 초과";
            }

            // Q -> 0.5초 안에 대상을 클릭하면 머리 파츠가 나간다
            if (InputReader.HeadPressed) BeginHeadAim();
            else if (_aim > 0f)
            {
                _aim -= dt;
                if (InputReader.ClickPressed)
                {
                    var picked = PickDamageableUnderCursor();
                    _aim = 0f;
                    _blockClick = true;
                    if (picked != null) { SetTarget(picked, false); UseHead(); }
                    else LastLog = "대상이 아니다. 취소";
                }
                else if (_aim <= 0f) LastLog = "조준 시간 초과";
            }

            for (int i = 0; i < _weapons.Count; i++)
            {
                var w = _weapons[i];
                if (w.cooldown > 0f) w.cooldown -= dt;
                if (w.comboWindow > 0f)
                {
                    w.comboWindow -= dt;
                    if (w.comboWindow <= 0f) w.comboIndex = 0;
                }

                // 휘두르는 동작
                if (w.swing > 0f && w.visual != null)
                {
                    w.swing = Mathf.Max(0f, w.swing - dt / 0.18f);
                    float k = Mathf.Sin((1f - w.swing) * Mathf.PI);
                    w.visual.localRotation = w.restRotation * Quaternion.Euler(-70f * k, 0f, 0f);
                }
            }

            if (!HasTarget) { Target = null; return; }

            Vector3 to = Target.transform.position - transform.position;
            float dist = to.magnitude;
            if (dist > AttackRange + 0.6f) return;

            Vector3 flat = Vector3.ProjectOnPlane(to, transform.up);
            if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, flat.normalized) > 70f) return;

            for (int i = 0; i < _weapons.Count; i++)
            {
                var w = _weapons[i];
                if (w.cooldown > 0f) continue;
                if (dist > w.part.ability.range + 0.6f) continue;
                Strike(w);
                break;      // 한 프레임에 한 팔만
            }
        }

        void BeginHeadAim()
        {
            if (_head == null) { LastLog = "머리 파츠가 없다"; return; }

            // 대기 중이어도 조준은 열어 둔다. 그래야 클릭이 이동으로 새지 않는다.
            _aim = AimWindow;
            LastLog = _head.cooldown > 0f
                ? _head.part.name + " 대기 " + _head.cooldown.ToString("0.0") + "초 — 아직 못 쏜다"
                : _head.part.name + " 조준 중 — 대상을 클릭";
        }

        Damageable PickDamageableUnderCursor()
        {
            var cam = Camera.main;
            if (cam == null) return null;

            Ray ray = cam.ScreenPointToRay(InputReader.MousePosition);
            var hits = Physics.RaycastAll(ray, 250f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Damageable found = null;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;
                var d = hits[i].transform.GetComponentInParent<Damageable>();
                if (d == null || !d.Alive) continue;
                if (hits[i].distance < best) { best = hits[i].distance; found = d; }
            }
            return found;
        }

        // 머리 파츠 발동
        void UseHead()
        {
            if (_head == null) { LastLog = "머리 파츠가 없다"; return; }

            var a = _head.part.ability;
            if (_head.cooldown > 0f) { LastLog = _head.part.name + " 대기 " + _head.cooldown.ToString("0.0") + "초"; return; }
            if (!HasTarget) { LastLog = "대상을 먼저 클릭"; return; }

            float dist = Vector3.Distance(transform.position, Target.transform.position);
            if (dist > a.range) { LastLog = _head.part.name + " 사거리 밖 (" + a.range.ToString("0") + "m)"; return; }

            if (a.requiresSprint && (_controller == null || !_controller.Sprinting))
            {
                LastLog = _head.part.name + "은 질주 중에만 쓸 수 있다";
                return;
            }

            if (_controller != null && !_controller.TrySpendEnergy(a.en))
            {
                LastLog = "EN 부족 (" + a.en.ToString("0") + " 필요)";
                return;
            }

            // 대상 쪽으로 얼굴을 돌린 뒤 발동한다
            if (_controller != null) _controller.FaceTowards(Target.transform.position);

            // 덮치기: 대상 앞까지 순간적으로 뛰어든다
            if (a.action == "POUNCE")
            {
                Vector3 dir = (transform.position - Target.transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
                _dashTo = Target.transform.position + dir.normalized * 1.6f;
                _dashTime = 0.25f;
            }

            float dealt = 0f;
            if (a.damage > 0f) dealt = Target.ApplyDamage(a.damage, transform.position, Mathf.Clamp01(a.armorIgnore));

            Target.ApplyStatus(transform.position, a.rootSeconds, a.dotDps, a.dotDuration, a.armorShred, a.armorShredDuration);

            if (a.selfRootSeconds > 0f && _controller != null) _controller.ApplyControlLock(a.selfRootSeconds);

            _head.swing = 1f;
            _head.cooldown = a.cooldown;
            LastLog = _head.part.name + " " + a.action + (dealt > 0f ? " → " + dealt.ToString("0") + " 피해" : " 사용");
        }

        void Strike(Weapon w)
        {
            var a = w.part.ability;
            float speedMul = w.part.drawback != null ? Mathf.Max(0.2f, w.part.drawback.attackSpeedMul) : 1f;

            float dealt = Target.ApplyDamage(a.damage, transform.position, Mathf.Clamp01(a.armorIgnore));
            LastLog = w.part.name + " " + a.action + " → " + dealt.ToString("0") + " 피해";

            w.swing = 1f;
            w.comboIndex++;
            w.comboWindow = 1.2f;

            // 연속 공격(사마귀 낫 3연타)은 짧은 간격, 마지막 타 뒤에 본래 대기시간
            bool comboContinues = a.combo > 1 && w.comboIndex < a.combo;
            w.cooldown = comboContinues ? (a.cooldown * 0.4f) / speedMul : a.cooldown / speedMul;
            if (!comboContinues) w.comboIndex = 0;
        }
    }
}
