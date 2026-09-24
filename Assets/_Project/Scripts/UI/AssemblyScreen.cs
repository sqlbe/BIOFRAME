using System.Collections.Generic;
using UnityEngine;
using Bioframe.Rules;
using Bioframe.Assembly;
using Bioframe.Movement;

namespace Bioframe.UI
{
    // 조립 화면. Tab으로 열고 닫는다.
    // 규격서 §5~§6의 스탯 계산과 조립 규칙을 그대로 보여준다.
    public class AssemblyScreen : MonoBehaviour
    {
        public FrameSpawner spawner;
        public int costLimit = 30;

        static readonly string[] SlotOrder = { "LC", "FL_L", "FL_R", "HD", "DS", "TL", "SK" };
        static readonly string[] SlotLabel = { "이동계", "왼 앞다리", "오른 앞다리", "머리", "등", "꼬리", "외피" };

        public bool IsOpen { get; private set; }

        string _coreId = "INS";
        readonly Dictionary<string, string> _picked = new Dictionary<string, string>();
        Vector2 _scroll;
        int _slotIndex;

        // GUI를 그리는 도중에 내용을 바꾸면 Unity의 화면 계산이 어긋난다.
        // 클릭은 여기에 적어 두고 그리기가 끝난 뒤에 반영한다.
        string _pendingCore;
        int _pendingSlot = -1;
        string _pendingSlotKey, _pendingPartId;
        bool _pendingRemove, _pendingClearAll, _pendingApply, _pendingReset;
        int _pendingBotPreset = -1;

        int _swapLimit;                                  // 0이면 제한 없음
        readonly Dictionary<string, string> _baseline = new Dictionary<string, string>();

        // 라운드 사이에는 정해진 수만큼만 바꿀 수 있다 (기획서 §4)
        public void SetSwapLimit(int limit)
        {
            _swapLimit = limit;
            _baseline.Clear();
            if (limit > 0)
                foreach (var kv in _picked) _baseline[kv.Key] = kv.Value;
        }

        public int ChangedSlots
        {
            get
            {
                if (_swapLimit <= 0) return 0;
                int n = 0;
                foreach (var slot in SlotOrder)
                {
                    string a, b;
                    _picked.TryGetValue(slot, out a);
                    _baseline.TryGetValue(slot, out b);
                    if (a != b) n++;
                }
                return n;
            }
        }
        GUIStyle _label, _title, _small, _button, _buttonOn;
        Texture2D _texNormal, _texHover, _texOn, _texOnHover;

        void Start()
        {
            if (spawner == null) spawner = FindFirstObjectByType<FrameSpawner>();
            PartDatabase.Load();
            ReadFromSpawner();
        }

        void Update()
        {
            if (InputReader.AssemblyTogglePressed) Toggle();
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            Time.timeScale = IsOpen ? 0f : 1f;
            if (IsOpen) ReadFromSpawner();
        }

        // 지금 기체 구성을 화면에 반영한다
        void ReadFromSpawner()
        {
            if (spawner == null) return;
            _coreId = spawner.coreId;
            _picked.Clear();

            int flCount = 0;
            for (int i = 0; i < spawner.partIds.Count; i++)
            {
                var p = PartDatabase.GetPart(spawner.partIds[i]);
                if (p == null) continue;
                if (p.socket == "FL") { _picked[flCount == 0 ? "FL_L" : "FL_R"] = p.id; flCount++; }
                else _picked[p.socket] = p.id;
            }
        }

        List<string> BuildPartIds()
        {
            var list = new List<string>();
            for (int i = 0; i < SlotOrder.Length; i++)
            {
                string id;
                if (_picked.TryGetValue(SlotOrder[i], out id) && !string.IsNullOrEmpty(id)) list.Add(id);
            }
            return list;
        }

        List<PartData> BuildParts()
        {
            return PartDatabase.GetParts(BuildPartIds());
        }

        static string SocketOf(string slot)
        {
            return slot.StartsWith("FL") ? "FL" : slot;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label);
            _label.fontSize = 13;
            _label.richText = true;
            _label.normal.textColor = new Color(0.07f, 0.11f, 0.09f);

            _title = new GUIStyle(_label);
            _title.fontSize = 17;
            _title.fontStyle = FontStyle.Bold;

