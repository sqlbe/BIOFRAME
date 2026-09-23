using System.Collections.Generic;
using UnityEngine;

namespace Bioframe.Assembly
{
    // 조립된 캐릭터의 시각 부분 참조를 들고 있는다.
    // M1에서는 전부 박스지만, 나중에 실사 모델로 교체해도 이 참조 구조는 그대로 쓴다.
    public class FrameVisual : MonoBehaviour
    {
        public Transform body;                  // 몸통 (다리 보행에 따라 위아래로 흔들림)
        public float bodyHeight = 1.1f;         // 지면에서 몸통까지 높이
        public float bodyLength = 1.8f;
        public float hipSpread = 0.55f;
        public readonly Dictionary<string, Transform> sockets = new Dictionary<string, Transform>();
        public readonly Dictionary<string, Transform> attachedParts = new Dictionary<string, Transform>();

        public Transform GetAttachedPart(string node)
        {
            Transform t;
            return attachedParts.TryGetValue(node, out t) ? t : null;
        }

        public Transform GetSocket(string node)
        {
            Transform t;
            return sockets.TryGetValue(node, out t) ? t : null;
        }
    }
}
