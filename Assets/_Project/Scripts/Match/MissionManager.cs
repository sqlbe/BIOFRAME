using System.Collections.Generic;
using UnityEngine;
using Bioframe.Assembly;
using Bioframe.Combat;
using Bioframe.Movement;

namespace Bioframe.Match
{
    // 기능을 하나씩 익히는 훈련 미션.
    // 미션마다 필요한 파츠를 자동으로 달아 주고, 그 기능만 쓰게 한다.
    public class MissionManager : MonoBehaviour
    {
        public FrameSpawner spawner;
        public MatchManager match;

        public class Mission
        {
            public string title;            // 목표
            public string hint;             // 조작 안내
            public string core = "INS";
            public string[] parts;          // 자동 장착
            public bool needBot;            // 연습 상대 필요 여부
            public int botPreset;           // 필요한 경우 어떤 상대
            public Vector3[] markers;       // 밟아야 하는 지점 (없으면 사용 안 함)
            public int goal = 1;            // 목표 횟수
            public bool useMatch;           // 라운드 경기를 쓰는 미션
            public bool armoredDummy;       // 장갑이 두꺼운 전용 허수아비를 세운다
            public System.Func<MissionManager, bool> check;   // 달성 판정 (지점 방식이면 null)
        }

        public bool Active { get; private set; }
        public int Index { get; private set; }
        public int Progress { get; private set; }
        public string Flash { get; private set; }      // 잠깐 뜨는 안내

        readonly List<Mission> _missions = new List<Mission>();
        readonly List<Transform> _markerObjects = new List<Transform>();
        readonly bool[] _markerTaken = new bool[8];
        float _flashTime;
        float _stateTimer;

        // 판정용 누적값. 미션이 바뀔 때 초기화한다.
        public int Kills, DotKills, RootHits, LongRootHits, CloakHits, SprayUses, FarKills;
        public int HighRootHits, PounceHits, BotsBeaten;
        public int BotKills, DroneHits, ArmoredKills;
        Damageable _armored;
        public float LastEnemyDistance;
        readonly HashSet<int> _beatenPresets = new HashSet<int>();

        public Mission Current { get { return Index >= 0 && Index < _missions.Count ? _missions[Index] : null; } }
        public int Count { get { return _missions.Count; } }

        public Mission MissionAt(int i)
        {
            if (_missions.Count == 0) BuildList();
            return (i >= 0 && i < _missions.Count) ? _missions[i] : null;
        }

        void Start()
        {
            if (spawner == null) spawner = FindFirstObjectByType<FrameSpawner>();
            if (match == null) match = FindFirstObjectByType<MatchManager>();
            BuildList();
        }

        void OnEnable()
        {
            Damageable.AnyDeath += OnAnyDeath;
            Damageable.AnyRooted += OnAnyRooted;
            Damageable.AnyDamaged += OnAnyDamaged;
        }

        void OnDisable()
        {
            Damageable.AnyDeath -= OnAnyDeath;
            Damageable.AnyRooted -= OnAnyRooted;
            Damageable.AnyDamaged -= OnAnyDamaged;
        }

        // --- 미션 목록 -------------------------------------------------

