using UnityEngine;
using Bioframe.Assembly;

namespace Bioframe.Combat
{
    // 허수아비. 쓰러지면 잠시 뒤 다시 선다.
    [RequireComponent(typeof(Damageable))]
    public class TrainingDummy : MonoBehaviour
    {
        public float respawnDelay = 3f;
        public bool rotateSlowly;     // 방향별 장갑 차이를 보기 위해 천천히 도는 허수아비

        Damageable _dmg;
        Transform _visual;
        float _deadTimer;

        void Start()
        {
            _dmg = GetComponent<Damageable>();

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            var bodyColor = new Color(0.55f, 0.42f, 0.30f);
            FrameBuilder.MakeBox(_visual, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(1.0f, 1.6f, 0.7f), bodyColor);
            FrameBuilder.MakeBox(_visual, "Head", new Vector3(0f, 2.0f, 0f), new Vector3(0.6f, 0.5f, 0.6f), new Color(0.45f, 0.33f, 0.22f));
            // 정면 표시: 어느 쪽이 앞인지 보이게 한다
            FrameBuilder.MakeBox(_visual, "FrontMark", new Vector3(0f, 1.3f, 0.42f), new Vector3(0.5f, 0.5f, 0.12f), new Color(0.85f, 0.45f, 0.10f));

            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.1f, 0f);
            col.size = new Vector3(1.1f, 2.2f, 0.9f);

            _dmg.onDeath += OnDeath;
        }

        void OnDeath(Damageable d)
        {
            _deadTimer = respawnDelay;
            if (_visual != null) _visual.localRotation = Quaternion.Euler(80f, 0f, 0f);   // 쓰러짐
        }

        void Update()
        {
            if (!_dmg.Alive)
            {
                _deadTimer -= Time.deltaTime;
                if (_deadTimer <= 0f)
                {
                    _dmg.ResetHp();
                    if (_visual != null) _visual.localRotation = Quaternion.identity;
                }
                return;
            }

            if (rotateSlowly) transform.Rotate(Vector3.up, 25f * Time.deltaTime, Space.World);
        }
    }
}
