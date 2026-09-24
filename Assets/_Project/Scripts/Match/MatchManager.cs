using UnityEngine;
using Bioframe.Assembly;
using Bioframe.Combat;
using Bioframe.UI;
using Bioframe.Movement;

namespace Bioframe.Match
{
    public enum MatchState
    {
        Ready,          // 라운드 시작 대기
        Fighting,       // 교전 중
        RoundEnd,       // 라운드 결과 표시
        Intermission,   // 라운드 사이 조립 시간
        MatchEnd        // 경기 종료
    }

    // 기획서 §4 매치 규칙. 1:1 결투는 3판 2선승이다.
    public class MatchManager : MonoBehaviour
    {
        public FrameSpawner spawner;
        public AssemblyScreen assembly;

        [Header("규칙")]
        public int winsNeeded = 2;          // 3판 2선승
        public float roundSeconds = 180f;
        public float readySeconds = 3f;
        public float roundEndSeconds = 2.5f;
        public float intermissionSeconds = 30f;
        public int swapLimit = 2;           // 라운드 사이 교체 가능한 소켓 수

        public MatchState State { get; private set; }
        public int RoundNumber { get; private set; }
        public int PlayerWins { get; private set; }
        public int BotWins { get; private set; }
        public float Timer { get; private set; }
        public string Banner { get; private set; }
        public string LastRoundResult { get; private set; }

        void Start()
        {
            if (spawner == null) spawner = FindFirstObjectByType<FrameSpawner>();
            if (assembly == null) assembly = FindFirstObjectByType<AssemblyScreen>();
            if (spawner != null) spawner.autoRespawn = false;
            StartMatch();
        }

        public void StartMatch()
        {
            PlayerWins = 0;
            BotWins = 0;
            RoundNumber = 0;
            LastRoundResult = "";
            BeginRound();
        }

        void BeginRound()
        {
            RoundNumber++;
            if (spawner != null)
            {
                spawner.ResetForRound();
                spawner.SetBotActive(false);   // 카운트다운 동안 정지
            }
            State = MatchState.Ready;
            Timer = readySeconds;
            Banner = RoundNumber + "라운드 준비";
        }

        void Update()
        {
            if (spawner == null) return;

            float dt = Time.unscaledDeltaTime;   // 조립 화면에서 시간이 멈춰도 흐르게
            var playerHp = spawner.PlayerHp;
            var botHp = spawner.BotHp;

            switch (State)
            {
                case MatchState.Ready:
                    Timer -= dt;
                    Banner = RoundNumber + "라운드  " + Mathf.CeilToInt(Mathf.Max(0f, Timer));
                    if (Timer <= 0f)
                    {
                        State = MatchState.Fighting;
                        Timer = roundSeconds;
                        Banner = "";
                        if (spawner != null) spawner.SetBotActive(true);
                    }
                    break;

                case MatchState.Fighting:
                    Timer -= Time.deltaTime;   // 전투 시간은 실제 흐름을 따른다

                    if (playerHp != null && !playerHp.Alive) { EndRound(false, "격파당함"); break; }
                    if (botHp != null && !botHp.Alive) { EndRound(true, "적 격파"); break; }

                    if (Timer <= 0f)
                    {
                        // 시간 초과: 남은 HP 비율이 높은 쪽 승리
                        float mine = playerHp != null ? playerHp.Hp / playerHp.maxHp : 0f;
                        float theirs = botHp != null ? botHp.Hp / botHp.maxHp : 0f;
                        if (Mathf.Abs(mine - theirs) < 0.01f) EndRound(false, "시간 초과 · 무승부는 패로 처리");
                        else EndRound(mine > theirs, "시간 초과 · 남은 HP 판정");
                    }
                    break;

                case MatchState.RoundEnd:
                    Timer -= dt;
                    if (Timer <= 0f)
                    {
                        if (PlayerWins >= winsNeeded || BotWins >= winsNeeded)
                        {
                            State = MatchState.MatchEnd;
                            Banner = PlayerWins > BotWins ? "경기 승리" : "경기 패배";
                        }
                        else BeginIntermission();
                    }
                    break;

                case MatchState.Intermission:
                    Timer -= dt;
                    Banner = "조립 시간  " + Mathf.CeilToInt(Mathf.Max(0f, Timer)) + "초  ·  소켓 "
                             + swapLimit + "개까지 교체";
                    if (Timer <= 0f) CloseIntermission();
                    break;

                case MatchState.MatchEnd:
                    if (InputReader.RestartPressed) StartMatch();
                    break;
            }
        }

        void EndRound(bool playerWon, string reason)
        {
            if (playerWon) PlayerWins++;
            else BotWins++;

            State = MatchState.RoundEnd;
            Timer = roundEndSeconds;
            LastRoundResult = (playerWon ? "라운드 승리" : "라운드 패배") + "  —  " + reason;
            Banner = LastRoundResult;
        }

        void BeginIntermission()
        {
            if (spawner != null) spawner.SetBotActive(false);
            State = MatchState.Intermission;
            Timer = intermissionSeconds;

            if (assembly != null)
            {
                assembly.SetSwapLimit(swapLimit);      // 교체 가능한 소켓 수 제한
                if (!assembly.IsOpen) assembly.Toggle();
            }
        }

        void CloseIntermission()
        {
            if (assembly != null)
            {
                assembly.SetSwapLimit(0);
                if (assembly.IsOpen) assembly.Toggle();
            }
            BeginRound();
        }

        // 조립 화면에서 출전을 누르면 남은 조립 시간을 건너뛴다
        public void OnBuildConfirmed()
        {
            if (State == MatchState.Intermission) CloseIntermission();
        }
    }
}
