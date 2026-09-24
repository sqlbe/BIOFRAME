using UnityEngine;
using Bioframe.Assembly;
using Bioframe.Match;
using Bioframe.Movement;

namespace Bioframe.UI
{
    // 시작 메뉴. Play를 누르면 먼저 여기서 무엇을 할지 고른다.
    public class StartMenu : MonoBehaviour
    {
        public MatchManager match;
        public MissionManager mission;
        public AssemblyScreen assembly;
        public FrameSpawner spawner;

        public bool IsOpen { get; private set; }

        Texture2D _white;
        GUIStyle _title, _label, _small, _button, _buttonMain;
        Vector2 _scroll;
        bool _showMissionList;

        void Start()
        {
            if (match == null) match = FindFirstObjectByType<MatchManager>();
            if (mission == null) mission = FindFirstObjectByType<MissionManager>();
            if (assembly == null) assembly = FindFirstObjectByType<AssemblyScreen>();
            if (spawner == null) spawner = FindFirstObjectByType<FrameSpawner>();
            Open();
        }

        public void Open()
        {
            IsOpen = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            IsOpen = false;
            Time.timeScale = 1f;
        }

        void Update()
        {
            // Esc로 메뉴를 다시 연다
            if (!IsOpen && InputReader.CursorTogglePressed && (assembly == null || !assembly.IsOpen)) Open();
        }

        void EnsureStyles()
        {
            if (_white != null) return;
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            _white.hideFlags = HideFlags.HideAndDontSave;

            _label = new GUIStyle(GUI.skin.label);
            _label.fontSize = 13;
            _label.richText = true;
            _label.normal.textColor = new Color(0.08f, 0.12f, 0.10f);

            _title = new GUIStyle(_label);
            _title.fontSize = 26;
            _title.fontStyle = FontStyle.Bold;

            _small = new GUIStyle(_label);
            _small.fontSize = 12;
            _small.wordWrap = true;
            _small.normal.textColor = new Color(0.34f, 0.40f, 0.38f);

            var normal = Tex(new Color(0.95f, 0.96f, 0.95f));
            var hover = Tex(new Color(0.86f, 0.92f, 0.90f));
            var main = Tex(new Color(0.80f, 0.91f, 0.88f));
            var mainHover = Tex(new Color(0.72f, 0.87f, 0.84f));

            _button = new GUIStyle(GUI.skin.button);
            _button.fontSize = 14;
            _button.alignment = TextAnchor.MiddleLeft;
            _button.padding = new RectOffset(14, 10, 10, 10);
            _button.richText = true;
            Paint(_button, normal, hover, new Color(0.10f, 0.14f, 0.12f));

            _buttonMain = new GUIStyle(_button);
            _buttonMain.fontSize = 16;
            _buttonMain.fontStyle = FontStyle.Bold;
            Paint(_buttonMain, main, mainHover, new Color(0.05f, 0.22f, 0.19f));
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        static void Paint(GUIStyle st, Texture2D normal, Texture2D hover, Color text)
        {
            st.normal.background = normal;
            st.hover.background = hover;
            st.active.background = hover;
            st.focused.background = normal;
            st.onNormal.background = normal;
            st.onHover.background = hover;
            st.normal.textColor = text;
            st.hover.textColor = text;
            st.active.textColor = text;
            st.focused.textColor = text;
            st.onNormal.textColor = text;
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();

            float w = 520f, h = _showMissionList ? 560f : 380f;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.90f, 0.92f, 0.93f, 0.75f));
            Fill(panel, new Color(1f, 1f, 1f, 0.98f));
            Fill(new Rect(panel.x, panel.y, panel.width, 3f), new Color(0.11f, 0.42f, 0.38f));

            GUILayout.BeginArea(new Rect(panel.x + 26f, panel.y + 20f, panel.width - 52f, panel.height - 40f));

            GUILayout.Label("BIOFRAME", _title);
            GUILayout.Label("생물을 설계하고, 팀을 짜고, 직접 조종해 싸운다.", _small);
            GUILayout.Space(14f);

            if (!_showMissionList)
            {
                if (GUILayout.Button("훈련 시작    <color=#3C4A46>기능을 하나씩 익힌다</color>", _buttonMain, GUILayout.Height(46f)))
                {
                    Close();
                    if (mission != null) mission.StartMissions();
                }

                GUILayout.Space(6f);
                if (GUILayout.Button("훈련 골라서 시작    <color=#3C4A46>원하는 항목부터</color>", _button, GUILayout.Height(38f)))
                    _showMissionList = true;

                GUILayout.Space(6f);
                if (GUILayout.Button("대전 시작    <color=#3C4A46>3판 2선승, 봇과 겨룬다</color>", _button, GUILayout.Height(38f)))
                {
                    Close();
                    if (mission != null) mission.StopMissions();
                    if (match != null) { match.enabled = true; match.StartMatch(); }
                }

                GUILayout.Space(6f);
                if (GUILayout.Button("자유 연습    <color=#3C4A46>규칙 없이 돌아다닌다</color>", _button, GUILayout.Height(38f)))
                {
                    Close();
                    if (mission != null) mission.StopMissions();
                    if (match != null) match.enabled = false;
                    if (spawner != null) { spawner.autoRespawn = true; spawner.Respawn(); }
                }

                GUILayout.Space(6f);
                if (GUILayout.Button("기체 조립    <color=#3C4A46>파츠를 고른다 (Tab)</color>", _button, GUILayout.Height(38f)))
                {
                    Close();
                    if (assembly != null && !assembly.IsOpen) assembly.Toggle();
                }

                GUILayout.Space(10f);
                GUILayout.Label("Esc 메뉴 · Tab 조립 · M 훈련 · F2 자세히", _small);
            }
            else
            {
                GUILayout.Label("훈련 항목", _label);
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(400f));

                for (int i = 0; i < (mission != null ? mission.Count : 0); i++)
                {
                    var m = mission.MissionAt(i);
                    if (m == null) continue;
                    if (GUILayout.Button((i + 1) + ".  " + m.title, _button))
                    {
                        Close();
                        mission.StartMissions();
                        mission.Go(i);
                    }
                }

                GUILayout.EndScrollView();
                GUILayout.Space(8f);
                if (GUILayout.Button("뒤로", _button, GUILayout.Height(32f))) _showMissionList = false;
            }

            GUILayout.EndArea();
        }
    }
}
