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

    [Serializable]
    public class PartData
    {
        public string id, name, socket, size, model;
        public float weight;
        public int cost;
        public TagData tags = new TagData();
        public LocomotionData locomotion;   // LC 파츠만 가짐
        public AbilityData ability;         // 공격 파츠만 가짐
        public DrawbackData drawback = new DrawbackData();
        public List<MutationData> mutations = new List<MutationData>();
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
