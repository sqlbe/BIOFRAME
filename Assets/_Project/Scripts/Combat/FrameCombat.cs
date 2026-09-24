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
            public TrailRenderer trail;     // 휘두를 때 남는 잔상
        }

        readonly List<Weapon> _weapons = new List<Weapon>();
        Damageable _self;
        Weapon _head;               // 머리 파츠 (Q 키)
        Weapon _back, _tail, _skin; // 등(E), 꼬리(R), 외피(F)
        public bool Cloaked { get; private set; }
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
        string _lastLog;
        public readonly List<string> LogLines = new List<string>();

        public string LastLog
        {
            get { return _lastLog; }
            private set
            {
                if (string.IsNullOrEmpty(value) || value == _lastLog) { _lastLog = value; return; }
                _lastLog = value;
                LogLines.Add(value);
                if (LogLines.Count > 6) LogLines.RemoveAt(0);
            }
        }

        public void Init(List<PartData> parts, FrameVisual visual, FrameStats stats, FrameController controller)
        {
            _self = GetComponent<Damageable>();
            _stats = stats;
            _controller = controller;
            _weapons.Clear();
            _head = null;
            AttackRange = 0f;

            _back = MakeSlot(parts, "DS", visual, "mount_DS_0");
            _tail = MakeSlot(parts, "TL", visual, "mount_TL_0");
            _skin = MakeSlot(parts, "SK", visual, null);
            Cloaked = false;

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
                if (w.visual != null)
                {
                    w.restRotation = w.visual.localRotation;
                    w.trail = MakeTrail(w.visual, TrailColorFor(p));
                }
                _weapons.Add(w);
                AttackRange = Mathf.Max(AttackRange, p.ability.range);
                index++;
            }
        }

        // 파츠 성격에 따라 궤적 색을 고른다. 나중에 파츠 데이터로 옮기면 된다.
        static Color TrailColorFor(PartData p)
        {
            // 파츠 데이터의 색을 그대로 쓰되, 궤적은 조금 더 밝게 한다
            Color c = FrameBuilder.ColorOf(p.color, new Color(0.75f, 0.95f, 0.9f));
            return Color.Lerp(c, Color.white, 0.35f);
        }

        static TrailRenderer MakeTrail(Transform on, Color color)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(on, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.5f);   // 무기 끝
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.18f;
            tr.startWidth = 0.22f;
            tr.endWidth = 0.02f;
            tr.minVertexDistance = 0.02f;
            tr.sharedMaterial = Fx.UnlitMaterial(color);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.emitting = false;
            return tr;
        }

        public void SetTarget(Damageable d) { SetTarget(d, true); }

        public void SetTarget(Damageable d, bool approach)
        {
            Target = d;
            AutoApproach = approach;
            if (d != null && approach) LastLog = d.displayName + " 지정";
        }

        public void ClearTarget() { Target = null; AutoApproach = false; }

        static Weapon MakeSlot(List<PartData> parts, string socket, FrameVisual visual, string node)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null || p.socket != socket || p.ability == null) continue;
                var w = new Weapon();
                w.part = p;
                w.visual = (visual != null && node != null) ? visual.GetAttachedPart(node) : null;
                if (w.visual != null) w.restRotation = w.visual.localRotation;
                return w;
            }
            return null;
        }

        Weapon SlotOf(string socket)
        {
            if (socket == "DS") return _back;
            if (socket == "TL") return _tail;
            if (socket == "SK") return _skin;
            return _head;
        }

        public string SlotName(string socket)
        {
            var w = SlotOf(socket);
            return w != null ? w.part.name : "없음";
        }

        public float SlotCooldown(string socket)
        {
            var w = SlotOf(socket);
            return w != null ? Mathf.Max(0f, w.cooldown) : 0f;
        }

        // 봇이 자기 구성을 읽을 때 쓰는 정보
        public bool HasSlot(string socket) { return SlotOf(socket) != null; }

        public string SlotAction(string socket)
        {
            var w = SlotOf(socket);
            return (w != null && w.part.ability != null) ? w.part.ability.action : null;
        }

        public float SlotRange(string socket)
        {
            var w = SlotOf(socket);
            return (w != null && w.part.ability != null) ? w.part.ability.range : 0f;
        }

        public bool HasCloak { get { return SlotAction("SK") == "CLOAK"; } }
        public bool HasSpray { get { return SlotAction("DS") == "SPRAY"; } }

        // 이 기체가 싸우고 싶어 하는 거리
        public float PreferredRange
        {
            get
            {
                float r = AttackRange;
                r = Mathf.Max(r, RangedSlotRange("HD"));
                r = Mathf.Max(r, RangedSlotRange("DS"));
                r = Mathf.Max(r, RangedSlotRange("TL"));
                return r;
            }
        }

        // 날아가는 무기만 원거리로 친다.
        // 치타 송곳니처럼 사거리가 길어도 달려들어 쓰는 기술은 근접으로 본다.
        float RangedSlotRange(string socket)
        {
            var w = SlotOf(socket);
            if (w == null || w.part.ability == null) return 0f;
            var a = w.part.ability;
            bool ranged = a.projectileSpeed > 0f || a.action == "DRONE" || a.action == "WEB";
            return ranged ? a.range : 0f;
        }

        // 봇이 조건을 확인한 뒤 파츠를 쓴다
        public bool TryUseSlot(string socket)
        {
            var w = SlotOf(socket);
            if (w == null || w.part.ability == null) return false;
            var a = w.part.ability;

            if (w.cooldown > 0f) return false;
            if (_self != null && _self.Rooted) return false;
            if (_controller != null && _controller.Energy < a.en) return false;

            bool needsTarget = a.action != "SPRAY" && a.action != "CLOAK";
            if (needsTarget)
            {
                if (!HasTarget) return false;
                if (a.range > 0f && Vector3.Distance(transform.position, Target.transform.position) > a.range) return false;
            }
            return UseSlot(w);
        }

        // 화면에 기술 칸을 그릴 때 쓰는 정보
        public PartData SlotPart(string socket)
        {
            var w = SlotOf(socket);
            return w != null ? w.part : null;
        }

        public float SlotCooldownMax(string socket)
        {
            var w = SlotOf(socket);
            return w != null && w.part.ability != null ? w.part.ability.cooldown : 0f;
        }

        public string HeadName { get { return _head != null ? _head.part.name : "없음"; } }
        public float HeadCooldown { get { return _head != null ? Mathf.Max(0f, _head.cooldown) : 0f; } }

        void Update()
        {
            float dt = Time.deltaTime;

            // 덮치기 이동
            if (_dashTime > 0f)
            {
                _dashTime -= dt;
                Vector3 next = Vector3.MoveTowards(transform.position, _dashTo, 26f * dt);
                Vector3 delta = next - transform.position;
                RaycastHit blocked;
                // 지형을 뚫고 지나가지 않게 확인한다
                if (delta.sqrMagnitude > 0.0001f &&
                    Physics.SphereCast(transform.position + Vector3.up * 0.7f, 0.45f, delta.normalized,
                                       out blocked, delta.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore)
                    && !blocked.transform.IsChildOf(transform)
                    && (Target == null || !blocked.transform.IsChildOf(Target.transform)))
                {
                    _dashTime = 0f;
                }
                else transform.position = next;

                if (_dashTime <= 0f)
                {
                    // 착지 충격파
                    Fx.Ring(transform.position + Vector3.up * 0.1f, Vector3.up, new Color(0.95f, 0.75f, 0.4f), 2.6f, 0.28f);
                    Fx.Dust(transform.position, transform.forward, new Color(0.86f, 0.82f, 0.72f), 8, 2.8f);
                }
            }

            TickSlot(_back, dt);
            TickSlot(_tail, dt);
            TickSlot(_skin, dt);

            // 투명 유지: EN을 계속 쓴다
            if (Cloaked)
            {
                float up = _skin != null ? _skin.part.ability.upkeep : 0f;
                if (_controller == null || !_controller.TrySpendEnergy(up * dt)) SetCloak(false);
            }

            if (InputReader.BackPressed) UseSlot(_back);
            if (InputReader.TailPressed) UseSlot(_tail);
            if (InputReader.SkinPressed) UseSlot(_skin);

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

                    // 적을 정확히 찍지 않아도, 근처에 있는 적을 잡아 공격한다
                    if (picked == null) picked = NearestEnemy(35f);

                    if (picked != null) SetTarget(picked, true);
                    else LastLog = "근처에 적이 없다";
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
                    else
                    {
                        // 빈 곳을 찍어도 그 방향으로 쏜다. 빗나가면 그냥 빗나간다.
                        Vector3 point;
                        if (_head != null && _head.part.ability.projectileSpeed > 0f && PickPointUnderCursor(out point))
                            UseHeadAt(point);
                        else LastLog = "대상이 아니다. 취소";
                    }
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
                    if (w.trail != null) w.trail.emitting = true;
                }
                else if (w.trail != null && w.trail.emitting) w.trail.emitting = false;
            }

            if (!HasTarget) { Target = null; return; }

            // 속박당하면 움직이지도 때리지도 못한다
            if (_self == null) _self = GetComponent<Damageable>();
            if (_self != null && _self.Rooted) return;

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

        static void TickSlot(Weapon w, float dt)
        {
            if (w == null) return;
            if (w.cooldown > 0f) w.cooldown -= dt;
            if (w.swing > 0f && w.visual != null)
            {
                w.swing = Mathf.Max(0f, w.swing - dt / 0.22f);
                float k = Mathf.Sin((1f - w.swing) * Mathf.PI);
                w.visual.localRotation = w.restRotation * Quaternion.Euler(30f * k, 0f, 0f);
            }
        }

        public void SetCloak(bool on)
        {
            if (Cloaked == on) return;
            Cloaked = on;
            if (on) { Fx.Tint(gameObject, new Color(0.88f, 0.94f, 0.96f)); LastLog = "투명 켜짐"; }
            else { Fx.ClearTint(gameObject); LastLog = "투명 꺼짐"; }
        }

        // 등, 꼬리, 외피 파츠 사용
        bool UseSlot(Weapon w)
        {
            if (w == null) return false;
            var a = w.part.ability;

            if (_self != null && _self.Rooted) { LastLog = "속박 중"; return false; }
            if (w.cooldown > 0f) { LastLog = w.part.name + " 대기 " + w.cooldown.ToString("0.0") + "초"; return false; }

            if (a.action == "CLOAK")
            {
                if (Cloaked) { SetCloak(false); w.cooldown = a.cooldown; return true; }
                if (_controller != null && !_controller.TrySpendEnergy(a.en)) { LastLog = "EN 부족"; return false; }
                SetCloak(true);
                w.cooldown = a.cooldown;
                return true;
            }

            if (_controller != null && !_controller.TrySpendEnergy(a.en)) { LastLog = "EN 부족 (" + a.en.ToString("0") + ")"; return false; }

            w.swing = 1f;
            w.cooldown = a.cooldown;
            if (Cloaked) SetCloak(false);   // 공격하면 투명이 풀린다

            if (a.action == "SPRAY") { Spray(w); return true; }
            if (a.action == "DRONE") { Drones(w); return true; }

            if (!HasTarget) { LastLog = w.part.name + ": 대상이 없다"; return false; }
            if (Vector3.Distance(transform.position, Target.transform.position) > a.range)
            { LastLog = w.part.name + " 사거리 밖"; return false; }

            if (_controller != null) _controller.FaceTowards(Target.transform.position);
            float dealt = Target.ApplyDamage(a.damage, transform.position, Mathf.Clamp01(a.armorIgnore));
            Target.ApplyStatus(transform.position, a.rootSeconds, a.dotDps, a.dotDuration, a.armorShred, a.armorShredDuration);
            Fx.Ring(Target.transform.position + Vector3.up * 1.0f, Vector3.up, TrailColorFor(w.part), 1.0f, 0.22f);
            LastLog = w.part.name + " -> " + dealt.ToString("0") + " 피해";
            return true;
        }

        // 뒤쪽으로 분사. 쫓아오는 상대를 떼어내는 용도.
        void Spray(Weapon w)
        {
            var a = w.part.ability;
            Vector3 origin = transform.position + transform.up * 0.8f;
            Vector3 dir = -transform.forward;
            float half = Mathf.Max(20f, a.coneAngle) * 0.5f;
            int hitCount = 0;

            var cols = Physics.OverlapSphere(origin, a.range, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].transform.IsChildOf(transform)) continue;
                var d = cols[i].GetComponentInParent<Damageable>();
                if (d == null || !d.Alive) continue;

                Vector3 to = d.transform.position - origin;
                if (Vector3.Angle(dir, to) > half) continue;

                d.ApplyDamage(a.damage, transform.position, Mathf.Clamp01(a.armorIgnore));
                d.ApplyStatus(transform.position, a.rootSeconds, a.dotDps, a.dotDuration, a.armorShred, a.armorShredDuration);
                hitCount++;
            }

            for (int i = 0; i < 5; i++)
                Fx.Spark(origin + dir * (0.8f + i * 0.7f), dir + Vector3.up * 0.3f, new Color(0.95f, 0.45f, 0.18f), 5, 4f);
            Fx.Ring(origin + dir * 1.2f, dir, new Color(0.95f, 0.5f, 0.2f), a.range * 0.6f, 0.3f);
            if (Fx.IsCameraTarget(transform)) Fx.Shake(0.2f, 0.15f);

            LastLog = w.part.name + " 분사 -> " + hitCount + "명 적중";
        }

        // 작은 드론을 여러 발 날린다
        void Drones(Weapon w)
        {
            var a = w.part.ability;
            if (!HasTarget) { LastLog = w.part.name + ": 대상이 없다"; return; }

            Vector3 origin = transform.position + transform.up * 1.2f;
            int count = Mathf.Max(1, a.projectileCount);

            for (int i = 0; i < count; i++)
            {
                Vector3 spread = Random.insideUnitSphere * 0.6f;
                Projectile.Spawn(transform, origin + spread, Target, a.projectileSpeed * Random.Range(0.85f, 1.15f), 1.2f,
                                 FrameBuilder.ColorOf(w.part.color, Color.yellow), 0.05f,
                                 (hitTarget, result) =>
                                 {
                                     if (result != Projectile.Result.Hit || hitTarget == null) return;
                                     hitTarget.ApplyDamage(a.damage, transform.position, Mathf.Clamp01(a.armorIgnore));
                                 });
            }
            LastLog = w.part.name + " 드론 " + count + "기 발사";
        }

        void BeginHeadAim()
        {
            if (_self != null && _self.Rooted) { LastLog = "속박 중 — 아무것도 못 한다"; return; }
            if (_head == null) { LastLog = "머리 파츠가 없다"; return; }

            // 대기 중이어도 조준은 열어 둔다. 그래야 클릭이 이동으로 새지 않는다.
            _aim = AimWindow;
            LastLog = _head.cooldown > 0f
                ? _head.part.name + " 대기 " + _head.cooldown.ToString("0.0") + "초 — 아직 못 쏜다"
                : _head.part.name + " 조준 중 — 대상을 클릭";
        }

        bool PickPointUnderCursor(out Vector3 point)
        {
            point = Vector3.zero;
            var cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(InputReader.MousePosition);
            var hits = Physics.RaycastAll(ray, 250f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; point = hits[i].point + Vector3.up * 1.0f; found = true; }
            }
            return found;
        }

        // 대상 없이 한 지점을 향해 쏜다
        void UseHeadAt(Vector3 point)
        {
            if (_head == null) return;
            var a = _head.part.ability;

            if (_head.cooldown > 0f) { LastLog = _head.part.name + " 대기 " + _head.cooldown.ToString("0.0") + "초"; return; }
            if (_self != null && _self.Rooted) { LastLog = "속박 중"; return; }
            if (a.requiresSprint && (_controller == null || !_controller.Sprinting))
            { LastLog = _head.part.name + "은 질주 중에만"; return; }
            if (_controller != null && !_controller.TrySpendEnergy(a.en))
            { LastLog = "EN 부족 (" + a.en.ToString("0") + " 필요)"; return; }

            // 사거리보다 멀리 찍으면 사거리 끝까지만 날아간다
            Vector3 origin = transform.position + transform.up * 0.9f;
            Vector3 dir = point - origin;
            if (dir.magnitude > a.range) point = origin + dir.normalized * a.range;

            if (_controller != null) _controller.FaceTowards(point);

            _head.swing = 1f;
            _head.cooldown = a.cooldown;
            if (a.selfRootSeconds > 0f && _controller != null) _controller.ApplyControlLock(a.selfRootSeconds);

            string partName = _head.part.name;
            Vector3 muzzle = origin + (point - origin).normalized * 0.8f;
            Projectile.Spawn(transform, muzzle, point, a.projectileSpeed, 1.4f,
                             new Color(0.18f, 0.45f, 0.95f), 0.10f,
                             (hitTarget, result) =>
                             {
                                 if (result != Projectile.Result.Hit || hitTarget == null)
                                 {
                                     LastLog = partName + (result == Projectile.Result.Blocked ? " 막힘" : " 빗나감");
                                     return;
                                 }
                                 float d = hitTarget.ApplyDamage(a.damage, transform.position, Mathf.Clamp01(a.armorIgnore));
                                 hitTarget.ApplyStatus(transform.position, a.rootSeconds, a.dotDps, a.dotDuration,
                                                       a.armorShred, a.armorShredDuration);
                                 LastLog = partName + " 명중 → " + d.ToString("0") + " 피해";
                             });
            LastLog = partName + " 발사";
        }

        // 가장 가까운 적을 찾는다. A로 바닥을 찍었을 때 쓴다.
        Damageable NearestEnemy(float maxRange)
        {
            var all = FindObjectsByType<Damageable>(FindObjectsSortMode.None);
            Damageable best = null;
            float bestDist = maxRange;

            for (int i = 0; i < all.Length; i++)
            {
                var d = all[i];
                if (d == null || !d.Alive) continue;
                if (d.transform == transform || d.transform.IsChildOf(transform)) continue;

                float dist = Vector3.Distance(transform.position, d.transform.position);
                if (dist < bestDist) { bestDist = dist; best = d; }
            }
            return best;
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

        // 봇이 쓰는 진입점. 조건이 안 되면 그냥 아무 일도 안 한다.
        public bool TryUseHead()
        {
            if (_self != null && _self.Rooted) return false;
            if (_head == null || _head.cooldown > 0f || !HasTarget) return false;
            var a = _head.part.ability;
            if (Vector3.Distance(transform.position, Target.transform.position) > a.range) return false;
            if (a.requiresSprint && (_controller == null || !_controller.Sprinting)) return false;
            if (_controller != null && _controller.Energy < a.en) return false;
            UseHead();
            return true;
        }

        public float HeadRange { get { return _head != null ? _head.part.ability.range : 0f; } }

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
                Fx.Ring(transform.position + Vector3.up * 0.1f, Vector3.up, new Color(0.95f, 0.6f, 0.3f), 2.2f, 0.3f);
                Fx.Afterimage(gameObject, new Color(0.95f, 0.55f, 0.25f), 0.3f);
                Fx.Dust(transform.position, -transform.forward, new Color(0.86f, 0.82f, 0.72f), 6, 2.2f);
                if (Fx.IsCameraTarget(transform)) Fx.Shake(0.3f, 0.2f);
            }

            _head.swing = 1f;
            _head.cooldown = a.cooldown;
            if (a.selfRootSeconds > 0f && _controller != null) _controller.ApplyControlLock(a.selfRootSeconds);

            if (a.projectileSpeed > 0f)
            {
                // 날아가는 발사체. 쏜 순간의 위치로 향하므로 상대가 움직이면 빗나간다.
                string partName = _head.part.name;
                Vector3 aimDir = (Target.transform.position + Vector3.up * 1.0f) - (transform.position + transform.up * 0.9f);
                if (aimDir.sqrMagnitude < 0.001f) aimDir = transform.forward;
                Vector3 muzzle = transform.position + transform.up * 0.9f + aimDir.normalized * 0.8f;
                Projectile.Spawn(transform, muzzle, Target, a.projectileSpeed, 1.4f,
                                 new Color(0.18f, 0.45f, 0.95f), 0.10f,
                                 (hitTarget, result) =>
                                 {
                                     if (result == Projectile.Result.Blocked) { LastLog = partName + " 막힘"; return; }
                                     if (result == Projectile.Result.Miss || hitTarget == null) { LastLog = partName + " 빗나감"; return; }

                                     float d = hitTarget.ApplyDamage(a.damage, transform.position, Mathf.Clamp01(a.armorIgnore));
                                     hitTarget.ApplyStatus(transform.position, a.rootSeconds, a.dotDps, a.dotDuration,
                                                           a.armorShred, a.armorShredDuration);
                                     LastLog = partName + " 명중 → " + d.ToString("0") + " 피해";
                                 });
                LastLog = _head.part.name + " 발사";
                return;
            }

            float dealt = 0f;
            if (a.damage > 0f) dealt = Target.ApplyDamage(a.damage, transform.position, Mathf.Clamp01(a.armorIgnore));

            Target.ApplyStatus(transform.position, a.rootSeconds, a.dotDps, a.dotDuration, a.armorShred, a.armorShredDuration);

            LastLog = _head.part.name + " " + a.action + (dealt > 0f ? " → " + dealt.ToString("0") + " 피해" : " 사용");
        }

        void Strike(Weapon w)
        {
            var a = w.part.ability;
            float speedMul = w.part.drawback != null ? Mathf.Max(0.2f, w.part.drawback.attackSpeedMul) : 1f;

            float dealt = Target.ApplyDamage(a.damage, transform.position, Mathf.Clamp01(a.armorIgnore));
            LastLog = w.part.name + " " + a.action + " → " + dealt.ToString("0") + " 피해";

            Vector3 impact = Vector3.Lerp(transform.position, Target.transform.position, 0.75f) + Vector3.up * 1.1f;
            Fx.Ring(impact, (transform.position - Target.transform.position).normalized,
                    TrailColorFor(w.part), 0.9f, 0.22f);
            if (a.damage >= 80f && Fx.IsCameraTarget(transform)) Fx.Shake(0.22f, 0.16f);

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
