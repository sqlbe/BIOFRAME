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
        static readonly Color CoreColorAlly = new Color(0.16f, 0.42f, 0.39f);
        static readonly Color HeadColorAlly = new Color(0.10f, 0.32f, 0.30f);
        static readonly Color PartColorAlly = new Color(0.82f, 0.46f, 0.08f);

        static readonly Color CoreColorEnemy = new Color(0.52f, 0.16f, 0.16f);
        static readonly Color HeadColorEnemy = new Color(0.38f, 0.10f, 0.10f);
        static readonly Color PartColorEnemy = new Color(0.30f, 0.30f, 0.34f);

        // 현재 조립 중인 개체의 색. 적은 붉은 계열로 만든다.
        static bool _enemy;
        static Color CoreColor = CoreColorAlly;
        static Color HeadColor = HeadColorAlly;
        static Color PartColor = PartColorAlly;

        // "#RRGGBB" 문자열을 색으로. 값이 없으면 기본색을 쓴다.
        public static Color ColorOf(string hex, Color fallback)
        {
            float r, g, b;
            if (!HexColor.TryParse(hex, out r, out g, out b)) return fallback;
            return new Color(r, g, b);
        }

        // 적은 같은 색을 붉게 물들여 아군과 구분한다
        public static Color Enemify(Color c)
        {
            return Color.Lerp(c, new Color(0.62f, 0.14f, 0.12f), 0.55f);
        }

        public static GameObject Build(CoreData core, List<PartData> parts, Vector3 position, bool enemy = false)
        {
            Color coreBase = ColorOf(core.color, enemy ? CoreColorEnemy : CoreColorAlly);
            if (enemy) coreBase = Enemify(coreBase);
            CoreColor = coreBase;
            HeadColor = Color.Lerp(coreBase, Color.black, 0.25f);
            PartColor = enemy ? PartColorEnemy : PartColorAlly;
            _enemy = enemy;

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
                float side = flIndex == 0 ? -1f : 1f;
                float scale = 0.9f + 0.2f * SizeGrade.Of(p.size);
                var attached = PartShapes.Create(p, socket, PartColorOf(p), side, scale);
                attached.transform.localPosition = new Vector3(side * 0.12f, 0f, 0.1f);
                attached.transform.localRotation = Quaternion.Euler(-14f, side * 10f, 0f);
                visual.attachedParts[node] = attached.transform;
                flIndex++;
                if (flIndex > 1) break;
            }

            // 머리 파츠
            for (int i = 0; i < parts.Count; i++)
            {
                var hp = parts[i];
                if (hp == null || hp.socket != "HD") continue;
                var hs = visual.GetSocket("mount_HD_0");
                if (hs == null) break;
                float hscale = 0.85f + 0.2f * SizeGrade.Of(hp.size);
                var hgo = PartShapes.Create(hp, hs, PartColorOf(hp), 1f, hscale);
                hgo.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                visual.attachedParts["mount_HD_0"] = hgo.transform;
                break;
            }

            // 등, 꼬리 파츠
            AttachSimple(parts, visual, "DS", "mount_DS_0", new Vector3(0f, 0.12f, 0f), new Vector3(0.7f, 0.25f, 0.9f));
            AttachSimple(parts, visual, "TL", "mount_TL_0", new Vector3(0f, 0.05f, -0.25f), new Vector3(0.2f, 0.2f, 0.8f));

            var motor = root.AddComponent<SurfaceMotor>();
            motor.height = visual.bodyHeight + 0.2f;
            motor.radius = 0.5f;

            return root;
        }

        static void BuildInsectBody(CoreData core, FrameVisual v, Transform body)
        {
            v.bodyHeight = 1.05f;
            v.bodyLength = 1.9f;
            v.hipSpread = 0.30f;

            PartShapes.Piece(body, ProcMesh.Ellipsoid(new Vector3(0.26f, 0.20f, 0.30f), 12), HeadColor,
                             new Vector3(0f, v.bodyHeight, 1.15f), Quaternion.identity);
            PartShapes.Piece(body, ProcMesh.Ellipsoid(new Vector3(0.40f, 0.28f, 0.52f), 14, 0.35f), CoreColor,
                             new Vector3(0f, v.bodyHeight, 0.35f), Quaternion.identity);
            PartShapes.Piece(body, ProcMesh.Ellipsoid(new Vector3(0.45f, 0.33f, 0.68f), 14, 0.3f), CoreColor,
                             new Vector3(0f, v.bodyHeight + 0.05f, -0.85f), Quaternion.identity);

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
            v.hipSpread = 0.28f;

            PartShapes.Piece(body, ProcMesh.Ellipsoid(new Vector3(0.24f, 0.22f, 0.34f), 12), HeadColor,
                             new Vector3(0f, v.bodyHeight + 0.15f, 1.35f), Quaternion.identity);
            PartShapes.Piece(body, ProcMesh.Tube(8, 4, 0.5f, 0.16f, 0.20f), CoreColor,
                             new Vector3(0f, v.bodyHeight + 0.1f, 0.75f), Quaternion.Euler(0f, 180f, 0f));
            PartShapes.Piece(body, ProcMesh.Ellipsoid(new Vector3(0.42f, 0.35f, 0.95f), 14, 0.3f), CoreColor,
                             new Vector3(0f, v.bodyHeight, -0.1f), Quaternion.identity);

            AddSocket(v, body, "mount_HD_0", new Vector3(0f, v.bodyHeight + 0.4f, 1.55f));
            AddSocket(v, body, "mount_DS_0", new Vector3(0f, v.bodyHeight + 0.4f, 0.4f));
            AddSocket(v, body, "mount_DS_1", new Vector3(0f, v.bodyHeight + 0.4f, -0.4f));
            AddSocket(v, body, "mount_DS_2", new Vector3(0f, v.bodyHeight + 0.4f, -1.0f));
            AddSocket(v, body, "mount_TL_0", new Vector3(0f, v.bodyHeight, -1.3f));
        }

        static void AttachSimple(List<PartData> parts, FrameVisual visual, string socket, string node,
                                 Vector3 offset, Vector3 size)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null || p.socket != socket) continue;
                var s = visual.GetSocket(node);
                if (s == null) return;
                float scale = 0.9f + 0.25f * SizeGrade.Of(p.size);
                var go = PartShapes.Create(p, s, PartColorOf(p), 1f, scale);
                go.transform.localPosition = offset;
                if (socket == "TL") go.transform.localRotation = Quaternion.Euler(8f, 180f, 0f);
                visual.attachedParts[node] = go.transform;
                return;
            }
        }

        public static Color PartColorOf(PartData p)
        {
            Color c = ColorOf(p.color, PartColorAlly);
            return _enemy ? Enemify(c) : c;
        }

        static GameObject AttachBoxPart(PartData part, Transform socket, float sideSign, Color color)
        {
            float scale = 0.8f + 0.25f * SizeGrade.Of(part.size);
            var go = MakeBox(socket, part.id, new Vector3(sideSign * 0.15f, 0f, 0.35f),
                             new Vector3(0.22f, 0.22f, 0.9f) * scale, color);
            go.transform.localRotation = Quaternion.Euler(-20f, sideSign * 8f, 0f);
            return go;
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
            // 콜라이더가 한 프레임이라도 남으면 이동 처리가 그것을 지형으로 보고 밀어낸다
            if (col != null) Object.DestroyImmediate(col);

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