            _small = new GUIStyle(_label);
            _small.fontSize = 12;
            _small.normal.textColor = new Color(0.32f, 0.38f, 0.36f);
            _small.wordWrap = true;

            // Unity 기본 버튼은 어두운 회색이라 글자가 묻힌다. 밝은 버튼을 직접 만든다.
            _texNormal = MakeTex(new Color(0.95f, 0.96f, 0.95f));
            _texHover = MakeTex(new Color(0.88f, 0.92f, 0.90f));
            _texOn = MakeTex(new Color(0.82f, 0.91f, 0.89f));
            _texOnHover = MakeTex(new Color(0.76f, 0.88f, 0.86f));

            _button = new GUIStyle(GUI.skin.button);
            _button.fontSize = 13;
            _button.alignment = TextAnchor.MiddleLeft;
            _button.padding = new RectOffset(10, 8, 7, 7);
            _button.margin = new RectOffset(0, 0, 2, 2);
            _button.border = new RectOffset(0, 0, 0, 0);
            _button.richText = true;
            _button.wordWrap = false;
            SetButtonColors(_button, _texNormal, _texHover, new Color(0.10f, 0.14f, 0.12f));

            _buttonOn = new GUIStyle(_button);
            _buttonOn.fontStyle = FontStyle.Bold;
            SetButtonColors(_buttonOn, _texOn, _texOnHover, new Color(0.06f, 0.24f, 0.21f));
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        static void SetButtonColors(GUIStyle st, Texture2D normal, Texture2D hover, Color text)
        {
            st.normal.background = normal;
            st.hover.background = hover;
            st.active.background = hover;
            st.focused.background = normal;
            st.onNormal.background = normal;
            st.onHover.background = hover;
            st.onActive.background = hover;
            st.normal.textColor = text;
            st.hover.textColor = text;
            st.active.textColor = text;
            st.focused.textColor = text;
            st.onNormal.textColor = text;
            st.onHover.textColor = text;
            st.onActive.textColor = text;
        }

        GUIStyle Btn(bool selected) { return selected ? _buttonOn : _button; }

        void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();

            float w = Mathf.Min(1180f, Screen.width - 40f);
            float h = Mathf.Min(720f, Screen.height - 40f);
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.color = new Color(1f, 1f, 1f, 0.97f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.60f, 0.58f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));

            var match = FindFirstObjectByType<Bioframe.Match.MatchManager>();
            bool intermission = match != null && match.State == Bioframe.Match.MatchState.Intermission;

            GUILayout.BeginHorizontal();
            GUILayout.Label("기체 조립", _title, GUILayout.Width(120f));
            if (intermission)
            {
                GUILayout.Label("남은 시간 " + Mathf.CeilToInt(Mathf.Max(0f, match.Timer)) + "초", _label, GUILayout.Width(120f));
                if (GUILayout.Button("바로 시작", _buttonOn, GUILayout.Width(150f), GUILayout.Height(30f)))
                    _pendingApply = true;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Label(intermission
                ? "라운드 사이 조립 시간이다. 소켓을 바꾸고 [바로 시작]을 누르면 기다리지 않고 다음 라운드로 넘어간다."
                : "Tab을 다시 누르면 전투로 돌아간다. 고르는 즉시 오른쪽 수치가 바뀐다.", _small);
            GUILayout.Space(8f);

            float usable = panel.width - 36f - 36f;          // 좌우 여백과 칸 사이 간격
            float colCore = Mathf.Clamp(usable * 0.22f, 200f, 300f);
            float colSlot = Mathf.Clamp(usable * 0.24f, 200f, 320f);
            float colStat = Mathf.Clamp(usable * 0.26f, 230f, 340f);
            float colPart = Mathf.Max(200f, usable - colCore - colSlot - colStat);
            float colH = panel.height - 110f;

            GUILayout.BeginHorizontal();
            DrawCoreColumn(colCore, colH);
            GUILayout.Space(12f);
            DrawSlotColumn(colSlot, colH);
            GUILayout.Space(12f);
            DrawPartColumn(colPart, colH);
            GUILayout.Space(12f);
            DrawStatsColumn(colStat, colH);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            ApplyPending();
        }

