using System.Collections.Generic;
using UnityEngine;

namespace Bioframe.Combat
{
    // 코드로 그리는 전투 연출. 모델이나 이펙트 에셋 없이 동작한다.
    // 실사 모델 단계(M4)에서 진짜 이펙트로 갈아탈 때도 호출 지점은 그대로 둔다.
    public static class Fx
    {
        public static Material UnlitMaterial(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        // 맞은 개체를 잠깐 번쩍이게 한다
        public static void Flash(GameObject root, Color color, float duration = 0.12f)
        {
            if (root == null) return;
            var f = root.GetComponent<FxFlash>();
            if (f == null) f = root.AddComponent<FxFlash>();
            f.Play(color, duration);
        }

        // 타격 지점에 파편을 튀긴다
        public static void Spark(Vector3 pos, Vector3 dir, Color color, int count = 7, float power = 4.5f)
        {
            var go = new GameObject("FxSpark");
            go.transform.position = pos;
            go.AddComponent<FxSpark>().Play(dir, color, count, power);
        }

        // 충격파 고리
        public static void Ring(Vector3 pos, Vector3 normal, Color color, float radius = 1.6f, float duration = 0.35f)
        {
            var go = new GameObject("FxRing");
            go.transform.position = pos;
            go.AddComponent<FxRing>().Play(normal, color, radius, duration);
        }

        // 속박된 대상을 줄로 묶는 표현
        public static void Bind(Transform target, float duration, Color color)
        {
            if (target == null) return;
            var b = target.GetComponent<FxBind>();
            if (b == null) b = target.gameObject.AddComponent<FxBind>();
            b.Play(duration, color);
        }

        // 빠르게 움직일 때 남는 잔상
        public static void Afterimage(GameObject root, Color color, float duration, float interval = 0.045f)
        {
            if (root == null) return;
            var a = root.GetComponent<FxAfterimage>();
            if (a == null) a = root.AddComponent<FxAfterimage>();
            a.Play(color, duration, interval);
        }

        // 발밑 먼지 한 뭉치
        public static void Dust(Vector3 pos, Vector3 dir, Color color, int count = 4, float power = 1.6f)
        {
            var go = new GameObject("FxDust");
            go.transform.position = pos;
            go.AddComponent<FxDust>().Play(dir, color, count, power);
        }

        public static void Shake(float amplitude, float duration)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var tp = cam.GetComponent<Bioframe.Movement.ThirdPersonCamera>();
            if (tp != null) tp.Shake(amplitude, duration);
        }

