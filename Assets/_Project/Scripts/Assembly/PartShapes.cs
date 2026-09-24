using UnityEngine;
using Bioframe.Rules;

namespace Bioframe.Assembly
{
    // 파츠 데이터의 shape 값에 따라 형태를 만든다.
    // shape가 없으면 소켓 종류로 기본 형태를 고른다.
    public static class PartShapes
    {
        public static GameObject Create(PartData part, Transform socket, Color color, float sideSign, float scale)
        {
            string shape = string.IsNullOrEmpty(part.shape) ? DefaultShape(part) : part.shape;

            var root = new GameObject(part.id);
            root.transform.SetParent(socket, false);

            switch (shape)
            {
                case "scythe": BuildScythe(root.transform, color, scale, sideSign); break;
                case "claw": BuildClaw(root.transform, color, scale, sideSign); break;
                case "club": BuildClub(root.transform, color, scale, sideSign); break;
                case "drill": BuildDrill(root.transform, color, scale, sideSign); break;
                case "mandible": BuildMandible(root.transform, color, scale); break;
                case "fang": BuildFang(root.transform, color, scale); break;
                case "spinneret": BuildSpinneret(root.transform, color, scale); break;
                case "sensor": BuildSensor(root.transform, color, scale); break;
                case "carapace": BuildCarapace(root.transform, color, scale); break;
                case "nozzle": BuildNozzle(root.transform, color, scale); break;
                case "pod": BuildPod(root.transform, color, scale); break;
                case "sting": BuildSting(root.transform, color, scale); break;
                case "tail": BuildTail(root.transform, color, scale); break;
                case "shell": BuildShell(root.transform, color, scale); break;
                default: BuildBlock(root.transform, color, scale); break;
            }

            return root;
        }

        static string DefaultShape(PartData p)
        {
            switch (p.socket)
            {
                case "FL": return "claw";
                case "HD": return "mandible";
                case "DS": return "carapace";
                case "TL": return "tail";
                case "SK": return "shell";
                default: return "block";
            }
        }

        // --- 형태들 -------------------------------------------------------

        static void BuildScythe(Transform p, Color c, float s, float side)
        {
            // 위팔 + 아래팔 + 안쪽으로 굽은 낫날
            Piece(p, ProcMesh.Tube(7, 5, 0.42f * s, 0.10f * s, 0.075f * s), c,
                  Vector3.zero, Quaternion.Euler(-25f, 0f, 0f));
            Piece(p, ProcMesh.Tube(7, 5, 0.38f * s, 0.075f * s, 0.05f * s), c,
                  new Vector3(0f, -0.16f * s, 0.38f * s), Quaternion.Euler(35f, 0f, 0f));
            Piece(p, ProcMesh.Blade(0.62f * s, 0.14f * s, 0.028f * s, 0.55f, 12, 0.1f, true),
                  Brighter(c), new Vector3(0f, -0.02f * s, 0.62f * s), Quaternion.Euler(-10f, 0f, side * 6f));
        }