        void ApplyPending()
        {
            if (!string.IsNullOrEmpty(_pendingCore))
            {
                _coreId = _pendingCore;
                _pendingCore = null;
                DropInvalidParts();
            }

            if (_pendingSlot >= 0)
            {
                _slotIndex = _pendingSlot;
                _pendingSlot = -1;
                _scroll = Vector2.zero;
            }

            if (_pendingClearAll)
            {
                _pendingClearAll = false;
                _picked.Clear();
            }

            if (_pendingRemove && !string.IsNullOrEmpty(_pendingSlotKey))
            {
                _picked.Remove(_pendingSlotKey);
                _pendingSlotKey = null;
                _pendingRemove = false;
            }

            if (!string.IsNullOrEmpty(_pendingSlotKey) && !string.IsNullOrEmpty(_pendingPartId))
            {
                _picked[_pendingSlotKey] = _pendingPartId;
                _pendingSlotKey = null;
                _pendingPartId = null;
            }

            if (_pendingBotPreset >= 0)
            {
                if (spawner != null) spawner.SetBotPreset(_pendingBotPreset);
                _pendingBotPreset = -1;
            }

            if (_pendingReset)
            {
                _pendingReset = false;
                if (spawner != null) { spawner.ResetBuild(); ReadFromSpawner(); }
            }

            if (_pendingApply)
            {
                _pendingApply = false;
                Apply();
            }
        }

        void DrawCoreColumn(float width, float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("<b>코어</b>", _label);

            foreach (var core in PartDatabase.Cores)
            {
                bool on = core.id == _coreId;
                string text = (on ? "▶ " : "    ") + core.name + "  (" + core.id + ")";
                if (GUILayout.Button(text, Btn(on))) _pendingCore = core.id;
                GUILayout.Label("   HP " + core.@base.hp.ToString("0") + " · 적재 " + core.@base.load.ToString("0")
                                + "kg · 속도 " + core.@base.spd.ToString("0.0"), _small);
            }
            GUILayout.Space(14f);
            DrawBotBlock();

            GUILayout.EndVertical();
        }

        // 연습 상대 고르기
        void DrawBotBlock()
        {
            if (spawner == null) return;

            GUILayout.Label("<b>연습 상대</b>", _label);
            GUILayout.Label("상대 구성에 따라 봇이 전술을 스스로 고른다.", _small);

            for (int i = 0; i < spawner.BotPresetCount; i++)
            {
                bool on = spawner.BotPresetIndex == i;
                if (GUILayout.Button((on ? "▶ " : "    ") + spawner.BotPresetNameAt(i), Btn(on)))
                    _pendingBotPreset = i;
                GUILayout.Label("   " + spawner.BotPresetPartsAt(i), _small);
            }
        }

        // 코어에 없는 소켓의 파츠는 자동으로 뺀다
        void DropInvalidParts()
        {
            var core = PartDatabase.GetCore(_coreId);
            if (core == null) return;

            var remove = new List<string>();
            foreach (var kv in _picked)
            {
                string socket = SocketOf(kv.Key);
                bool has = false;
                for (int i = 0; i < core.sockets.Count; i++)
                    if (core.sockets[i].type == socket) { has = true; break; }
                if (!has) remove.Add(kv.Key);
            }
            for (int i = 0; i < remove.Count; i++) _picked.Remove(remove[i]);
        }

        void DrawSlotColumn(float width, float height)
        {
            var core = PartDatabase.GetCore(_coreId);

            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("<b>소켓</b>", _label);

            for (int i = 0; i < SlotOrder.Length; i++)
            {
                string slot = SlotOrder[i];
                string socket = SocketOf(slot);

                bool exists = false;
                if (core != null)
                    for (int j = 0; j < core.sockets.Count; j++)
                        if (core.sockets[j].type == socket) { exists = true; break; }

                string id;
                _picked.TryGetValue(slot, out id);
                var part = PartDatabase.GetPart(id);

                GUI.enabled = exists;
                string mark = _slotIndex == i ? "▶ " : "   ";
                string name = part != null ? part.name : (exists ? "비어 있음" : "이 코어에 없음");
                string color = part != null ? ColorTag(part) : "#7A8884";
                if (GUILayout.Button(mark + SlotLabel[i] + " : <color=" + color + ">" + name + "</color>", Btn(_slotIndex == i)))
                    _pendingSlot = i;
                GUI.enabled = true;
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("모두 비우기", _button)) _pendingClearAll = true;
            if (GUILayout.Button("기본 구성으로 되돌리기", _button)) _pendingReset = true;
            GUILayout.Label("구성은 자동 저장된다. 씬을 다시 만들어도 남는다.", _small);
            GUILayout.EndVertical();
        }