        // 카메라가 보고 있는 대상인지. 내 기체가 맞았을 때만 화면을 흔들기 위해 쓴다.
        public static bool IsCameraTarget(Transform t)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var tp = cam.GetComponent<Bioframe.Movement.ThirdPersonCamera>();
            return tp != null && tp.target == t;
        }
    }

    public class FxFlash : MonoBehaviour
    {
        readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        readonly List<Color> _original = new List<Color>();
        float _time, _duration;
        Color _color;
        bool _active;

        public void Play(Color color, float duration)
        {
            if (_renderers.Count == 0)
            {
                GetComponentsInChildren(true, _renderers);
                for (int i = 0; i < _renderers.Count; i++)
                {
                    var block = new MaterialPropertyBlock();
                    _renderers[i].GetPropertyBlock(block);
                    _original.Add(block.HasColor("_BaseColor") ? block.GetColor("_BaseColor") : Color.gray);
                }
            }
            _color = color;
            _duration = duration;
            _time = duration;
            _active = true;
        }

        void Update()
        {
            if (!_active) return;
            _time -= Time.deltaTime;
            float k = Mathf.Clamp01(_time / Mathf.Max(0.01f, _duration));

            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null) continue;
                var block = new MaterialPropertyBlock();
                Color c = Color.Lerp(_original[i], _color, k);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                _renderers[i].SetPropertyBlock(block);
            }

            if (_time <= 0f) _active = false;
        }
    }

    // 몸통 박스를 복사해 잠깐 남겼다가 지운다
    public class FxAfterimage : MonoBehaviour
    {
        float _time, _interval, _timer;
        Color _color;

        public void Play(Color color, float duration, float interval)
        {
            _color = color;
            _time = Mathf.Max(_time, duration);
            _interval = interval;
            _timer = 0f;
        }

        void Update()
        {
            if (_time <= 0f) return;
            float dt = Time.deltaTime;
            _time -= dt;
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = _interval;

            var filters = GetComponentsInChildren<MeshFilter>();
            var ghostRoot = new GameObject("FxGhost");
            ghostRoot.transform.position = transform.position;
            ghostRoot.transform.rotation = transform.rotation;

            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] == null || filters[i].sharedMesh == null) continue;
                var go = new GameObject("g");
                go.transform.SetParent(ghostRoot.transform, false);
                go.transform.position = filters[i].transform.position;
                go.transform.rotation = filters[i].transform.rotation;
                go.transform.localScale = filters[i].transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = filters[i].sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Fx.UnlitMaterial(_color);
            }

            ghostRoot.AddComponent<FxFade>().Play(0.28f);
        }
    }

    // 잠깐 있다가 사라지는 물체
    public class FxFade : MonoBehaviour
    {
        float _time, _duration;

        public void Play(float duration) { _duration = duration; _time = duration; }

        void Update()
        {
            _time -= Time.deltaTime;
            float k = Mathf.Clamp01(_time / Mathf.Max(0.01f, _duration));
            transform.localScale = Vector3.one * (0.6f + 0.4f * k);
            if (_time <= 0f) Destroy(gameObject);
        }
    }

    public class FxDust : MonoBehaviour
    {
        struct Puff { public Transform t; public Vector3 vel; }
        readonly System.Collections.Generic.List<Puff> _puffs = new System.Collections.Generic.List<Puff>();
        float _life = 0.7f;

        public void Play(Vector3 dir, Color color, int count, float power)
        {
            var mat = Fx.UnlitMaterial(color);
            for (int i = 0; i < count; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var col = cube.GetComponent<Collider>();
                if (col != null) Destroy(col);
                cube.transform.SetParent(transform, false);
                cube.transform.localPosition = Random.insideUnitSphere * 0.25f;
                cube.transform.localScale = Vector3.one * Random.Range(0.12f, 0.26f);
                cube.transform.localRotation = Random.rotation;
                var mr = cube.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;

                var p = new Puff();
                p.t = cube.transform;
                p.vel = (dir.normalized * 0.6f + Vector3.up * 0.5f + Random.insideUnitSphere * 0.5f) * power;
                _puffs.Add(p);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            for (int i = 0; i < _puffs.Count; i++)
            {
                var p = _puffs[i];
                if (p.t == null) continue;
                p.vel *= (1f - 2.2f * dt);
                p.t.position += p.vel * dt;
                p.t.localScale *= (1f + 0.9f * dt);
                _puffs[i] = p;
            }
            if (_life <= 0f) Destroy(gameObject);
        }
    }

    public class FxSpark : MonoBehaviour
    {
        struct Bit { public Transform t; public Vector3 vel; }
        readonly List<Bit> _bits = new List<Bit>();
        float _life = 0.55f;

        public void Play(Vector3 dir, Color color, int count, float power)
        {
            var mat = Fx.UnlitMaterial(color);
            for (int i = 0; i < count; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var col = cube.GetComponent<Collider>();
                if (col != null) Destroy(col);
                cube.transform.SetParent(transform, false);
                cube.transform.localScale = Vector3.one * Random.Range(0.06f, 0.16f);
                cube.transform.localRotation = Random.rotation;
                var mr = cube.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;

                var b = new Bit();
                b.t = cube.transform;
                b.vel = (dir.normalized + Random.insideUnitSphere * 0.8f).normalized * Random.Range(power * 0.4f, power);
                _bits.Add(b);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;

            for (int i = 0; i < _bits.Count; i++)
            {
                var b = _bits[i];
                if (b.t == null) continue;
                b.vel += Vector3.down * (14f * dt);
                b.t.position += b.vel * dt;
                b.t.localScale *= (1f - 2.2f * dt);
                _bits[i] = b;
            }

            if (_life <= 0f) Destroy(gameObject);
        }
    }

    public class FxRing : MonoBehaviour
    {
        LineRenderer _line;
        float _time, _duration, _radius;

        public void Play(Vector3 normal, Color color, float radius, float duration)
        {
            Vector3 n = normal.sqrMagnitude < 0.001f ? Vector3.up : normal.normalized;
            _radius = radius;
            _duration = duration;
            _time = duration;

            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.loop = true;
            _line.positionCount = 24;
            _line.startWidth = 0.12f;
            _line.endWidth = 0.12f;
            _line.sharedMaterial = Fx.UnlitMaterial(color);
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            transform.rotation = Quaternion.LookRotation(n);
        }

        void Update()
        {
            _time -= Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_time / Mathf.Max(0.01f, _duration));
            float r = _radius * (0.2f + 0.8f * k);

            for (int i = 0; i < _line.positionCount; i++)
            {
                float a = i / (float)_line.positionCount * Mathf.PI * 2f;
                _line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            _line.startWidth = _line.endWidth = 0.14f * (1f - k);

            if (_time <= 0f) Destroy(gameObject);
        }
    }

    public class FxBind : MonoBehaviour
    {
        LineRenderer[] _strands;
        float _time;
        Transform _shakeRoot;
        Vector3 _shakeBase;

        public void Play(float duration, Color color)
        {
            _time = Mathf.Max(_time, duration);
            if (_strands != null) return;

            _strands = new LineRenderer[6];
            var mat = Fx.UnlitMaterial(color);
            for (int i = 0; i < _strands.Length; i++)
            {
                var go = new GameObject("Strand_" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 3;
                lr.startWidth = 0.05f;
                lr.endWidth = 0.02f;
                lr.sharedMaterial = mat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _strands[i] = lr;
            }

            var body = transform.Find("Body");
            if (body != null) { _shakeRoot = body; _shakeBase = body.localPosition; }
        }

        void Update()
        {
            _time -= Time.deltaTime;

            if (_strands != null)
            {
                for (int i = 0; i < _strands.Length; i++)
                {
                    if (_strands[i] == null) continue;
                    float a = (i / (float)_strands.Length) * Mathf.PI * 2f + Time.time * 0.6f;
                    Vector3 anchor = new Vector3(Mathf.Cos(a) * 1.5f, 0.05f, Mathf.Sin(a) * 1.5f);
                    Vector3 mid = new Vector3(Mathf.Cos(a) * 0.7f, 0.8f + Mathf.Sin(Time.time * 9f + i) * 0.08f, Mathf.Sin(a) * 0.7f);
                    _strands[i].SetPosition(0, anchor);
                    _strands[i].SetPosition(1, mid);
                    _strands[i].SetPosition(2, new Vector3(0f, 1.1f, 0f));
                }
            }

            // 묶인 채 버둥거리는 떨림
            if (_shakeRoot != null)
                _shakeRoot.localPosition = _shakeBase + new Vector3(Mathf.Sin(Time.time * 34f) * 0.035f, 0f, Mathf.Cos(Time.time * 29f) * 0.03f);

            if (_time <= 0f)
            {
                if (_shakeRoot != null) _shakeRoot.localPosition = _shakeBase;
                if (_strands != null)
                {
                    for (int i = 0; i < _strands.Length; i++)
                        if (_strands[i] != null) Destroy(_strands[i].gameObject);
                }
                _strands = null;
                Destroy(this);
            }
        }
    }
}
