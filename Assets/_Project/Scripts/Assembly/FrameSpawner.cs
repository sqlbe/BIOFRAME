using System.Collections.Generic;
using UnityEngine;
using Bioframe.Rules;
using Bioframe.Movement;

namespace Bioframe.Assembly
{
    // M1 테스트용 스포너. 조립 화면이 없으므로 여기서 코어와 파츠를 지정하고,
    // 숫자 키로 이동계를 바꿔가며 조작감을 비교한다.
    public class FrameSpawner : MonoBehaviour
    {
        public string coreId = "INS";
        public List<string> partIds = new List<string> { "LC-01", "FL-01", "FL-03" };
        public ThirdPersonCamera cam;
        public bool showHud = true;

        static readonly string[] LocomotionCycle = { "LC-01", "LC-02", "LC-03" };

        GameObject _frame;
        FrameController _controller;
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
            else if (n == 9) { coreId = coreId == "INS" ? "QUA" : "INS"; Respawn(); }
        }

        void ToggleMode()
        {
            _mode = _mode == ControlMode.Direct ? ControlMode.ClickMove : ControlMode.Direct;
            if (_controller != null) _controller.SetMode(_mode);
            if (cam != null) cam.SetMode(_mode == ControlMode.Direct ? CameraMode.Follow : CameraMode.TopDown);
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

            _controller = _frame.AddComponent<FrameController>();
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
            style.fontSize = 13;
            style.richText = true;
            style.normal.textColor = new Color(0.09f, 0.13f, 0.11f);   // 밝은 배경용 진한 글자

            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.92f);

            GUI.Box(new Rect(10, 10, 360, 250), "");
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
                ? "<b>조작: 3인칭</b>  <color=#4A5A55>(F1으로 전환)</color>"
                : "<b>조작: 쿼터뷰 클릭 이동</b>  <color=#4A5A55>(F1으로 전환)</color>");
            sb.AppendLine(_mode == ControlMode.Direct
                ? "<color=#4A5A55>WASD 이동 · 마우스 우클릭 드래그로 시점</color>"
                : "<color=#4A5A55>좌클릭 이동 · WASD로도 조작 가능</color>");
            sb.AppendLine("<color=#4A5A55>Shift 질주 · Space 도약(벽에서 이탈) · Ctrl 내려오기</color>");
            sb.AppendLine("<color=#4A5A55>휠 확대축소 · Ctrl+휠 시야 각도</color>");
            sb.AppendLine("<color=#4A5A55>1 거미 · 2 메뚜기 · 3 치타 · 9 코어 · R 처음 위치</color>");

            GUI.Label(new Rect(20, 16, 350, 240), sb.ToString(), style);
            GUI.color = prev;
        }
    }
}
