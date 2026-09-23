using System.Collections.Generic;
using UnityEngine;
using Bioframe.Rules;
using Bioframe.Movement;
using Bioframe.Combat;

namespace Bioframe.Assembly
{
    // M1 테스트용 스포너. 조립 화면이 없으므로 여기서 코어와 파츠를 지정하고,
    // 숫자 키로 이동계를 바꿔가며 조작감을 비교한다.
    public class FrameSpawner : MonoBehaviour
    {
        public string coreId = "INS";
        public List<string> partIds = new List<string> { "LC-01", "FL-01", "FL-03", "HD-06" };
        public ThirdPersonCamera cam;
        public bool showHud = true;

        static readonly string[] LocomotionCycle = { "LC-01", "LC-02", "LC-03" };
        // 앞다리 후보. 빈 칸(null)까지 돌려서 한쪽 팔을 비울 수도 있다.
        static readonly string[] ArmCycle = { "FL-01", "FL-02", "FL-03", null };
        static readonly string[] HeadCycle = { "HD-06", "HD-07", "HD-08", "HD-01", null };

        GameObject _frame;
        FrameController _controller;
        FrameCombat _combat;
        CoreData _core;
        List<PartData> _parts;
        FrameStats _stats;
        List<AssemblyIssue> _issues = new List<AssemblyIssue>();

        void Start()
        {
            _mode = startMode;
            if (cam != null) cam.SetMode(_mode == ControlMode.Direct ? CameraMode.Follow : CameraMode.TopDown);
            Spawn(transform.position + Vector3.up * 0.5f);
        }

        // 기본 조작은 쿼터뷰 클릭 이동. F1으로 3인칭으로 바꿀 수 있다.
        public ControlMode startMode = ControlMode.ClickMove;
        ControlMode _mode = ControlMode.ClickMove;

        void Update()
        {
            if (InputReader.ModeTogglePressed) ToggleMode();
            if (InputReader.RespawnPressed) Spawn(transform.position + Vector3.up * 0.5f);

            int n = InputReader.NumberPressed;
            if (n == 0) return;

            if (n >= 1 && n <= 3) SwapLocomotion(LocomotionCycle[n - 1]);
            else if (n == 4) CycleArm(0);
            else if (n == 5) CycleArm(1);
            else if (n == 6) CycleHead();
            else if (n == 9) { coreId = coreId == "INS" ? "QUA" : "INS"; Respawn(); }
        }

        void ToggleMode()
        {
            _mode = _mode == ControlMode.Direct ? ControlMode.ClickMove : ControlMode.Direct;
            if (_controller != null) _controller.SetMode(_mode);
            if (cam != null) cam.SetMode(_mode == ControlMode.Direct ? CameraMode.Follow : CameraMode.TopDown);
        }

        void CycleHead()
        {
            int idx = -1;
            for (int i = 0; i < partIds.Count; i++)
            {
                var p = PartDatabase.GetPart(partIds[i]);
                if (p != null && p.socket == "HD") { idx = i; break; }
            }

            string current = idx >= 0 ? partIds[idx] : null;
            int next = 0;
            for (int i = 0; i < HeadCycle.Length; i++)
                if (HeadCycle[i] == current) { next = (i + 1) % HeadCycle.Length; break; }

            string id = HeadCycle[next];
            if (idx >= 0)
            {
                if (id == null) partIds.RemoveAt(idx);
                else partIds[idx] = id;
            }
            else if (id != null) partIds.Add(id);

            Respawn();
        }

        // slot 0 = 왼팔, 1 = 오른팔
        void CycleArm(int slot)
        {
            var arms = new List<int>();
            for (int i = 0; i < partIds.Count; i++)
            {
                var p = PartDatabase.GetPart(partIds[i]);
                if (p != null && p.socket == "FL") arms.Add(i);
            }

            string current = slot < arms.Count ? partIds[arms[slot]] : null;
            int next = 0;
            for (int i = 0; i < ArmCycle.Length; i++)
                if (ArmCycle[i] == current) { next = (i + 1) % ArmCycle.Length; break; }

            string id = ArmCycle[next];

            if (slot < arms.Count)
            {
                if (id == null) partIds.RemoveAt(arms[slot]);
                else partIds[arms[slot]] = id;
            }
            else if (id != null) partIds.Add(id);

            Respawn();
        }

        void SwapLocomotion(string id)
        {
            for (int i = 0; i < partIds.Count; i++)
            {
                var p = PartDatabase.GetPart(partIds[i]);
                if (p != null && p.socket == "LC") { partIds[i] = id; Respawn(); return; }
            }
            partIds.Add(id);
            Respawn();
        }

        void Respawn()
        {
            Vector3 pos = _frame != null ? _frame.transform.position + Vector3.up * 0.3f : transform.position;
            Spawn(pos);
        }

        public void Spawn(Vector3 position)
        {
            if (_frame != null) Destroy(_frame);

            _core = PartDatabase.GetCore(coreId);
            if (_core == null) { Debug.LogError("[BIOFRAME] 코어를 찾을 수 없다: " + coreId); return; }

            _parts = PartDatabase.GetParts(partIds);
            _issues = AssemblyRules.Validate(_core, _parts);
            _stats = BuildStats.Compute(_core, _parts);

            if (!AssemblyRules.IsBuildable(_issues))
            {
                foreach (var i in _issues)
                    if (i.level == IssueLevel.Blocked) Debug.LogError("[BIOFRAME] 조립 불가 " + i);
                return;
            }

            _frame = FrameBuilder.Build(_core, _parts, position);
            var visual = _frame.GetComponent<FrameVisual>();

            var legs = _frame.AddComponent<ProceduralLegs>();
            legs.Setup(_stats.legs, visual.body, visual.bodyHeight, visual.bodyLength, visual.hipSpread);

            _combat = _frame.AddComponent<FrameCombat>();

            _controller = _frame.AddComponent<FrameController>();
            _controller.combat = _combat;
            _combat.Init(_parts, visual, _stats, _controller);
            _controller.Init(_stats, cam != null ? cam.transform : null);
            _controller.SetMode(_mode);

            if (cam != null) cam.target = _frame.transform;
        }

