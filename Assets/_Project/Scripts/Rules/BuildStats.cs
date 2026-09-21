using System.Collections.Generic;

namespace Bioframe.Rules
{
    // 규격서 §5 핵심 계산식. 엔진과 분리해 두고 테스트로 검증한다.
    public struct FrameStats
    {
        public float partsWeight;      // 파츠 무게 합
        public float totalWeight;      // 코어 자체 무게 + 파츠
        public float load;             // 적재 한도
        public float overload;         // 과적률 0~
        public float speed;            // 실효 이동 속도 m/s
        public float sprintSpeed;      // 질주 속도 m/s
        public float sprintUpkeep;     // 질주 유지 EN/초
        public float jumpHeight;       // 도약 높이 m
        public float hp;
        public float enMax, enRegen;
        public ArmorData armor;
        public int legs;
        public bool wallClimb, ceiling;
        public float turnMulWhileSprint;
    }

    public static class BuildStats
    {
        public const float BaseJumpHeight = 1.6f;

        public static FrameStats Compute(CoreData core, IList<PartData> parts)
        {
            var s = new FrameStats();
            s.armor = new ArmorData
            {
                front = core.@base.arm.front,
                side = core.@base.arm.side,
                rear = core.@base.arm.rear,
                top = core.@base.arm.top
            };
            s.hp = core.@base.hp;
            s.enMax = core.@base.en;
            s.enRegen = core.@base.enRegen;
            s.load = core.@base.load;
            s.legs = core.RecommendedLegs();
            s.turnMulWhileSprint = 1f;

            float armMul = 1f;
            float spdMul = 1f, jumpMul = 1f, sprintMul = 1f;

            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null) continue;

                // 크기가 1등급 다르면 무게 +20% (규격서 R6)
                float w = p.weight;
                if (SizeGrade.Of(p.size) != SizeGrade.Of(core.size)) w *= 1.2f;
                s.partsWeight += w;

                if (p.drawback != null)
                {
                    armMul *= p.drawback.armMul;
                    s.turnMulWhileSprint *= p.drawback.turnMulWhileSprint;
                }

                // JsonUtility는 JSON에 없는 객체 항목도 기본값으로 채운다.
                // 그래서 소켓 종류로 한 번 더 거른다. 앞다리 파츠가 다리 수를 덮어쓰면 안 된다.
                if (p.socket == "LC" && p.locomotion != null)
                {
                    s.legs = p.locomotion.legs;
                    spdMul *= p.locomotion.spdMul;
                    jumpMul *= p.locomotion.jumpMul;
                    sprintMul *= p.locomotion.sprintMul;
                    s.sprintUpkeep += p.locomotion.sprintUpkeep;
                    s.wallClimb |= p.locomotion.wallClimb;
                    s.ceiling |= p.locomotion.ceiling;
                }
            }

            s.totalWeight = core.selfWeight + s.partsWeight;
            s.overload = s.load > 0f ? Max(0f, (s.partsWeight - s.load) / s.load) : 0f;

            float loadPenalty = Max(0.4f, 1f - 1.5f * s.overload);
            s.speed = core.@base.spd * spdMul * loadPenalty;
            s.sprintSpeed = s.speed * sprintMul;
            s.jumpHeight = BaseJumpHeight * jumpMul * loadPenalty;

            s.armor.front *= armMul;
            s.armor.side *= armMul;
            s.armor.rear *= armMul;
            s.armor.top *= armMul;

            return s;
        }

        // 받는 피해 = 피해 x 100 / (100 + 장갑)
        public static float DamageAfterArmor(float damage, float armor, float ignoreRatio = 0f)
        {
            float a = armor * (1f - ignoreRatio);
            return damage * 100f / (100f + Max(0f, a));
        }

        static float Max(float a, float b) { return a > b ? a : b; }
    }
}