        void DrawPartColumn(float width, float height)
        {
            string slot = SlotOrder[_slotIndex];
            string socket = SocketOf(slot);

            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("<b>" + SlotLabel[_slotIndex] + " 파츠</b>", _label);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(width), GUILayout.Height(height - 30f));

            string cur;
            _picked.TryGetValue(slot, out cur);

            if (GUILayout.Button((string.IsNullOrEmpty(cur) ? "▶ " : "    ") + "비우기", Btn(string.IsNullOrEmpty(cur))))
            {
                _pendingSlotKey = slot;
                _pendingRemove = true;
            }

            foreach (var p in PartDatabase.Parts)
            {
                if (p.socket != socket) continue;

                bool on = p.id == cur;
                string head = (on ? "▶ " : "    ") + "<color=" + ColorTag(p) + "><b>" + p.name + "</b></color>"
                              + "   " + p.weight.ToString("0") + "kg · 코스트 " + p.cost + " · " + p.size;
                if (GUILayout.Button(head, Btn(on)))
                {
                    _pendingSlotKey = slot;
                    _pendingPartId = p.id;      // 좌우 앞다리에 같은 파츠를 달아도 된다
                }
                GUILayout.Label("      " + Describe(p), _small);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        static string Describe(PartData p)
        {
            var sb = new System.Text.StringBuilder();
            if (p.ability != null)
            {
                sb.Append(ActionName(p.ability.action));
                if (p.ability.damage > 0f) sb.Append(" · 피해 " + p.ability.damage.ToString("0"));
                if (p.ability.range > 0f) sb.Append(" · 사거리 " + p.ability.range.ToString("0.0") + "m");
                if (p.ability.cooldown > 0f) sb.Append(" · 대기 " + p.ability.cooldown.ToString("0.0") + "s");
                if (p.ability.rootSeconds > 0f) sb.Append(" · 속박 " + p.ability.rootSeconds.ToString("0") + "s");
                if (p.ability.dotDps > 0f) sb.Append(" · 지속 " + p.ability.dotDps.ToString("0") + "/s");
                if (p.ability.armorIgnore > 0f) sb.Append(" · 장갑무시 " + (p.ability.armorIgnore * 100f).ToString("0") + "%");
            }
            if (p.locomotion != null)
            {
                sb.Append("다리 " + p.locomotion.legs + "개");
                if (p.locomotion.spdMul != 1f) sb.Append(" · 속도 x" + p.locomotion.spdMul.ToString("0.00"));
                if (p.locomotion.jumpMul != 1f) sb.Append(" · 도약 x" + p.locomotion.jumpMul.ToString("0.0"));
                if (p.locomotion.sprintMul > 1f) sb.Append(" · 질주 x" + p.locomotion.sprintMul.ToString("0.00"));
                if (p.locomotion.wallClimb) sb.Append(" · 벽 이동");
                if (p.locomotion.ceiling) sb.Append(" · 천장 이동");
            }
            if (p.passive != null)
            {
                if (p.passive.armFront != 0f) sb.Append(" · 정면장갑 +" + p.passive.armFront.ToString("0"));
                if (p.passive.armSide != 0f) sb.Append(" · 측면 +" + p.passive.armSide.ToString("0"));
                if (p.passive.spdMul != 1f) sb.Append(" · 속도 x" + p.passive.spdMul.ToString("0.00"));
                if (p.passive.hpBonus != 0f) sb.Append(" · HP " + p.passive.hpBonus.ToString("+0;-0"));
                if (p.passive.survivesLethal) sb.Append(" · 치명타 1회 생존");
                if (p.passive.knockbackResist != 0f) sb.Append(" · 넉백저항 +" + (p.passive.knockbackResist * 100f).ToString("0") + "%");
            }
            return sb.ToString();
        }

        static string ActionName(string action)
        {
            switch (action)
            {
                case "SWING": return "휘두르기";
                case "THRUST": return "찌르기";
                case "GRAB": return "잡기";
                case "WEB": return "거미줄";
                case "GNAW": return "갉기";
                case "POUNCE": return "덮치기";
                case "SPRAY": return "분사";
                case "DRONE": return "드론";
                case "STING": return "독침";
                case "CLOAK": return "투명";
                default: return action;
            }
        }

        void DrawStatsColumn(float width, float height)
        {
            var core = PartDatabase.GetCore(_coreId);
            var parts = BuildParts();

            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("<b>성능</b>", _label);

            if (core == null) { GUILayout.Label("코어 없음", _label); GUILayout.EndVertical(); return; }

            var stats = BuildStats.Compute(core, parts);
            var issues = AssemblyRules.Validate(core, parts);
            bool buildable = AssemblyRules.IsBuildable(issues);

            int cost = 0;
            for (int i = 0; i < parts.Count; i++) cost += parts[i].cost;

            GUILayout.Label(Row("무게", stats.totalWeight.ToString("0") + " kg  /  적재 " + stats.load.ToString("0") + " kg"), _label);
            GUILayout.Label(Row("과적", (stats.overload * 100f).ToString("0") + " %"), _label);
            GUILayout.Label(Row("속도", stats.speed.ToString("0.0") + "  (질주 " + stats.sprintSpeed.ToString("0.0") + ")"), _label);
            GUILayout.Label(Row("도약", stats.jumpHeight.ToString("0.0") + " m"), _label);
            GUILayout.Label(Row("HP", stats.hp.ToString("0")), _label);
            GUILayout.Label(Row("EN", stats.enMax.ToString("0") + "  재생 " + stats.enRegen.ToString("0.0") + "/s"), _label);
            GUILayout.Label(Row("장갑", "정면 " + stats.armor.front.ToString("0") + " · 측면 " + stats.armor.side.ToString("0")
                                + " · 후면 " + stats.armor.rear.ToString("0")), _label);
            GUILayout.Label(Row("다리", stats.legs + "개" + (stats.wallClimb ? " · 벽 이동" : "") + (stats.ceiling ? " · 천장" : "")), _label);
            GUILayout.Label(Row("코스트", cost + " / " + costLimit + (cost > costLimit ? "   <color=#B23A2E>초과</color>" : "")), _label);

            GUILayout.Space(8f);
            GUILayout.Label("<b>조립 규칙</b>", _label);
            if (issues.Count == 0) GUILayout.Label("문제 없음", _small);
            for (int i = 0; i < issues.Count; i++)
            {
                string c = issues[i].level == IssueLevel.Blocked ? "#B23A2E"
                         : issues[i].level == IssueLevel.Penalty ? "#A8650F" : "#5A6862";
                GUILayout.Label("<color=" + c + ">" + issues[i].code + " " + issues[i].message + "</color>", _small);
            }

            if (_swapLimit > 0)
            {
                int changed = ChangedSlots;
                string c = changed > _swapLimit ? "#B23A2E" : "#14695F";
                GUILayout.Space(6f);
                GUILayout.Label("<color=" + c + ">교체한 소켓 " + changed + " / " + _swapLimit + "</color>", _label);
                if (changed > _swapLimit) buildable = false;
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = buildable;
            if (GUILayout.Button(buildable ? "이 구성으로 출전  (Tab)" : "조립 불가", _button, GUILayout.Height(34f)))
                _pendingApply = true;
            GUI.enabled = true;

            GUILayout.EndVertical();
        }

        static string Row(string key, string value)
        {
            return "<color=#5A6862>" + key + "</color>   " + value;
        }

        static string ColorTag(PartData p)
        {
            Color c = FrameBuilder.ColorOf(p.color, new Color(0.24f, 0.29f, 0.27f));
            Color dark = Color.Lerp(c, Color.black, 0.35f);
            return "#" + ColorUtility.ToHtmlStringRGB(dark);
        }

        void Apply()
        {
            if (spawner == null) return;
            spawner.coreId = _coreId;
            spawner.partIds = BuildPartIds();
            spawner.Respawn();

            var match = FindFirstObjectByType<Bioframe.Match.MatchManager>();
            if (match != null && match.State == Bioframe.Match.MatchState.Intermission)
            {
                SetSwapLimit(0);
                match.OnBuildConfirmed();
                if (IsOpen) Toggle();
                return;
            }

            Toggle();
        }
    }
}
