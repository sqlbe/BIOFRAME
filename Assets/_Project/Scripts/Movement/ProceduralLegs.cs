using System.Collections.Generic;
using UnityEngine;
using Bioframe.Assembly;

namespace Bioframe.Movement
{
    // 다리를 코드로 움직인다. 다리 개수와 무관하게 같은 코드가 걷게 만드는 것이
    // 이 게임 자유도의 핵심이다(기획서 §1 핵심 결정).
    public class ProceduralLegs : MonoBehaviour
    {
        [Header("규격")]
        public int legCount = 6;
        public float bodyHeight = 1.05f;
        public float bodyLength = 1.9f;
        public float hipSpread = 0.58f;

        [Header("보행")]
        public float stepDistance = 0.55f;
        public float stepDuration = 0.16f;
        public float stepHeight = 0.32f;
        public float strideAhead = 0.35f;   // 진행 방향으로 발을 미리 내딛는 정도
        public float legReach = 1.3f;
        public float stanceOutward = 0.55f;   // 다리를 몸통 바깥으로 벌리는 정도
        public float bodyFollowSpeed = 8f;

        [Header("모양")]
        public Color legColor = new Color(0.09f, 0.45f, 0.42f);
        public float legWidth = 0.09f;

        class Leg
        {
            public Vector3 hipLocal;
            public float side;          // -1 왼쪽, +1 오른쪽
            public int group;           // 0/1 교대로 내딛는다
            public Vector3 foot;
            public Vector3 stepFrom, stepTo;
            public float t = 1f;
            public bool stepping;
            public Transform upper, lower;   // 넓적다리, 종아리
        }

        readonly List<Leg> _legs = new List<Leg>();
        Transform _body;
        Vector3 _lastPos;
        Vector3 _velocity;
        float _bodyY;
        int _steppingGroup = -1;
        Mesh _segmentMesh;

        public void Setup(int legs, Transform body, float height, float length, float spread, Color? color = null)
        {
            if (color.HasValue) legColor = color.Value;
            legCount = Mathf.Max(2, legs);
            _body = body;
            bodyHeight = height;
            bodyLength = length;
            hipSpread = spread;
            Rebuild();
        }

        void Start()
        {
            if (_legs.Count == 0) Rebuild();
            _lastPos = transform.position;
            _bodyY = 0f;
        }

