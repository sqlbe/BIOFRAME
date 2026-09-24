using System;
using System.Collections.Generic;

namespace Bioframe.Rules
{
    // 규격서 §9 데이터 형식에 대응하는 순수 데이터 클래스.
    // Unity에 의존하지 않도록 [Serializable] 외에는 엔진 타입을 쓰지 않는다.

    [Serializable]
    public class ArmorData
    {
        public float front, side, rear, top;

        public float Get(string dir)
        {
            switch (dir)
            {
                case "side": return side;
                case "rear": return rear;
                case "top": return top;
                default: return front;
            }
        }
    }

    [Serializable]
    public class CoreBase
    {
        public float hp = 1000f;
        public float load = 120f;
        public float en = 100f;
        public float enRegen = 8f;
        public float spd = 7f;
        public float agi = 1f;
        public ArmorData arm = new ArmorData();
    }

    [Serializable]
    public class SocketDef
    {
        public string node, type, mirror;
        public bool optional, required;
        public int recommendLegs;
    }

    [Serializable]
    public class CoreData
    {
        public string id, name, size;
        public string color;
        public float selfWeight = 60f;
        public CoreBase @base = new CoreBase();
        public float thrustFactor = 1f;
        public List<SocketDef> sockets = new List<SocketDef>();

        public int RecommendedLegs()
        {
            foreach (var s in sockets)
                if (s.type == "LC") return s.recommendLegs;
            return 4;
        }
    }

    [Serializable]
    public class TagData
    {
        public string motif;
        public List<string> trait = new List<string>();

        public bool Has(string t) { return trait != null && trait.Contains(t); }
    }

    [Serializable]
    public class LocomotionData
    {
        public int legs = 4;
        public float spdMul = 1f;
        public float jumpMul = 1f;
        public float sprintMul = 1f;
        public float sprintUpkeep = 0f;
        public float leapCost = 0f;
        public bool wallClimb, ceiling;
    }

    [Serializable]
    public class BlockData { public float front; }

    [Serializable]
    public class AbilityData
    {
        public string action, type;
        public float damage, cooldown, range, en, grabSeconds;
        public float armorIgnore;            // 0~1. 갯가재 곤봉처럼 장갑을 무시하는 파츠
        public float rootSeconds;            // 대상을 묶어두는 시간
        public float selfRootSeconds;        // 쓰고 나서 내가 못 움직이는 시간
        public float dotDps, dotDuration;    // 지속 피해
        public float armorShred, armorShredDuration;   // 상대 장갑 깎기
        public bool requiresSprint;          // 질주 중에만 사용 가능(치타 송곳니)
        public float projectileSpeed;        // 0보다 크면 날아가는 발사체로 처리한다
        public int projectileCount = 1;      // 드론 포드처럼 여러 발 나가는 경우
        public float coneAngle;              // 분사 각도(도). 범위 공격에 쓴다
        public float upkeep;                 // 켜 두는 동안 초당 EN (카멜레온 투명)
        public int combo = 1;
        public BlockData block;
    }

    [Serializable]
    public class DrawbackData
    {
        // 값이 없는 항목은 "영향 없음"이 기본값이 되도록 초기화한다.
        public float knockbackResist = 0f;
        public float armMul = 1f;
        public float turnMulWhileSprint = 1f;
        public float attackSpeedMul = 1f;
        public bool canBlock = true;
    }

    [Serializable]
    public class MutationData { public string up, down; }

    // 쓰는 능력이 없는 파츠(갑각, 판갑 등)가 주는 상시 효과
    [Serializable]
    public class PassiveData
    {
        public float armFront, armSide, armRear, armTop;
        public float spdMul = 1f;
        public float jumpMul = 1f;
        public float hpBonus;
        public float enRegenBonus;
        public float knockbackResist;
        public bool survivesLethal;      // 도마뱀 자절 꼬리
    }

    [Serializable]
    public class PartData
    {
        public string id, name, socket, size, model;
        public string color;        // "#RRGGBB". 조립 화면과 실제 모델 색에 쓴다
        public string shape;        // 절차적 형태 이름. 실사 모델이 들어오면 model 경로가 대신한다
        public float weight;
        public int cost;
        public TagData tags = new TagData();
        public LocomotionData locomotion;   // LC 파츠만 가짐
        public AbilityData ability;         // 공격 파츠만 가짐
        public DrawbackData drawback = new DrawbackData();
        public PassiveData passive;
        public List<MutationData> mutations = new List<MutationData>();
    }

    // "#RRGGBB" 문자열을 0~1 값 세 개로 바꾼다. 엔진에 의존하지 않도록 직접 변환한다.
    public static class HexColor
    {
        public static bool TryParse(string hex, out float r, out float g, out float b)
        {
            r = g = b = 0f;
            if (string.IsNullOrEmpty(hex)) return false;
            string h = hex.Trim();
            if (h.StartsWith("#")) h = h.Substring(1);
            if (h.Length != 6) return false;

            int v;
            if (!int.TryParse(h, System.Globalization.NumberStyles.HexNumber,
                              System.Globalization.CultureInfo.InvariantCulture, out v)) return false;

            r = ((v >> 16) & 0xFF) / 255f;
            g = ((v >> 8) & 0xFF) / 255f;
            b = (v & 0xFF) / 255f;
            return true;
        }
    }

    public static class SizeGrade
    {
        public static int Of(string size)
        {
            switch (size)
            {
                case "S": return 0;
                case "M": return 1;
                case "L": return 2;
                case "XL": return 3;
                default: return 1;
            }
        }
    }
}
