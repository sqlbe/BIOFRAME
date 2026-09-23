using UnityEngine;

namespace Bioframe.Movement
{
    public enum CameraMode
    {
        Follow,     // 3인칭. 오른쪽 버튼을 누른 동안만 회전한다
        TopDown     // 쿼터뷰. 각도 고정, 클릭 이동용
    }

    // 캐릭터를 따라다니는 카메라. 두 방식을 F1으로 오간다.
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public CameraMode mode = CameraMode.TopDown;

        [Header("3인칭")]
        public float distance = 6.5f;
        public float height = 1.6f;
        public float sensitivity = 3.0f;
        public float minPitch = -25f;
        public float maxPitch = 65f;

        [Header("쿼터뷰")]
        public float topDownDistance = 14f;
        public float topDownPitch = 52f;
        public float pitchStep = 6f;           // Ctrl + 휠 한 칸에 바뀌는 각도

        [Header("공통")]
        public float followSpeed = 14f;
        public float minDistance = 3f;
        public float maxDistance = 22f;
        public float zoomStep = 1.5f;          // 휠 한 칸에 움직이는 거리
        public float topDownZoomStep = 2.5f;
        public float zoomSmooth = 12f;

        float _yaw, _pitch = 52f;
        float _shakeAmp, _shakeTime, _shakeDuration;
        float _dist, _topDist;
        bool _distInit;

        public float Yaw { get { return _yaw; } }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (target != null) _yaw = target.eulerAngles.y;
            _dist = distance;
            _topDist = topDownDistance;
            _distInit = true;
        }

        // 큰 타격이나 피격 때 화면을 짧게 흔든다
        public void Shake(float amplitude, float duration)
        {
            if (amplitude <= _shakeAmp && _shakeTime > 0f) return;
            _shakeAmp = amplitude;
            _shakeDuration = duration;
            _shakeTime = duration;
        }

        public void SetMode(CameraMode m)
        {
            mode = m;
            if (mode == CameraMode.TopDown) _pitch = topDownPitch;
        }

        void LateUpdate()
        {
            if (target == null) return;

            float dt = Time.deltaTime;

            // 마우스 오른쪽 버튼을 누르는 동안에만 시점을 돌린다. 평소에는 커서가 자유롭다.
            if (InputReader.LookHeld)
            {
                Vector2 look = InputReader.Look * sensitivity;
                _yaw += look.x;
                if (mode == CameraMode.Follow)
                    _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (!_distInit) { _dist = distance; _topDist = topDownDistance; _distInit = true; }

            float scroll = InputReader.Scroll;
            if (scroll != 0f)
            {
                if (InputReader.DownHeld)
                {
                    // Ctrl + 휠: 내려다보는 각도를 바꾼다. 수평(낮게) ~ 수직(위에서 내려다보기)
                    if (mode == CameraMode.Follow) _pitch = Mathf.Clamp(_pitch + scroll * pitchStep, minPitch, maxPitch);
                    else topDownPitch = Mathf.Clamp(topDownPitch + scroll * pitchStep, 8f, 88f);
                }
                else if (mode == CameraMode.Follow)
                    distance = Mathf.Clamp(distance - scroll * zoomStep, minDistance, maxDistance);
                else
                    topDownDistance = Mathf.Clamp(topDownDistance - scroll * topDownZoomStep, minDistance + 4f, maxDistance + 10f);
            }

            // 목표 거리로 부드럽게 따라간다
            float k = 1f - Mathf.Exp(-zoomSmooth * dt);
            _dist = Mathf.Lerp(_dist, distance, k);
            _topDist = Mathf.Lerp(_topDist, topDownDistance, k);

            float pitch = mode == CameraMode.Follow ? _pitch : topDownPitch;
            float dist = mode == CameraMode.Follow ? _dist : _topDist;

            Quaternion rot = Quaternion.Euler(pitch, _yaw, 0f);
            Vector3 focus = target.position + Vector3.up * height;
            Vector3 wanted = focus - rot * Vector3.forward * dist;

            // 지형에 묻히지 않게 당긴다
            RaycastHit hit;
            if (Physics.Linecast(focus, wanted, out hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(target))
                wanted = hit.point + hit.normal * 0.25f;

            transform.position = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-followSpeed * dt));
            transform.rotation = Quaternion.LookRotation(focus - transform.position);

            if (_shakeTime > 0f)
            {
                _shakeTime -= dt;
                float shakeK = Mathf.Clamp01(_shakeTime / Mathf.Max(0.01f, _shakeDuration));
                transform.position += (transform.right * Random.Range(-1f, 1f) + transform.up * Random.Range(-1f, 1f))
                                      * (_shakeAmp * shakeK);
                if (_shakeTime <= 0f) _shakeAmp = 0f;
            }
        }
    }
}
