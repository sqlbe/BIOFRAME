using System.Collections.Generic;
using UnityEngine;
using Bioframe.Rules;
using Bioframe.Movement;

namespace Bioframe.Assembly
{
    // 코어와 파츠 데이터를 받아 박스로 된 임시 캐릭터를 만든다.
    // 실사 모델 단계(M4)에서는 박스 대신 glb 프리팹을 소켓에 붙이도록 바꾸면 된다.
    public static class FrameBuilder
    {
        static readonly Color CoreColor = new Color(0.16f, 0.42f, 0.39f);
        static readonly Color HeadColor = new Color(0.10f, 0.32f, 0.30f);
        static readonly Color PartColor = new Color(0.82f, 0.46f, 0.08f);

        public static GameObject Build(CoreData core, List<PartData> parts, Vector3 position)
        {
            var root = new GameObject("Frame_" + core.id);
            root.transform.position = position;

            var visual = root.AddComponent<FrameVisual>();
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            visual.body = body;

            if (core.id == "QUA") BuildQuadBody(core, visual, body);
            else BuildInsectBody(core, visual, body);

            // 앞다리(FL) 파츠를 어깨 소켓에 붙인다. 좌우가 다른 파츠여도 된다.
            int flIndex = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null || p.socket != "FL") continue;
                string node = flIndex == 0 ? "mount_FL_L" : "mount_FL_R";
                var socket = visual.GetSocket(node);
                if (socket == null) continue;
                AttachBoxPart(p, socket, flIndex == 0 ? -1f : 1f);
                flIndex++;
                if (flIndex > 1) break;
            }

            var motor = root.AddComponent<SurfaceMotor>();
            motor.height = visual.bodyHeight + 0.2f;
            motor.radius = 0.5f;

            return root;
        }

        static void BuildInsectBody(CoreData core, FrameVisual v, Transform body)
        {
            v.bodyHeight = 1.05f;
            v.bodyLength = 1.9f;
            v.hipSpread = 0.58f;

            MakeBox(body, "Head", new Vector3(0f, v.bodyHeight, 1.15f), new Vector3(0.5f, 0.4f, 0.6f), HeadColor);
            MakeBox(body, "Thorax", new Vector3(0f, v.bodyHeight, 0.35f), new Vector3(0.8f, 0.55f, 1.0f), CoreColor);
            MakeBox(body, "Abdomen", new Vector3(0f, v.bodyHeight + 0.05f, -0.85f), new Vector3(0.9f, 0.65f, 1.3f), CoreColor);

            AddSocket(v, body, "mount_HD_0", new Vector3(0f, v.bodyHeight + 0.15f, 1.45f));
            AddSocket(v, body, "mount_FL_L", new Vector3(-0.45f, v.bodyHeight + 0.05f, 0.7f));
            AddSocket(v, body, "mount_FL_R", new Vector3(0.45f, v.bodyHeight + 0.05f, 0.7f));
            AddSocket(v, body, "mount_DS_0", new Vector3(0f, v.bodyHeight + 0.35f, 0.35f));
            AddSocket(v, body, "mount_DS_1", new Vector3(0f, v.bodyHeight + 0.4f, -0.85f));
            AddSocket(v, body, "mount_TL_0", new Vector3(0f, v.bodyHeight, -1.55f));
        }

        static void BuildQuadBody(CoreData core, FrameVisual v, Transform body)
        {
            v.bodyHeight = 1.25f;
            v.bodyLength = 2.1f;
            v.hipSpread = 0.5f;

            MakeBox(body, "Head", new Vector3(0f, v.bodyHeight + 0.15f, 1.35f), new Vector3(0.45f, 0.45f, 0.7f), HeadColor);
            MakeBox(body, "Neck", new Vector3(0f, v.bodyHeight + 0.1f, 0.95f), new Vector3(0.35f, 0.35f, 0.5f), CoreColor);
            MakeBox(body, "Torso", new Vector3(0f, v.bodyHeight, -0.1f), new Vector3(0.85f, 0.7f, 1.9f), CoreColor);

            AddSocket(v, body, "mount_HD_0", new Vector3(0f, v.bodyHeight + 0.4f, 1.55f));
            AddSocket(v, body, "mount_DS_0", new Vector3(0f, v.bodyHeight + 0.4f, 0.4f));
            AddSocket(v, body, "mount_DS_1", new Vector3(0f, v.bodyHeight + 0.4f, -0.4f));
            AddSocket(v, body, "mount_DS_2", new Vector3(0f, v.bodyHeight + 0.4f, -1.0f));
            AddSocket(v, body, "mount_TL_0", new Vector3(0f, v.bodyHeight, -1.3f));
        }

        static void AttachBoxPart(PartData part, Transform socket, float sideSign)
        {
            float scale = 0.8f + 0.25f * SizeGrade.Of(part.size);
            var go = MakeBox(socket, part.id, new Vector3(sideSign * 0.15f, 0f, 0.35f),
                             new Vector3(0.22f, 0.22f, 0.9f) * scale, PartColor);
            go.transform.localRotation = Quaternion.Euler(-20f, sideSign * 8f, 0f);
        }

        static void AddSocket(FrameVisual v, Transform parent, string node, Vector3 localPos)
        {
            var t = new GameObject(node).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            v.sockets[node] = t;
        }

        public static GameObject MakeBox(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);   // 몸통 충돌은 CharacterController가 담당
                else Object.DestroyImmediate(col);
            }

            SetColor(go, color);
            return go;
        }

        public static void SetColor(GameObject go, Color color)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);   // URP Lit
            block.SetColor("_Color", color);       // 내장 파이프라인 대비
            mr.SetPropertyBlock(block);
        }
    }
}