        void Rebuild()
        {
            foreach (var l in _legs)
                if (l.upper != null && l.upper.parent != null) Destroy(l.upper.parent.gameObject);
            _legs.Clear();

            // 길이 1짜리 마디를 하나 만들어 두고, 크기만 바꿔 재사용한다
            if (_segmentMesh == null) _segmentMesh = ProcMesh.Tube(7, 4, 1f, 1f, 0.55f);

            int rows = Mathf.CeilToInt(legCount / 2f);
            float front = bodyLength * 0.32f;
            float back = -bodyLength * 0.34f;

            for (int i = 0; i < legCount; i++)
            {
                int row = i / 2;
                float side = (i % 2 == 0) ? -1f : 1f;
                float z = rows <= 1 ? 0f : Mathf.Lerp(front, back, row / (float)(rows - 1));

                var leg = new Leg();
                leg.side = side;
                // 몸통 안쪽에서 다리가 뻗어 나오게 한다
                leg.hipLocal = new Vector3(side * hipSpread, bodyHeight - 0.10f, z);
                leg.group = (row + (side > 0f ? 1 : 0)) % 2;   // 대각선끼리 함께 움직인다
                leg.foot = transform.TransformPoint(new Vector3(side * (hipSpread + stanceOutward), 0f, z));

                var go = new GameObject("Leg_" + i);
                go.transform.SetParent(transform, false);
                leg.upper = PartShapes.Piece(go.transform, _segmentMesh, legColor, Vector3.zero, Quaternion.identity).transform;
                leg.lower = PartShapes.Piece(go.transform, _segmentMesh, Color.Lerp(legColor, Color.black, 0.15f),
                                             Vector3.zero, Quaternion.identity).transform;

                _legs.Add(leg);
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _velocity = (transform.position - _lastPos) / dt;
            _lastPos = transform.position;

            float speed = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
            float duration = Mathf.Clamp(stepDuration / Mathf.Max(0.5f, speed * 0.35f), 0.06f, 0.30f);
            float ahead = Mathf.Clamp(speed * strideAhead * 0.75f, 0f, legReach * 0.6f);
            float maxStretch = legReach * 0.92f;

            float footSum = 0f;
            int grounded = 0;
            bool anyStepping = false;

            for (int i = 0; i < _legs.Count; i++)
            {
                var leg = _legs[i];
                // 몸통이 위아래로 흔들리면 다리 시작점도 같이 움직여야 한다
                // 다리 시작점은 항상 기체 기준이다.
                // 몸통(위아래로 흔들리는 부분) 기준으로 잡으면
                // 몸통이 올라감 → 발도 올라감 → 몸통이 더 올라감 으로 끝없이 떠오른다.
                Vector3 hip = transform.TransformPoint(leg.hipLocal);
                Vector3 outward = transform.right * (leg.side * stanceOutward);
                Vector3 desired = GroundPoint(hip + outward + _velocity.normalized * ahead + transform.up * 0.2f);

                float stretch = Vector3.Distance(leg.foot, hip);

                if (leg.stepping)
                {
                    // 걷는 도중에도 몸이 계속 움직이므로 착지 지점을 따라 갱신한다
                    leg.stepTo = Vector3.Lerp(leg.stepTo, desired, 1f - Mathf.Exp(-12f * dt));
                    leg.t += dt / duration;
                    float k = Mathf.Clamp01(leg.t);
                    leg.foot = Vector3.Lerp(leg.stepFrom, leg.stepTo, k);
                    leg.foot += transform.up * (Mathf.Sin(k * Mathf.PI) * stepHeight);
                    if (leg.t >= 1f)
                    {
                        leg.stepping = false;
                        leg.foot = leg.stepTo;
                    }
                }
                else
                {
                    bool tooFar = (leg.foot - desired).sqrMagnitude > stepDistance * stepDistance;
                    bool overStretched = stretch > maxStretch;   // 다리가 끝까지 늘어났으면 무조건 내딛는다
                    bool groupFree = _steppingGroup < 0 || _steppingGroup == leg.group;
                    if (overStretched || (tooFar && groupFree))
                    {
                        leg.stepping = true;
                        leg.t = 0f;
                        leg.stepFrom = leg.foot;
                        leg.stepTo = desired + _velocity * duration * 0.5f;   // 착지할 때쯤 몸이 가 있을 위치
                        if (overStretched) _steppingGroup = -1;
                        else _steppingGroup = leg.group;
                    }
                }

                // 그래도 남는 늘어짐은 잘라낸다. 다리가 길게 끌리는 것을 막는다
                Vector3 toFoot = leg.foot - hip;
                if (toFoot.magnitude > legReach)
                    leg.foot = hip + toFoot.normalized * legReach;

                if (leg.stepping) anyStepping = true;
                else { footSum += Vector3.Dot(leg.foot - transform.position, transform.up); grounded++; }

                DrawLeg(leg, hip);
            }

            if (!anyStepping) _steppingGroup = -1;

            // 몸통은 땅에 닿아 있는 발 높이를 따라간다
            if (_body != null)
            {
                float targetY = grounded > 0 ? footSum / grounded : 0f;
                targetY = Mathf.Clamp(targetY, -0.25f, 0.25f);   // 흔들림일 뿐, 몸을 띄우는 값이 아니다
                _bodyY = Mathf.Lerp(_bodyY, targetY, 1f - Mathf.Exp(-bodyFollowSpeed * dt));
                var lp = _body.localPosition;
                lp.y = _bodyY;
                _body.localPosition = lp;
            }
        }

        Vector3 GroundPoint(Vector3 from)
        {
            // 벽이나 천장에 붙으면 캐릭터의 "아래"가 바뀐다. 그 방향으로 바닥을 찾는다.
            Vector3 up = transform.up;
            var ray = new Ray(from + up * 1.2f, -up);
            var hits = Physics.RaycastAll(ray, 4f, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            Vector3 best = from;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;   // 자기 몸은 무시
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    best = hits[i].point;
                }
            }
            return best;
        }

        void DrawLeg(Leg leg, Vector3 hip)
        {
            if (leg.upper == null || leg.lower == null) return;

            Vector3 dir = leg.foot - hip;
            float d = dir.magnitude;
            float seg = Mathf.Max(legReach * 0.5f, d * 0.52f);
            float h = Mathf.Sqrt(Mathf.Max(0.0001f, seg * seg - (d * 0.5f) * (d * 0.5f)));

            Vector3 axis = Vector3.Cross(dir.normalized, transform.right * leg.side);
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.up;
            axis.Normalize();
            if (axis.y < 0f) axis = -axis;   // 곤충처럼 무릎이 위로 꺾이게

            Vector3 knee = hip + dir * 0.5f + axis * h;

            PlaceSegment(leg.upper, hip, knee, legWidth * 1.25f);
            PlaceSegment(leg.lower, knee, leg.foot, legWidth * 0.95f);
        }

        // 길이 1짜리 마디를 두 점 사이에 맞춰 늘인다
        void PlaceSegment(Transform seg, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 delta = to - from;
            float len = delta.magnitude;
            if (len < 0.001f) return;

            seg.position = from;
            seg.rotation = Quaternion.LookRotation(delta / len, transform.up);
            seg.localScale = new Vector3(thickness, thickness, len);
        }
    }
}
