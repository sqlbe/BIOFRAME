using System.Collections.Generic;
using UnityEngine;

namespace Bioframe.Assembly
{
    // 코드로 형태를 계산해 메시를 만든다.
    // 실사 모델(M4)로 갈아탈 때까지 쓰는 임시 형태지만, 박스보다 실루엣이 분명하다.
    public static class ProcMesh
    {
        // 휘어진 관. 마디마다 굵기가 달라진다. 다리, 꼬리, 뿔에 쓴다.
        public static Mesh Tube(int sides, int rings, float length, float startRadius, float endRadius,
                                float bend = 0f, float taperPower = 1f)
        {
            sides = Mathf.Max(3, sides);
            rings = Mathf.Max(2, rings);

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();

            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);
                float radius = Mathf.Lerp(startRadius, endRadius, Mathf.Pow(t, taperPower));

                // 진행 방향은 +Z, bend 값만큼 위로 휘어진다
                float z = length * t;
                float y = bend * length * t * t;
                float slope = 2f * bend * length * t / Mathf.Max(0.0001f, length);
                Vector3 center = new Vector3(0f, y, z);
                Quaternion rot = Quaternion.Euler(-Mathf.Atan(slope) * Mathf.Rad2Deg, 0f, 0f);

                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 local = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                    Vector3 n = rot * local.normalized;
                    verts.Add(center + rot * local);
                    norms.Add(n);
                }
            }

            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = r * sides + s;
                    int b = r * sides + (s + 1) % sides;
                    int c = (r + 1) * sides + s;
                    int d = (r + 1) * sides + (s + 1) % sides;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            // 양 끝 막기
            CapEnd(verts, norms, tris, 0, sides, Vector3.back);
            CapEnd(verts, norms, tris, (rings - 1) * sides, sides, Vector3.forward);

            return Finish(verts, norms, tris, "Tube");
        }

        static void CapEnd(List<Vector3> verts, List<Vector3> norms, List<int> tris, int ringStart, int sides, Vector3 normal)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < sides; i++) center += verts[ringStart + i];
            center /= sides;

            int c = verts.Count;
            verts.Add(center);
            norms.Add(normal);

            for (int s = 0; s < sides; s++)
            {
                int a = ringStart + s;
                int b = ringStart + (s + 1) % sides;
                if (normal.z > 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
                else { tris.Add(a); tris.Add(b); tris.Add(c); }
            }
        }

        // 안쪽으로 굽은 날. 사마귀 낫, 집게발, 송곳니에 쓴다.
        public static Mesh Blade(float length, float width, float thickness, float curve, int segments = 10,
                                 float tipSharpness = 0.12f, bool teeth = false)
        {
            segments = Mathf.Max(3, segments);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();

            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)(segments - 1);
                float z = length * t;
                float y = -curve * length * t * t;             // 아래로 굽는다
                float w = Mathf.Lerp(width, width * tipSharpness, Mathf.Pow(t, 0.8f));
                if (teeth && i % 2 == 1) w *= 1.18f;            // 톱니

                float th = Mathf.Lerp(thickness, thickness * 0.35f, t);

                verts.Add(new Vector3(-th, y, z)); norms.Add(Vector3.left);
                verts.Add(new Vector3(th, y, z)); norms.Add(Vector3.right);
                verts.Add(new Vector3(-th, y + w, z)); norms.Add(Vector3.left);
                verts.Add(new Vector3(th, y + w, z)); norms.Add(Vector3.right);
            }

            for (int i = 0; i < segments - 1; i++)
            {
                int b = i * 4, n = (i + 1) * 4;
                Quad(tris, b + 0, n + 0, b + 2, n + 2);   // 왼면
                Quad(tris, b + 3, n + 3, b + 1, n + 1);   // 오른면
                Quad(tris, b + 2, n + 2, b + 3, n + 3);   // 등
                Quad(tris, b + 1, n + 1, b + 0, n + 0);   // 날
            }

            return Finish(verts, norms, tris, "Blade");
        }

        static void Quad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(c); tris.Add(b); tris.Add(d);
        }

        // 둥근 덩어리. 몸통 마디, 갑각, 머리에 쓴다.
        public static Mesh Ellipsoid(Vector3 radii, int segments = 12, float flatten = 0f)
        {
            segments = Mathf.Max(4, segments);
            int rings = segments / 2 + 1;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();

            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = v * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments;
                    float theta = u * Mathf.PI * 2f;

                    Vector3 n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta),
                                            Mathf.Cos(phi),
                                            Mathf.Sin(phi) * Mathf.Sin(theta));
                    Vector3 p = new Vector3(n.x * radii.x, n.y * radii.y, n.z * radii.z);
                    if (flatten > 0f && p.y < 0f) p.y *= (1f - flatten);   // 아랫면을 눌러 납작하게

                    verts.Add(p);
                    norms.Add(n.normalized);
                }
            }

            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            return Finish(verts, norms, tris, "Ellipsoid");
        }

        // 뾰족한 침. 독침, 뿔, 발끝에 쓴다.
        public static Mesh Spike(float length, float radius, int sides = 6)
        {
            return Tube(sides, 6, length, radius, radius * 0.06f, 0f, 1.6f);
        }

        static Mesh Finish(List<Vector3> verts, List<Vector3> norms, List<int> tris, string name)
        {
            var m = new Mesh();
            m.name = "Proc_" + name;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.RecalculateNormals();
            return m;
        }
    }
}
