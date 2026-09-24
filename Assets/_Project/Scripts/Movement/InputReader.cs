using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bioframe.Movement
{
    // 새 입력 시스템과 옛 입력 시스템 어느 쪽이 켜져 있어도 동작하도록 감싼다.
    // 규격서의 "소켓 = 키" 원칙에 따라, 여기서 읽은 값을 각 부위 코드가 가져다 쓴다.
    public static class InputReader
    {
        public static Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                return new Vector2(x, y);
#else
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            }
        }

        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return m == null ? Vector2.zero : m.delta.ReadValue() * 0.05f;
#else
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
            }
        }

        public static bool Sprint
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.leftShiftKey.isPressed;
#else
                return Input.GetKey(KeyCode.LeftShift);
#endif
            }
        }

        // Ctrl: 벽에서 내려오기
        public static bool DownHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.leftCtrlKey.isPressed;
#else
                return Input.GetKey(KeyCode.LeftControl);
#endif
            }
        }

        // Q: 머리 파츠 사용
        // A: 공격 대상 지정 (누른 뒤 대상을 클릭)
        public static bool AttackKeyPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.aKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.A);
#endif
            }
        }

        public static bool HeadPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.qKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Q);
#endif
            }
        }

        // E: 등 파츠, R: 꼬리 파츠, F: 외피 파츠
        public static bool BackPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.eKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.E);
#endif
            }
        }

        public static bool TailPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.rKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.R);
#endif
            }
        }

        public static bool SkinPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.fKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.F);
#endif
            }
        }

        public static bool JumpPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.spaceKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Space);
#endif
            }
        }

        // 1~3번 키로 이동계 파츠를 바꿔가며 손맛을 비교한다 (M1 전용)
        public static int NumberPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return 0;
                if (k.digit1Key.wasPressedThisFrame) return 1;
                if (k.digit2Key.wasPressedThisFrame) return 2;
                if (k.digit3Key.wasPressedThisFrame) return 3;
                if (k.digit4Key.wasPressedThisFrame) return 4;
                if (k.digit5Key.wasPressedThisFrame) return 5;
                if (k.digit6Key.wasPressedThisFrame) return 6;
                if (k.digit7Key.wasPressedThisFrame) return 7;
                if (k.digit8Key.wasPressedThisFrame) return 8;
                if (k.digit9Key.wasPressedThisFrame) return 9;
                if (k.digit0Key.wasPressedThisFrame) return 10;      // 등 파츠 교체
                if (k.minusKey.wasPressedThisFrame) return 11;       // 꼬리 파츠 교체
                if (k.equalsKey.wasPressedThisFrame) return 12;      // 외피 파츠 교체
                return 0;
#else
                if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
                if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
                if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
                if (Input.GetKeyDown(KeyCode.Alpha4)) return 4;
                if (Input.GetKeyDown(KeyCode.Alpha5)) return 5;
                if (Input.GetKeyDown(KeyCode.Alpha6)) return 6;
                if (Input.GetKeyDown(KeyCode.Alpha7)) return 7;
                if (Input.GetKeyDown(KeyCode.Alpha8)) return 8;
                if (Input.GetKeyDown(KeyCode.Alpha9)) return 9;
                if (Input.GetKeyDown(KeyCode.Alpha0)) return 10;
                if (Input.GetKeyDown(KeyCode.Minus)) return 11;
                if (Input.GetKeyDown(KeyCode.Equals)) return 12;
                return 0;
#endif
            }
        }


        // 오른쪽 버튼을 누르고 있는 동안에만 카메라를 돌린다
        public static bool LookHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return m != null && m.rightButton.isPressed;
#else
                return Input.GetMouseButton(1);
#endif
            }
        }

        public static bool ClickPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return m != null && m.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool ClickHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return m != null && m.leftButton.isPressed;
#else
                return Input.GetMouseButton(0);
#endif
            }
        }

        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return m == null ? Vector2.zero : m.position.ReadValue();
#else
                return Input.mousePosition;
#endif
            }
        }

        public static float Scroll
        {
            get
            {
                // 휠 값의 크기는 환경마다 다르다(1 또는 120). 방향만 쓰고 크기는 카메라가 정한다.
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                float v = m == null ? 0f : m.scroll.ReadValue().y;
#else
                float v = Input.mouseScrollDelta.y;
#endif
                if (v > 0.01f) return 1f;
                if (v < -0.01f) return -1f;
                return 0f;
            }
        }

        // F1: 조작 방식 전환 (3인칭 <-> 쿼터뷰 클릭 이동)
        public static bool ModeTogglePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.f1Key.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.F1);
#endif
            }
        }

        // F5: 시작 위치로 되돌리기 (테스트용)
        public static bool RespawnPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.f5Key.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.F5);
#endif
            }
        }

        // Tab: 조립 화면 열기/닫기
        public static bool AssemblyTogglePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.tabKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Tab);
#endif
            }
        }

        // F2: 자세한 정보 패널 접기/펴기
        public static bool DetailTogglePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.f2Key.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.F2);
#endif
            }
        }

        // Enter: 경기 다시 시작
        public static bool RestartPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
#else
                return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
            }
        }

        public static bool CursorTogglePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.escapeKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }
    }
}