        void BuildList()
        {
            _missions.Clear();

            // 1. 기초 조작
            _missions.Add(new Mission
            {
                title = "표시된 지점 3곳을 밟아라",
                hint = "빈 땅을 좌클릭하면 그곳으로 이동한다",
                parts = new[] { "LC-01" },
                markers = new[] { new Vector3(6f, 0f, 4f), new Vector3(-5f, 0f, 6f), new Vector3(2f, 0f, -7f) },
                goal = 3
            });

            _missions.Add(new Mission
            {
                title = "질주와 도약으로 먼 지점 2곳을 밟아라",
                hint = "Shift 질주 · Space 도약 · 치타 다리는 빠르다",
                parts = new[] { "LC-03" },
                markers = new[] { new Vector3(22f, 0f, 16f), new Vector3(-20f, 0f, -14f) },
                goal = 2
            });

            _missions.Add(new Mission
            {
                title = "시야 각도를 낮춰 기둥 뒤 지점을 찾아 밟아라",
                hint = "Ctrl+휠 시야 각도 · 휠 확대축소 · 우클릭 드래그 회전",
                parts = new[] { "LC-02" },
                markers = new[] { new Vector3(5.4f, 0f, 5.6f) },
                goal = 1
            });

            _missions.Add(new Mission
            {
                title = "조립 화면에서 사마귀 낫을 양쪽 앞다리에 달고 출전하라",
                hint = "Tab 조립 화면 · 왼 앞다리와 오른 앞다리에 사마귀 낫",
                parts = new[] { "LC-01" },
                check = m =>
                {
                    int count = 0;
                    foreach (var id in m.spawner.partIds) if (id == "FL-01") count++;
                    return count >= 2;
                }
            });

            // 2. 파츠별 능력
            AddCombat("사마귀 낫으로 허수아비를 격파하라", "마우스로 A를 누른 뒤 적 클릭 · 3연타로 벤다",
                      new[] { "LC-01", "FL-01" }, m => m.Kills >= 1);

            AddCombat("게 집게로 허수아비를 격파하라", "A + 클릭 · 집게는 느리지만 단단하다",
                      new[] { "LC-01", "FL-03" }, m => m.Kills >= 1);

            _missions.Add(new Mission
            {
                title = "갯가재 곤봉으로 장갑 150짜리 허수아비를 정면에서 격파하라",
                hint = "장갑 40%를 무시한다 · 대기 6초 · 앞에서 때려도 잘 들어간다",
                parts = new[] { "LC-01", "FL-02" },
                armoredDummy = true,
                check = m => m.ArmoredKills >= 1
            });

            AddCombat("거미줄로 10m 밖에서 상대를 속박하라", "Q 누르고 대상 클릭 · 사거리 12m",
                      new[] { "LC-01", "FL-01", "HD-06" }, m => m.LongRootHits >= 1, needBot: true, preset: 0);

            AddCombat("분사샘을 쓰고 8m 밖으로 달아나라", "E 등 파츠 · 뒤쪽 범위에 분사한 뒤 Shift로 도망",
                      new[] { "LC-03", "FL-01", "DS-02" }, m => m.SprayUses >= 1 && m.LastEnemyDistance > 8f,
                      needBot: true, preset: 4);

            AddCombat("드론으로 8m 밖에서 상대에게 6번 명중시켜라", "E 등 파츠 · 사거리 18m · 한 번에 6기가 날아간다",
                      new[] { "LC-02", "FL-01", "DS-03" }, m => m.DroneHits >= 6, needBot: true, preset: 4);

            AddCombat("투명 상태로 접근해 첫 타격을 넣어라", "F 외피 파츠 · 6m 안에 들어가면 들킨다",
                      new[] { "LC-01", "FL-01", "SK-01" }, m => m.CloakHits >= 1, needBot: true, preset: 0);

            AddCombat("전갈 독침의 지속 피해로 마무리하라", "R 꼬리 파츠 · 6초간 독이 남는다",
                      new[] { "LC-01", "FL-01", "TL-01" }, m => m.DotKills >= 1, needBot: true, preset: 0);

            // 3. 조합 시너지
            AddCombat("높은 곳에 올라 거미줄로 묶은 뒤 내려가 격파하라",
                      "벽면을 클릭하면 타고 오른다 · 높이 3m 이상에서 Q",
                      new[] { "LC-01", "FL-01", "HD-06" },
                      m => m.HighRootHits >= 1 && m.BotKills >= 1, needBot: true, preset: 0);

            _missions.Add(new Mission
            {
                title = "메뚜기 다리로 7m를 뛰어 지붕 위 지점을 밟아라",
                hint = "Space 도약 · 상자나 기둥을 발판으로 쓴다",
                parts = new[] { "LC-02", "FL-01" },
                markers = new[] { new Vector3(18f, 6.2f, -14f) },
                goal = 1
            });

            AddCombat("질주 중에 치타 송곳니로 덮쳐라",
                      "Shift로 달리는 중에 Q · 질주가 아니면 발동하지 않는다",
                      new[] { "LC-03", "FL-01", "HD-08" }, m => m.PounceHits >= 1, needBot: true, preset: 0);

            AddCombat("중장갑으로 정면 교전을 버티며 봇을 격파하라",
                      "장갑 120 · 느리지만 단단하다 · HP 40% 이상 남기고 이겨라",
                      new[] { "LC-01", "FL-01", "DS-01", "SK-02" },
                      m => m.BotKills >= 1 && m.spawner.PlayerHp != null
                           && m.spawner.PlayerHp.Hp > m.spawner.PlayerHp.maxHp * 0.4f,
                      needBot: true, preset: 0);

            // 4. 실전
            AddCombat("다섯 전술의 상대를 모두 격파하라",
                      "쓰러뜨릴 때마다 다음 전술의 상대가 나온다",
                      new[] { "LC-01", "FL-01", "HD-06" },
                      m => m.BotsBeaten >= 5, needBot: true, preset: 0);

            _missions.Add(new Mission
            {
                title = "3판 2선승 경기에서 승리하라",
                hint = "라운드 사이에 소켓 2개를 바꿀 수 있다",
                parts = null,
                needBot = true,
                useMatch = true,
                check = m => m.match != null && m.match.PlayerWins >= 2
            });
        }

