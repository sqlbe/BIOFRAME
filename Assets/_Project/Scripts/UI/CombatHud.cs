using UnityEngine;
using Bioframe.Rules;
using Bioframe.Assembly;
using Bioframe.Combat;
using Bioframe.Movement;

namespace Bioframe.UI
{
    // 전투 화면. 아래쪽에 HP·EN 막대와 기술 칸, 오른쪽 아래에 전투 기록을 그린다.
    public class CombatHud : MonoBehaviour
    {
        public FrameSpawner spawner;
        public AssemblyScreen assembly;
        public Bioframe.Match.MatchManager match;

        static readonly string[] Keys = { "Q", "E", "R", "F" };
        static readonly string[] Sockets = { "HD", "DS", "TL", "SK" };
        static readonly string[] SlotLabel = { "머리", "등", "꼬리", "외피" };

        Texture2D _white;
        GUIStyle _key, _name, _small, _log, _big;

        void Start()
        {
            if (spawner == null) spawner = FindFirstObjectByType<FrameSpawner>();
            if (assembly == null) assembly = FindFirstObjectByType<AssemblyScreen>();
            if (match == null) match = FindFirstObjectByType<Bioframe.Match.MatchManager>();
        }

        void EnsureStyles()
        {
            if (_white != null) return;
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            _white.hideFlags = HideFlags.HideAndDontSave;

            _key = new GUIStyle(GUI.skin.label);
            _key.fontSize = 15;
            _key.fontStyle = FontStyle.Bold;
            _key.alignment = TextAnchor.UpperLeft;
            _key.normal.textColor = new Color(0.09f, 0.13f, 0.11f);

            _name = new GUIStyle(_key);
            _name.fontSize = 12;
            _name.fontStyle = FontStyle.Normal;
            _name.richText = true;

            _small = new GUIStyle(_name);
            _small.fontSize = 11;
            _small.normal.textColor = new Color(0.36f, 0.42f, 0.40f);

            _log = new GUIStyle(_name);
            _log.fontSize = 12;
            _log.alignment = TextAnchor.LowerLeft;

            _big = new GUIStyle(_key);
            _big.fontSize = 13;
        }

