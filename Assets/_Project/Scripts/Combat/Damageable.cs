using System.Collections.Generic;
using UnityEngine;
using Bioframe.Rules;

namespace Bioframe.Combat
{
    // 피해를 받는 대상. 규격서 §5의 방향별 장갑 공식을 그대로 쓴다.
    public class Damageable : MonoBehaviour
    {
        public string displayName = "대상";
        public string subtitle;            // 장착 파츠 요약. 이름표 아래에 표시한다
        public float maxHp = 800f;
        public ArmorData armor = new ArmorData { front = 60f, side = 30f, rear = 10f, top = 25f };

        public float Hp { get; private set; }
        public bool survivesLethal;      // 도마뱀 자절 꼬리
        public float knockbackResist;    // 0~1. 클수록 덜 밀린다
        bool _lethalUsed;
        public bool Alive { get { return Hp > 0f; } }

        public System.Action<Damageable> onDeath;

        struct Popup
        {
            public string text;
            public Vector3 world;
            public float life;
            public Color color;
        }

        readonly List<Popup> _popups = new List<Popup>();

        // 상태 효과
        float _rootUntil;                  // 속박이 끝나는 시각
        float _dotDps, _dotTime, _dotTick; // 지속 피해
        float _shred, _shredTime;          // 장갑 깎기 비율
        Vector3 _lastAttacker;

        public bool Rooted { get { return Time.time < _rootUntil; } }
        public float RootRemain { get { return Mathf.Max(0f, _rootUntil - Time.time); } }
        public float ArmorShred { get { return _shredTime > 0f ? _shred : 0f; } }
        public string StatusText
        {
            get
            {
                string t = "";
                if (Rooted) t += "속박 " + RootRemain.ToString("0.0") + "s ";
                if (_dotTime > 0f) t += "지속피해 ";
                if (_shredTime > 0f) t += "장갑-" + (_shred * 100f).ToString("0") + "% ";
                return t;
            }
        }

        void Awake() { Hp = maxHp; }

        public void ResetHp()
        {
            Hp = maxHp;
            _rootUntil = 0f; _dotTime = 0f; _dotDps = 0f; _shredTime = 0f; _shred = 0f;
            _lethalUsed = false;
        }

        // attackerPos 기준으로 어느 면을 맞았는지 판정한다
        public string HitDirection(Vector3 attackerPos)
        {
            Vector3 to = attackerPos - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return "top";

            float angle = Vector3.Angle(transform.forward, to.normalized);
            if (angle < 60f) return "front";
            if (angle > 120f) return "rear";
            return "side";
        }

        // 상태 효과를 건다. 지속 피해와 장갑 깎기는 더 강한 쪽으로 덮어쓴다.
        public void ApplyStatus(Vector3 attackerPos, float rootSeconds, float dotDps, float dotDuration,
                                float armorShred, float shredDuration)
        {
            if (!Alive) return;
            _lastAttacker = attackerPos;

            if (rootSeconds > 0f)
            {
                _rootUntil = Mathf.Max(_rootUntil, Time.time + rootSeconds);
                AddPopup("속박", new Color(0.10f, 0.35f, 0.55f));
                Fx.Bind(transform, rootSeconds, new Color(0.95f, 0.96f, 1f));
                Fx.Ring(transform.position + Vector3.up * 0.1f, Vector3.up, new Color(0.85f, 0.9f, 1f), 1.8f, 0.4f);
            }
            if (dotDps > 0f && dotDuration > 0f)
            {
                _dotDps = Mathf.Max(_dotDps, dotDps);
                _dotTime = Mathf.Max(_dotTime, dotDuration);
            }
            if (armorShred > 0f && shredDuration > 0f)
            {
                _shred = Mathf.Clamp(Mathf.Max(_shred, armorShred), 0f, 0.8f);
                _shredTime = Mathf.Max(_shredTime, shredDuration);
                AddPopup("장갑 -" + (armorShred * 100f).ToString("0") + "%", new Color(0.55f, 0.30f, 0.05f));
            }
        }

        void AddPopup(string text, Color color)
        {
            var p = new Popup();
            p.text = text;
            p.world = transform.position + Vector3.up * 2.4f + Random.insideUnitSphere * 0.25f;
            p.life = 1.1f;
            p.color = color;
            _popups.Add(p);
        }