        void AddCombat(string title, string hint, string[] parts, System.Func<MissionManager, bool> check,
                       bool needBot = false, int preset = 0)
        {
            _missions.Add(new Mission
            {
                title = title,
                hint = hint,
                parts = parts,
                check = check,
                needBot = needBot,
                botPreset = preset
            });
        }

        // --- 진행 ------------------------------------------------------

        public void StartMissions()
        {
            if (_missions.Count == 0) BuildList();
            if (spawner == null) spawner = FindFirstObjectByType<FrameSpawner>();
            if (match == null) match = FindFirstObjectByType<MatchManager>();
            Active = true;
            if (match != null) match.enabled = false;
            if (spawner != null) spawner.autoRespawn = true;
            Go(0);
        }

        public void StopMissions()
        {
            Active = false;
            ClearMarkers();
            if (_armored != null) { Destroy(_armored.gameObject); _armored = null; }
            if (match != null) match.enabled = true;
        }

        public void Go(int index)
        {
            if (_missions.Count == 0) return;
            Index = Mathf.Clamp(index, 0, _missions.Count - 1);
            Progress = 0;
            Kills = DotKills = RootHits = LongRootHits = CloakHits = SprayUses = FarKills = 0;
            HighRootHits = PounceHits = BotsBeaten = 0;
            BotKills = DroneHits = ArmoredKills = 0;
            _beatenPresets.Clear();
            LastEnemyDistance = 0f;
            _stateTimer = 0f;
            for (int i = 0; i < _markerTaken.Length; i++) _markerTaken[i] = false;

            var m = Current;
            if (m == null || spawner == null) return;

            // 필요한 파츠를 자동으로 달아 준다
            if (m.parts != null)
            {
                spawner.coreId = m.core;
                spawner.partIds = new List<string>(m.parts);
            }
            spawner.spawnBot = m.needBot;
            if (m.needBot) spawner.SetBotPreset(m.botPreset);
            spawner.Respawn();

            // 경기 규칙을 쓰는 미션은 라운드 진행자를 켠다
            if (match != null)
            {
                match.enabled = m.useMatch;
                if (m.useMatch) match.StartMatch();
            }

            SpawnArmoredDummy(m);
            SpawnMarkers(m);
            Say(m.title);
        }

        public void Next() { Go(Index + 1); }
        public void Prev() { Go(Index - 1); }

        void Say(string text)
        {
            Flash = text;
            _flashTime = 3f;
        }

        void Update()
        {
            if (InputReader.MissionTogglePressed)
            {
                if (Active) StopMissions(); else StartMissions();
            }
            if (!Active) return;

            if (InputReader.MissionNextPressed) Next();
            if (InputReader.MissionPrevPressed) Prev();

            float dt = Time.deltaTime;
            if (_flashTime > 0f) { _flashTime -= dt; if (_flashTime <= 0f) Flash = null; }

            var m = Current;
            if (m == null) return;

            // 상대와의 거리 기록 (분사·원거리 판정에 쓴다)
            if (spawner.BotHp != null && spawner.PlayerHp != null)
                LastEnemyDistance = Vector3.Distance(spawner.PlayerHp.transform.position,
                                                     spawner.BotHp.transform.position);

            bool done = false;

            if (m.markers != null && m.markers.Length > 0)
            {
                done = CheckMarkers(m);
            }
            else if (m.check != null)
            {
                done = m.check(this);
                Progress = done ? m.goal : 0;
            }

            if (done)
            {
                _stateTimer += dt;
                if (_stateTimer > 1.2f)
                {
                    if (Index >= _missions.Count - 1) { Say("훈련을 모두 마쳤다"); StopMissions(); }
                    else { Say("통과"); Next(); }
                }
            }
        }

        bool CheckMarkers(Mission m)
        {
            if (spawner.PlayerHp == null) return false;
            Vector3 pos = spawner.PlayerHp.transform.position;

            for (int i = 0; i < _markerObjects.Count && i < m.markers.Length; i++)
            {
                if (_markerTaken[i]) continue;
                if (_markerObjects[i] == null) continue;
                if (Vector3.Distance(pos, _markerObjects[i].position) > 2.2f) continue;

                _markerTaken[i] = true;
                Progress++;
                if (_markerObjects[i] != null) Destroy(_markerObjects[i].gameObject);
            }
            return Progress >= m.goal;
        }