        static void BuildClaw(Transform p, Color c, float s, float side)
        {
            Piece(p, ProcMesh.Tube(8, 4, 0.40f * s, 0.11f * s, 0.10f * s), c,
                  Vector3.zero, Quaternion.Euler(-12f, 0f, 0f));
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.14f, 0.12f, 0.22f) * s, 10), c,
                  new Vector3(0f, 0f, 0.52f * s), Quaternion.identity);
            // 위아래로 벌어진 집게
            Piece(p, ProcMesh.Blade(0.34f * s, 0.10f * s, 0.035f * s, 0.35f, 8), Brighter(c),
                  new Vector3(0f, 0.05f * s, 0.62f * s), Quaternion.Euler(-16f, 0f, 0f));
            Piece(p, ProcMesh.Blade(0.30f * s, 0.09f * s, 0.032f * s, -0.30f, 8), Brighter(c),
                  new Vector3(0f, -0.06f * s, 0.62f * s), Quaternion.Euler(14f, 0f, 180f));
        }

        static void BuildClub(Transform p, Color c, float s, float side)
        {
            Piece(p, ProcMesh.Tube(7, 4, 0.46f * s, 0.09f * s, 0.08f * s), c,
                  Vector3.zero, Quaternion.Euler(-18f, 0f, 0f));
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.17f, 0.15f, 0.20f) * s, 10), Brighter(c),
                  new Vector3(0f, -0.06f * s, 0.62f * s), Quaternion.identity);
        }

        static void BuildDrill(Transform p, Color c, float s, float side)
        {
            Piece(p, ProcMesh.Tube(7, 4, 0.38f * s, 0.10f * s, 0.09f * s), c, Vector3.zero, Quaternion.identity);
            Piece(p, ProcMesh.Spike(0.46f * s, 0.11f * s, 7), Brighter(c),
                  new Vector3(0f, 0f, 0.38f * s), Quaternion.identity);
        }

        static void BuildMandible(Transform p, Color c, float s)
        {
            // 좌우로 벌어진 큰턱
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.13f, 0.11f, 0.16f) * s, 10), c, Vector3.zero, Quaternion.identity);
            Piece(p, ProcMesh.Blade(0.36f * s, 0.09f * s, 0.03f * s, 0.45f, 8, 0.15f, true), Brighter(c),
                  new Vector3(-0.09f * s, 0f, 0.14f * s), Quaternion.Euler(0f, -18f, 90f));
            Piece(p, ProcMesh.Blade(0.36f * s, 0.09f * s, 0.03f * s, 0.45f, 8, 0.15f, true), Brighter(c),
                  new Vector3(0.09f * s, 0f, 0.14f * s), Quaternion.Euler(0f, 18f, -90f));
        }

        static void BuildFang(Transform p, Color c, float s)
        {
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.14f, 0.12f, 0.18f) * s, 10), c, Vector3.zero, Quaternion.identity);
            Piece(p, ProcMesh.Spike(0.22f * s, 0.035f * s, 6), Brighter(c),
                  new Vector3(-0.055f * s, -0.05f * s, 0.16f * s), Quaternion.Euler(18f, 0f, 0f));
            Piece(p, ProcMesh.Spike(0.22f * s, 0.035f * s, 6), Brighter(c),
                  new Vector3(0.055f * s, -0.05f * s, 0.16f * s), Quaternion.Euler(18f, 0f, 0f));
        }

        static void BuildSpinneret(Transform p, Color c, float s)
        {
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.12f, 0.11f, 0.15f) * s, 10), c, Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 3; i++)
            {
                float a = -0.06f + i * 0.06f;
                Piece(p, ProcMesh.Tube(6, 3, 0.16f * s, 0.028f * s, 0.018f * s), Brighter(c),
                      new Vector3(a * s, -0.02f * s, 0.14f * s), Quaternion.Euler(6f, a * 120f, 0f));
            }
        }

        static void BuildSensor(Transform p, Color c, float s)
        {
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.10f, 0.09f, 0.12f) * s, 8), c, Vector3.zero, Quaternion.identity);
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.09f, 0.09f, 0.09f) * s, 10), Brighter(c),
                  new Vector3(-0.09f * s, 0.03f * s, 0.10f * s), Quaternion.identity);
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.09f, 0.09f, 0.09f) * s, 10), Brighter(c),
                  new Vector3(0.09f * s, 0.03f * s, 0.10f * s), Quaternion.identity);
        }

        static void BuildCarapace(Transform p, Color c, float s)
        {
            // 가운데가 갈라진 등껍질
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.30f, 0.16f, 0.42f) * s, 12, 0.85f), c,
                  new Vector3(-0.16f * s, 0f, 0f), Quaternion.Euler(0f, 0f, -8f));
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.30f, 0.16f, 0.42f) * s, 12, 0.85f), c,
                  new Vector3(0.16f * s, 0f, 0f), Quaternion.Euler(0f, 0f, 8f));
        }

        static void BuildNozzle(Transform p, Color c, float s)
        {
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.18f, 0.14f, 0.24f) * s, 10), c, Vector3.zero, Quaternion.identity);
            Piece(p, ProcMesh.Tube(7, 3, 0.26f * s, 0.07f * s, 0.10f * s), Brighter(c),
                  new Vector3(0f, 0.02f * s, -0.20f * s), Quaternion.Euler(0f, 180f, 0f));
        }

        static void BuildPod(Transform p, Color c, float s)
        {
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.22f, 0.14f, 0.30f) * s, 10, 0.6f), c, Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                    Piece(p, ProcMesh.Tube(6, 2, 0.10f * s, 0.035f * s, 0.035f * s), Brighter(c),
                          new Vector3((-0.10f + i * 0.10f) * s, (0.02f + j * 0.08f) * s, 0.28f * s), Quaternion.identity);
        }

        static void BuildSting(Transform p, Color c, float s)
        {
            Piece(p, ProcMesh.Tube(7, 6, 0.50f * s, 0.10f * s, 0.06f * s, 0.55f), c,
                  Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
            Piece(p, ProcMesh.Spike(0.26f * s, 0.055f * s, 7), Brighter(c),
                  new Vector3(0f, 0.30f * s, -0.44f * s), Quaternion.Euler(140f, 0f, 0f));
        }

        static void BuildTail(Transform p, Color c, float s)
        {
            Piece(p, ProcMesh.Tube(7, 6, 0.62f * s, 0.10f * s, 0.035f * s, 0.25f), c,
                  Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
        }

        static void BuildShell(Transform p, Color c, float s)
        {
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.34f, 0.20f, 0.46f) * s, 12, 0.7f), c,
                  Vector3.zero, Quaternion.identity);
        }

        static void BuildBlock(Transform p, Color c, float s)
        {
            Piece(p, ProcMesh.Ellipsoid(new Vector3(0.12f, 0.12f, 0.24f) * s, 8), c, Vector3.zero, Quaternion.identity);
        }

        // --- 도우미 -------------------------------------------------------

        static Color Brighter(Color c) { return Color.Lerp(c, Color.white, 0.25f); }

        public static GameObject Piece(Transform parent, Mesh mesh, Color color, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("piece");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = LitMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            mr.SetPropertyBlock(block);
            return go;
        }

        static Material _lit;
        public static Material LitMaterial()
        {
            if (_lit != null) return _lit;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            _lit = new Material(sh);
            return _lit;
        }
    }
}