        public float ApplyDamage(float raw, Vector3 attackerPos, float armorIgnore = 0f)
        {
            if (!Alive) return 0f;

            _lastAttacker = attackerPos;
            string dir = HitDirection(attackerPos);
            float effectiveArmor = armor.Get(dir) * (1f - ArmorShred);
            float dealt = BuildStats.DamageAfterArmor(raw, effectiveArmor, armorIgnore);
            Hp = Mathf.Max(0f, Hp - dealt);
            if (Hp <= 0f && survivesLethal && !_lethalUsed)
            {
                _lethalUsed = true;
                Hp = 1f;
                AddPopup("자절!", new Color(0.10f, 0.50f, 0.42f));
                Fx.Ring(transform.position + Vector3.up * 0.2f, Vector3.up, new Color(0.35f, 0.85f, 0.75f), 2.2f, 0.4f);
            }

            // 연출: 번쩍임, 파편, 밀려나기, 내가 맞았으면 화면 흔들림
            Vector3 hitPoint = transform.position + Vector3.up * 1.2f;
            Vector3 away = (transform.position - attackerPos);
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = transform.forward;
            away.Normalize();

            Fx.Flash(gameObject, new Color(1f, 0.92f, 0.85f), 0.14f);
            Fx.Spark(hitPoint, away * 0.5f + Vector3.up * 0.5f, new Color(0.95f, 0.75f, 0.35f),
                     Mathf.Clamp(Mathf.RoundToInt(dealt / 8f), 4, 12), 3f + dealt * 0.03f);

            var motor = GetComponent<Bioframe.Movement.SurfaceMotor>();
            if (motor != null)
                motor.Nudge(away, Mathf.Clamp(dealt * 0.004f, 0.03f, 0.25f) * Mathf.Clamp01(1f - knockbackResist));

            if (Fx.IsCameraTarget(transform)) Fx.Shake(Mathf.Clamp(dealt * 0.004f, 0.05f, 0.35f), 0.18f);

            var p = new Popup();
            p.text = dealt.ToString("0") + "  " + DirName(dir);
            p.world = transform.position + Vector3.up * 2.2f + Random.insideUnitSphere * 0.3f;
            p.life = 1.1f;
            p.color = dir == "rear" ? new Color(0.70f, 0.18f, 0.12f) : new Color(0.12f, 0.16f, 0.14f);
            _popups.Add(p);

            if (!Alive && onDeath != null) onDeath(this);
            return dealt;
        }

        static string DirName(string dir)
        {
            switch (dir)
            {
                case "rear": return "후면";
                case "side": return "측면";
                case "top": return "상부";
                default: return "정면";
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_shredTime > 0f) _shredTime -= dt;

            // 지속 피해는 0.5초마다 한 번씩 들어간다
            if (_dotTime > 0f && Alive)
            {
                _dotTime -= dt;
                _dotTick -= dt;
                if (_dotTick <= 0f)
                {
                    _dotTick = 0.5f;
                    float tickDamage = _dotDps * 0.5f;
                    Hp = Mathf.Max(0f, Hp - tickDamage);
                    AddPopup(tickDamage.ToString("0"), new Color(0.35f, 0.45f, 0.20f));
                    if (!Alive && onDeath != null) onDeath(this);
                }
                if (_dotTime <= 0f) _dotDps = 0f;
            }

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var p = _popups[i];
                p.life -= Time.deltaTime;
                p.world += Vector3.up * (0.9f * Time.deltaTime);
                if (p.life <= 0f) _popups.RemoveAt(i);
                else _popups[i] = p;
            }
        }

        void OnGUI()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // 체력 막대
            if (Alive)
            {
                Vector3 head = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.9f);
                if (head.z > 0f)
                {
                    float w = 70f, h = 7f;
                    var back = new Rect(head.x - w * 0.5f, Screen.height - head.y, w, h);
                    var prev = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.25f);
                    GUI.DrawTexture(back, Texture2D.whiteTexture);
                    GUI.color = new Color(0.72f, 0.24f, 0.18f, 0.95f);
                    GUI.DrawTexture(new Rect(back.x, back.y, w * (Hp / maxHp), h), Texture2D.whiteTexture);
                    GUI.color = prev;

                    var nameStyle = new GUIStyle(GUI.skin.label);
                    nameStyle.fontSize = 12;
                    nameStyle.alignment = TextAnchor.UpperCenter;
                    nameStyle.normal.textColor = new Color(0.10f, 0.14f, 0.12f);
                    GUI.Label(new Rect(back.x - 60f, back.y + 8f, w + 120f, 16f), displayName, nameStyle);

                    if (!string.IsNullOrEmpty(subtitle))
                    {
                        var subStyle = new GUIStyle(nameStyle);
                        subStyle.fontSize = 10;
                        subStyle.normal.textColor = new Color(0.36f, 0.42f, 0.40f);
                        GUI.Label(new Rect(back.x - 90f, back.y + 22f, w + 180f, 14f), subtitle, subStyle);
                    }

                    string st = StatusText;
                    if (!string.IsNullOrEmpty(st))
                    {
                        var stStyle = new GUIStyle(GUI.skin.label);
                        stStyle.fontSize = 12;
                        stStyle.fontStyle = FontStyle.Bold;
                        stStyle.alignment = TextAnchor.UpperCenter;
                        stStyle.normal.textColor = new Color(0.08f, 0.30f, 0.50f);
                        GUI.Label(new Rect(back.x - 30f, back.y - 18f, w + 60f, 18f), st, stStyle);
                    }
                }
            }

            // 피해 숫자
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = 15;
            style.fontStyle = FontStyle.Bold;
            for (int i = 0; i < _popups.Count; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(_popups[i].world);
                if (sp.z <= 0f) continue;
                style.normal.textColor = _popups[i].color;
                GUI.Label(new Rect(sp.x - 40f, Screen.height - sp.y, 90f, 22f), _popups[i].text, style);
            }
        }
    }
}