        // 장갑이 두꺼운 전용 허수아비. 장갑 무시 효과를 체감하는 미션에만 쓴다.
        void SpawnArmoredDummy(Mission m)
        {
            if (_armored != null) { Destroy(_armored.gameObject); _armored = null; }
            if (m == null || !m.armoredDummy || spawner == null) return;

            Vector3 pos = spawner.transform.position + new Vector3(5f, 0f, 3f);
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point;

            var go = new GameObject("장갑 허수아비");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var dmg = go.AddComponent<Damageable>();
            dmg.displayName = "장갑 허수아비";
            dmg.subtitle = "정면 장갑 150";
            dmg.maxHp = 600f;
            dmg.armor = new Rules.ArmorData { front = 150f, side = 60f, rear = 20f, top = 40f };
            dmg.ResetHp();
            dmg.onDeath += d => { if (Active) ArmoredKills++; };

            go.AddComponent<TrainingDummy>();
            _armored = dmg;
        }

        // --- 지점 표시 --------------------------------------------------

        void SpawnMarkers(Mission m)
        {
            ClearMarkers();
            if (m.markers == null) return;

            for (int i = 0; i < m.markers.Length; i++)
            {
                Vector3 p = m.markers[i];
                RaycastHit hit;
                if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                    p = hit.point;

                var go = new GameObject("MissionMarker_" + i);
                go.transform.position = p + Vector3.up * 0.6f;

                var mesh = ProcMesh.Tube(10, 3, 1.2f, 0.45f, 0.45f);
                PartShapes.Piece(go.transform, mesh, new Color(0.15f, 0.65f, 0.55f),
                                 Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
                go.AddComponent<MissionMarkerSpin>();
                _markerObjects.Add(go.transform);
            }
        }

        void ClearMarkers()
        {
            for (int i = 0; i < _markerObjects.Count; i++)
                if (_markerObjects[i] != null) Destroy(_markerObjects[i].gameObject);
            _markerObjects.Clear();
        }

        // --- 전투 신호 --------------------------------------------------

        void OnAnyDeath(Damageable target, bool byDot)
        {
            if (!Active || spawner == null) return;
            if (spawner.PlayerHp != null && target == spawner.PlayerHp) return;   // 내가 죽은 건 제외

            Kills++;
            if (byDot) DotKills++;
            if (spawner.BotHp != null && target == spawner.BotHp) BotKills++;

            // 봇을 쓰러뜨렸으면 어떤 전술이었는지 기록하고 다음 상대를 부른다
            if (spawner.BotHp != null && target == spawner.BotHp)
            {
                _beatenPresets.Add(spawner.BotPresetIndex);
                BotsBeaten = _beatenPresets.Count;

                var cur = Current;
                if (cur != null && cur.title.StartsWith("다섯 전술") && BotsBeaten < 5)
                    spawner.SetBotPreset((spawner.BotPresetIndex + 1) % spawner.BotPresetCount);
            }

            if (spawner.PlayerHp != null && spawner.BotHp != null && target == spawner.BotHp &&
                Vector3.Distance(spawner.PlayerHp.transform.position, target.transform.position) > 8f)
                FarKills++;
        }

        void OnAnyRooted(Damageable target, float seconds, Vector3 attackerPos)
        {
            if (!Active) return;

            // 내가 건 속박만 센다. 봇이 나를 묶은 것으로 미션이 진행되면 안 된다.
            if (spawner == null || spawner.PlayerHp == null) return;
            if (target == spawner.PlayerHp) return;
            if (Vector3.Distance(attackerPos, spawner.PlayerHp.transform.position) > 2.5f) return;

            RootHits++;
            if (Vector3.Distance(attackerPos, target.transform.position) >= 10f) LongRootHits++;

            // 지면보다 3m 이상 높은 곳에서 걸었는지
            if (spawner != null && spawner.PlayerHp != null && spawner.PlayerHp.transform.position.y > 3f)
                HighRootHits++;
        }

        void OnAnyDamaged(Damageable target, float dealt, Vector3 attackerPos)
        {
            if (!Active || spawner == null) return;
            var combat = spawner.PlayerCombat;
            if (combat == null) return;
            if (spawner.PlayerHp != null && target == spawner.PlayerHp) return;

            if (combat.Cloaked) CloakHits++;
        }

        public void NotifySpray() { if (Active) SprayUses++; }
        public void NotifyPounce() { if (Active) PounceHits++; }

        // 드론이 8m 밖에서 맞았을 때만 센다
        public void NotifyDroneHit(float distance)
        {
            if (Active && distance > 8f) DroneHits++;
        }
    }

    // 지점 표시가 천천히 돈다
    public class MissionMarkerSpin : MonoBehaviour
    {
        void Update()
        {
            transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
            var p = transform.position;
            p.y += Mathf.Sin(Time.time * 2f) * 0.003f;
            transform.position = p;
        }
    }
}
