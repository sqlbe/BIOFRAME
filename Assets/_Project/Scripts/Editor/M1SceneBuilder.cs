using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Bioframe.Assembly;
using Bioframe.Movement;
using Bioframe.Combat;
using Bioframe.UI;

namespace Bioframe.EditorTools
{
    // 메뉴에서 M1 테스트 씬을 한 번에 만든다. 손으로 오브젝트를 배치할 필요가 없다.
    public static class M1SceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/M1_Test.unity";

        [MenuItem("Tools/BIOFRAME/M1 테스트 씬 만들기")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 두께 있는 바닥. 얇은 판은 밀려나기나 돌진에 뚫릴 수 있다.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -1f, 0f);
            ground.transform.localScale = new Vector3(120f, 2f, 120f);
            Paint(ground, new Color(0.82f, 0.84f, 0.80f));

            // 경사, 계단, 벽: 다리 IK와 카메라를 확인할 지형
            var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "Slope";
            slope.transform.position = new Vector3(12f, 0.5f, 6f);
            slope.transform.localScale = new Vector3(10f, 0.5f, 14f);
            slope.transform.rotation = Quaternion.Euler(-14f, 0f, 0f);
            Paint(slope, new Color(0.86f, 0.87f, 0.84f));

            for (int i = 0; i < 6; i++)
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "Step_" + i;
                step.transform.position = new Vector3(-10f, 0.2f + i * 0.35f, -4f - i * 1.4f);
                step.transform.localScale = new Vector3(6f, 0.4f + i * 0.7f, 1.4f);
                Paint(step, new Color(0.88f, 0.89f, 0.86f));
            }

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall (벽 타기용 · M1 후반)";
            wall.transform.position = new Vector3(0f, 5f, 18f);
            wall.transform.localScale = new Vector3(20f, 10f, 1f);
            Paint(wall, new Color(0.74f, 0.78f, 0.80f));

            for (int i = 0; i < 8; i++)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "Rock_" + i;
                float a = i * Mathf.PI * 2f / 8f;
                rock.transform.position = new Vector3(Mathf.Cos(a) * 9f, 0.4f, Mathf.Sin(a) * 9f);
                rock.transform.localScale = new Vector3(1.6f, 0.8f + (i % 3) * 0.5f, 1.6f);
                rock.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
                Paint(rock, new Color(0.78f, 0.75f, 0.68f));
            }

            // 벽 타기 시험용 기둥과, 천장 이동 시험용 지붕
            var pillarPos = new Vector3[] { new Vector3(5f, 4f, 4f), new Vector3(-6f, 4f, 7f), new Vector3(2f, 4f, -7f) };
            for (int i = 0; i < pillarPos.Length; i++)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Pillar_" + i;
                pillar.transform.position = pillarPos[i];
                pillar.transform.localScale = new Vector3(2.2f, 8f, 2.2f);
                Paint(pillar, new Color(0.76f, 0.79f, 0.81f));
            }

            var roofLegA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofLegA.name = "RoofLeg_A";
            roofLegA.transform.position = new Vector3(-13f, 2.5f, -10f);
            roofLegA.transform.localScale = new Vector3(1.5f, 5f, 1.5f);
            Paint(roofLegA, new Color(0.76f, 0.79f, 0.81f));

            var roofLegB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofLegB.name = "RoofLeg_B";
            roofLegB.transform.position = new Vector3(-5f, 2.5f, -10f);
            roofLegB.transform.localScale = new Vector3(1.5f, 5f, 1.5f);
            Paint(roofLegB, new Color(0.76f, 0.79f, 0.81f));

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof (천장 이동 시험)";
            roof.transform.position = new Vector3(-9f, 5.2f, -10f);
            roof.transform.localScale = new Vector3(11f, 0.5f, 7f);
            Paint(roof, new Color(0.80f, 0.77f, 0.83f));

            BuildHall(new Vector3(18f, 0f, -14f));

            // 허수아비: 하나는 고정, 하나는 천천히 돌아 방향별 장갑 차이를 보여준다
            MakeDummy("허수아비 A", new Vector3(4f, 0f, -3f), Quaternion.Euler(0f, 180f, 0f), false);
            MakeDummy("허수아비 B (회전)", new Vector3(-3f, 0f, -4f), Quaternion.identity, true);
            MakeDummy("허수아비 C (건물 안)", new Vector3(20f, 0f, -12f), Quaternion.Euler(0f, 90f, 0f), false);

            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera));
            camGo.name = "Main Camera";
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<ThirdPersonCamera>();
            if (cam == null) cam = camGo.AddComponent<ThirdPersonCamera>();

            var spawnerGo = new GameObject("FrameSpawner");
            spawnerGo.transform.position = new Vector3(0f, 0.5f, 0f);
            var spawner = spawnerGo.AddComponent<FrameSpawner>();
            spawner.cam = cam;

            var uiGo = new GameObject("UI");
            var screen = uiGo.AddComponent<AssemblyScreen>();
            screen.spawner = spawner;

            var hud = uiGo.AddComponent<CombatHud>();
            hud.spawner = spawner;
            hud.assembly = screen;

            var matchGo = new GameObject("MatchManager");
            var match = matchGo.AddComponent<Bioframe.Match.MatchManager>();
            match.spawner = spawner;
            match.assembly = screen;
            hud.match = match;

            var missionGo = new GameObject("MissionManager");
            var mission = missionGo.AddComponent<Bioframe.Match.MissionManager>();
            mission.spawner = spawner;
            mission.match = match;
            hud.mission = mission;

            var menu = uiGo.AddComponent<StartMenu>();
            menu.match = match;
            menu.mission = mission;
            menu.assembly = screen;
            menu.spawner = spawner;
            match.autoStart = false;

            var lightGo = GameObject.Find("Directional Light");
            if (lightGo != null)
            {
                lightGo.transform.rotation = Quaternion.Euler(45f, 35f, 0f);
                var l = lightGo.GetComponent<Light>();
                if (l != null) { l.intensity = 1.35f; l.shadows = LightShadows.Soft; l.color = new Color(1f, 0.98f, 0.94f); }
            }

            // 밝은 화면: 하늘색 배경과 밝은 주변광
            var camComp = camGo.GetComponent<Camera>();
            if (camComp != null)
            {
                camComp.clearFlags = CameraClearFlags.SolidColor;
                camComp.backgroundColor = new Color(0.86f, 0.90f, 0.94f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.90f, 0.93f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.80f, 0.83f, 0.86f);
            RenderSettings.ambientGroundColor = new Color(0.68f, 0.70f, 0.70f);
            RenderSettings.fog = false;

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[BIOFRAME] M1 테스트 씬을 만들었다: " + ScenePath + "  (Play 버튼을 누르면 조작할 수 있다)");
        }

        static void MakeDummy(string name, Vector3 pos, Quaternion rot, bool rotate)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.rotation = rot;
            var dmg = go.AddComponent<Damageable>();
            dmg.displayName = name;
            var dummy = go.AddComponent<TrainingDummy>();
            dummy.rotateSlowly = rotate;
        }

        // 기둥 4개 + 천장 + 벽 2면으로 된 건물. 천장 이동과 실내 전투를 시험한다.
        static void BuildHall(Vector3 origin)
        {
            var root = new GameObject("Hall (기둥·천장 건물)");
            root.transform.position = origin;

            float half = 6f;        // 건물 반너비
            float ceiling = 5.5f;   // 천장 높이
            var pillarColor = new Color(0.76f, 0.79f, 0.81f);
            var wallColor = new Color(0.72f, 0.76f, 0.78f);

            // 모서리 기둥 4개
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sz = (i < 2) ? -1f : 1f;
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Hall_Pillar_" + i;
                pillar.transform.SetParent(root.transform, false);
                pillar.transform.localPosition = new Vector3(sx * (half - 0.8f), ceiling * 0.5f, sz * (half - 0.8f));
                pillar.transform.localScale = new Vector3(1.3f, ceiling, 1.3f);
                Paint(pillar, pillarColor);
            }

            // 가운데 기둥 하나 더: 천장으로 올라가는 지름길
            var center = GameObject.CreatePrimitive(PrimitiveType.Cube);
            center.name = "Hall_Pillar_Center";
            center.transform.SetParent(root.transform, false);
            center.transform.localPosition = new Vector3(0f, ceiling * 0.5f, 0f);
            center.transform.localScale = new Vector3(1.6f, ceiling, 1.6f);
            Paint(center, pillarColor);

            // 천장
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Hall_Ceiling";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, ceiling + 0.3f, 0f);
            roof.transform.localScale = new Vector3(half * 2f, 0.6f, half * 2f);
            Paint(roof, new Color(0.80f, 0.77f, 0.83f));

            // 벽 2면만 세우고 나머지 2면은 열어 둔다
            var wallA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallA.name = "Hall_Wall_A";
            wallA.transform.SetParent(root.transform, false);
            wallA.transform.localPosition = new Vector3(0f, ceiling * 0.5f, -half);
            wallA.transform.localScale = new Vector3(half * 2f, ceiling, 0.5f);
            Paint(wallA, wallColor);

            var wallB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallB.name = "Hall_Wall_B";
            wallB.transform.SetParent(root.transform, false);
            wallB.transform.localPosition = new Vector3(-half, ceiling * 0.5f, 0f);
            wallB.transform.localScale = new Vector3(0.5f, ceiling, half * 2f);
            Paint(wallB, wallColor);

            // 실내 장애물: 엄폐물 겸 낮은 단
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Hall_Crate";
            crate.transform.SetParent(root.transform, false);
            crate.transform.localPosition = new Vector3(2.5f, 0.6f, -2.5f);
            crate.transform.localScale = new Vector3(2.2f, 1.2f, 2.2f);
            Paint(crate, new Color(0.80f, 0.76f, 0.66f));
        }

        static void Paint(GameObject go, Color color)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mr.sharedMaterial = mat;
        }
    }
}