        // 위 가운데: 라운드, 점수, 남은 시간, 안내 문구
        void DrawMatchBar()
        {
            if (match == null) return;

            float w = 320f;
            var box = new Rect((Screen.width - w) * 0.5f, 80f, w, 30f);
            Fill(box, new Color(1f, 1f, 1f, 0.92f));
            Frame(box, new Color(0.62f, 0.66f, 0.64f));

            var center = new GUIStyle(_key);
            center.alignment = TextAnchor.MiddleCenter;
            center.fontSize = 14;
            center.richText = true;

            int m = Mathf.FloorToInt(Mathf.Max(0f, match.Timer) / 60f);
            int sec = Mathf.FloorToInt(Mathf.Max(0f, match.Timer) % 60f);
            string time = match.State == Bioframe.Match.MatchState.Fighting
                          ? m.ToString("0") + ":" + sec.ToString("00") : "";

            GUI.Label(box, match.RoundNumber + "라운드    <color=#14695F>" + match.PlayerWins + "</color> : <color=#B23A2E>"
                      + match.BotWins + "</color>    " + time, center);

            bool assemblyOpen = assembly != null && assembly.IsOpen;
            if (!assemblyOpen && !string.IsNullOrEmpty(match.Banner))
            {
                var big = new GUIStyle(_key);
                big.alignment = TextAnchor.MiddleCenter;
                big.fontSize = 26;
                var banner = new Rect(0f, Screen.height * 0.34f, Screen.width, 44f);
                GUI.Label(banner, match.Banner, big);
            }

            if (!assemblyOpen && match.State == Bioframe.Match.MatchState.MatchEnd)
            {
                var hint = new GUIStyle(_small);
                hint.alignment = TextAnchor.MiddleCenter;
                hint.fontSize = 14;
                GUI.Label(new Rect(0f, Screen.height * 0.34f + 46f, Screen.width, 24f),
                          "Enter를 누르면 다시 시작", hint);
            }
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        void Frame(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        void OnGUI()
        {
            if (spawner == null) return;
            EnsureStyles();

            DrawMatchBar();

            if (assembly != null && assembly.IsOpen) return;   // 조립 화면이 열려 있으면 나머지는 가린다

            var hp = spawner.PlayerHp;
            var combat = spawner.PlayerCombat;
            var ctrl = spawner.PlayerController;
            if (hp == null || combat == null || ctrl == null) return;

            float w = 560f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 118f;

            // 바탕
            var panel = new Rect(x - 12f, y - 10f, w + 24f, 108f);
            Fill(panel, new Color(1f, 1f, 1f, 0.90f));
            Frame(panel, new Color(0.62f, 0.66f, 0.64f));

            // HP
            var hpBack = new Rect(x, y, w, 16f);
            Fill(hpBack, new Color(0.86f, 0.87f, 0.85f));
            Fill(new Rect(hpBack.x, hpBack.y, w * Mathf.Clamp01(hp.Hp / hp.maxHp), hpBack.height),
                 new Color(0.72f, 0.24f, 0.18f));
            GUI.Label(new Rect(hpBack.x + 6f, hpBack.y - 1f, 260f, 18f),
                      "HP  " + hp.Hp.ToString("0") + " / " + hp.maxHp.ToString("0"), _big);

            // EN
            var enBack = new Rect(x, y + 20f, w, 10f);
            var stats = spawner.PlayerStats;
            Fill(enBack, new Color(0.86f, 0.87f, 0.85f));
            Fill(new Rect(enBack.x, enBack.y, w * Mathf.Clamp01(ctrl.Energy / Mathf.Max(1f, stats.enMax)), enBack.height),
                 new Color(0.20f, 0.52f, 0.68f));
            GUI.Label(new Rect(enBack.x + 6f, enBack.y - 4f, 260f, 16f),
                      "EN  " + ctrl.Energy.ToString("0"), _small);

            // 기술 칸 네 개
            float slotW = 132f, slotH = 52f, gap = 10f;
            float sx = x;
            for (int i = 0; i < Sockets.Length; i++)
            {
                var r = new Rect(sx, y + 36f, slotW, slotH);
                var part = combat.SlotPart(Sockets[i]);

                Fill(r, part != null ? new Color(0.97f, 0.98f, 0.97f) : new Color(0.92f, 0.93f, 0.92f));
                Frame(r, new Color(0.70f, 0.74f, 0.72f));

                if (part != null)
                {
                    Color c = FrameBuilder.ColorOf(part.color, new Color(0.3f, 0.4f, 0.38f));
                    Fill(new Rect(r.x, r.y, 4f, r.height), c);   // 파츠 색 띠

                    GUI.Label(new Rect(r.x + 10f, r.y + 4f, 24f, 20f), Keys[i], _key);
                    GUI.Label(new Rect(r.x + 32f, r.y + 6f, slotW - 38f, 16f), part.name, _name);

                    float cd = combat.SlotCooldown(Sockets[i]);
                    float cdMax = combat.SlotCooldownMax(Sockets[i]);
                    if (cd > 0.01f && cdMax > 0.01f)
                    {
                        // 남은 대기시간을 어둡게 덮는다
                        float k = Mathf.Clamp01(cd / cdMax);
                        Fill(new Rect(r.x, r.yMax - r.height * k, r.width, r.height * k), new Color(0.2f, 0.25f, 0.24f, 0.35f));
                        GUI.Label(new Rect(r.x + 32f, r.y + 26f, slotW - 38f, 16f), cd.ToString("0.0") + "초", _small);
                    }
                    else
                    {
                        bool cloak = Sockets[i] == "SK" && combat.Cloaked;
                        GUI.Label(new Rect(r.x + 32f, r.y + 26f, slotW - 38f, 16f),
                                  cloak ? "<color=#14695F>켜짐</color>" : "사용 가능", _small);
                    }
                }
                else
                {
                    GUI.Label(new Rect(r.x + 10f, r.y + 4f, 24f, 20f), Keys[i], _key);
                    GUI.Label(new Rect(r.x + 32f, r.y + 14f, slotW - 38f, 16f), SlotLabel[i] + " 비어 있음", _small);
                }

                sx += slotW + gap;
            }

            // 위쪽: 적 정보
            var bot = spawner.BotHp;
            if (bot != null)
            {
                float bw = 460f;
                var bx = (Screen.width - bw) * 0.5f;
                var box = new Rect(bx - 10f, 12f, bw + 20f, 62f);
                Fill(box, new Color(1f, 1f, 1f, 0.90f));
                Frame(box, new Color(0.62f, 0.66f, 0.64f));

                var title = new GUIStyle(_key);
                title.fontSize = 14;
                title.alignment = TextAnchor.UpperCenter;
                string state = bot.Rooted ? "  <color=#14695F>속박 " + bot.RootRemain.ToString("0.0") + "초</color>" : "";
                title.richText = true;
                GUI.Label(new Rect(bx, 16f, bw, 20f),
                          spawner.BotPresetName + "  —  " + spawner.BotTacticName + state, title);

                var bar = new Rect(bx, 38f, bw, 12f);
                Fill(bar, new Color(0.86f, 0.87f, 0.85f));
                Fill(new Rect(bar.x, bar.y, bw * Mathf.Clamp01(bot.Hp / bot.maxHp), bar.height),
                     new Color(0.72f, 0.24f, 0.18f));

                var sub = new GUIStyle(_small);
                sub.alignment = TextAnchor.UpperCenter;
                GUI.Label(new Rect(bx, 52f, bw, 16f),
                          bot.Hp.ToString("0") + " / " + bot.maxHp.ToString("0") + "   " + bot.subtitle, sub);
            }

            // 전투 기록
            float logW = 330f;
            var logRect = new Rect(Screen.width - logW - 14f, Screen.height - 130f, logW, 116f);
            Fill(logRect, new Color(1f, 1f, 1f, 0.82f));
            Frame(logRect, new Color(0.70f, 0.74f, 0.72f));
            for (int i = 0; i < combat.LogLines.Count; i++)
            {
                float alpha = 0.45f + 0.55f * (i + 1f) / combat.LogLines.Count;
                _log.normal.textColor = new Color(0.09f, 0.13f, 0.11f, alpha);
                GUI.Label(new Rect(logRect.x + 8f, logRect.y + 6f + i * 18f, logW - 16f, 18f), combat.LogLines[i], _log);
            }

            // 도움말
            GUI.Label(new Rect(x - 12f, y - 30f, 460f, 18f),
                      "<color=#3C4A46>Tab 조립 · A+클릭 공격 · Q·E·R·F 파츠 · F2 자세히</color>", _small);
        }
    }
}