        static string StateName(Bioframe.Movement.SurfaceState s)
        {
            switch (s)
            {
                case Bioframe.Movement.SurfaceState.Wall: return "<color=#14695F>벽</color>";
                case Bioframe.Movement.SurfaceState.Ceiling: return "<color=#14695F>천장</color>";
                case Bioframe.Movement.SurfaceState.Air: return "공중";
                default: return "지상";
            }
        }

        void OnGUI()
        {
            if (!showHud || _core == null) return;

            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.richText = true;
            style.wordWrap = false;
            style.normal.textColor = new Color(0.07f, 0.11f, 0.09f);   // 밝은 배경용 진한 글자

            var prev = GUI.color;

            // 바탕을 직접 그린다. 에디터 기본 상자는 어두워서 글자가 묻힌다.
            var panel = new Rect(10, 10, 380, 330);
            GUI.color = new Color(1f, 1f, 1f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.62f, 0.66f, 0.64f, 1f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - 1f, panel.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 1f, panel.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.xMax - 1f, panel.y, 1f, panel.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>" + _core.name + " (" + _core.id + ")</b>");
            sb.AppendLine("파츠: " + string.Join(", ", partIds.ToArray()));
            sb.AppendLine("무게 " + _stats.totalWeight.ToString("0") + "kg  /  적재 " + _stats.load.ToString("0") + "kg");
            sb.AppendLine("과적 " + (_stats.overload * 100f).ToString("0") + "%   다리 " + _stats.legs + "개"
                          + (_stats.wallClimb ? "   벽이동 가능" : ""));
            sb.AppendLine("속도 " + _stats.speed.ToString("0.0") + "  질주 " + _stats.sprintSpeed.ToString("0.0") + " m/s");
            sb.AppendLine("도약 " + _stats.jumpHeight.ToString("0.0") + "m   장갑(정면) " + _stats.armor.front.ToString("0"));
            if (_controller != null)
            {
                sb.AppendLine("EN " + _controller.Energy.ToString("0") + " / " + _stats.enMax.ToString("0")
                              + (_controller.Sprinting ? "  <color=#A8650F>질주 중</color>" : ""));
                sb.AppendLine("현재 속도 " + _controller.PlanarSpeed.ToString("0.0") + " m/s   상태 " + StateName(_controller.State));
                if (_stats.wallClimb)
                    sb.AppendLine(_controller.CanCeiling
                        ? "<color=#14695F>벽·천장 이동 가능</color>"
                        : "<color=#A8650F>벽 이동 가능 · 천장은 무게 초과(C3)</color>");
            }
            foreach (var i in _issues)
                if (i.level == IssueLevel.Penalty)
                    sb.AppendLine("<color=#A8650F>" + i.code + " " + i.message + "</color>");

            sb.AppendLine("");
            sb.AppendLine(_mode == ControlMode.Direct
                ? "<b>조작: 3인칭</b>  <color=#3C4A46>(F1으로 전환)</color>"
                : "<b>조작: 쿼터뷰 클릭 이동</b>  <color=#3C4A46>(F1으로 전환)</color>");
            sb.AppendLine(_mode == ControlMode.Direct
                ? "<color=#3C4A46>WASD 이동 · 마우스 우클릭 드래그로 시점</color>"
                : "<color=#3C4A46>좌클릭 이동 · WASD로도 조작 가능</color>");
            sb.AppendLine("<color=#3C4A46>Shift 질주 · Space 도약(벽에서 이탈) · Ctrl 내려오기</color>");
            sb.AppendLine("<color=#3C4A46>휠 확대축소 · Ctrl+휠 시야 각도</color>");
            if (_combat != null)
            {
                sb.AppendLine(_combat.AimingAttack
                    ? "<color=#A8650F>A 공격 지정 중 — 적을 클릭</color>"
                    : (_combat.HasTarget
                        ? "<color=#14695F>대상: " + _combat.Target.displayName + "</color>"
                        : "<color=#3C4A46>A 누르고 적 클릭 = 접근 공격</color>"));
                if (!string.IsNullOrEmpty(_combat.LastLog))
                    sb.AppendLine("<color=#A8650F>" + _combat.LastLog + "</color>");
            }
            sb.AppendLine("<color=#3C4A46>1 거미 · 2 메뚜기 · 3 치타 · 9 코어 · R 처음 위치</color>");
            sb.AppendLine("<color=#3C4A46>4 왼팔 · 5 오른팔 · 6 머리 교체</color>");
            if (_combat != null)
                sb.AppendLine((_combat.AimingHead ? "<color=#A8650F>Q 조준 중 — 대상 클릭: " : "<color=#3C4A46>Q 머리 파츠: ") + _combat.HeadName
                              + (_combat.HeadCooldown > 0f ? " (대기 " + _combat.HeadCooldown.ToString("0.0") + "s)" : "") + "</color>");

            GUI.Label(new Rect(22, 18, 366, 314), sb.ToString(), style);
            GUI.color = prev;
        }
    }
}
