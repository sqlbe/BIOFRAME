using System.Collections.Generic;

namespace Bioframe.Rules
{
    public enum IssueLevel { Blocked, Penalty, Warning }

    public struct AssemblyIssue
    {
        public IssueLevel level;
        public string code;     // R1, R3 ...
        public string message;

        public override string ToString() { return code + " " + message; }
    }

    // 규격서 §6 조립 규칙. M1에서는 R1~R4, R6, R9만 구현한다.
    public static class AssemblyRules
    {
        public const float OverloadHardLimit = 1.3f;

        public static List<AssemblyIssue> Validate(CoreData core, IList<PartData> parts)
        {
            var issues = new List<AssemblyIssue>();
            if (core == null)
            {
                issues.Add(Make(IssueLevel.Blocked, "R0", "코어가 없다"));
                return issues;
            }

            bool hasLocomotion = false;
            int coreGrade = SizeGrade.Of(core.size);

            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null) continue;

                if (!HasSocket(core, p.socket))
                    issues.Add(Make(IssueLevel.Blocked, "R2", p.name + ": 이 코어에 " + p.socket + " 소켓이 없다"));

                int diff = Abs(SizeGrade.Of(p.size) - coreGrade);
                if (diff >= 2)
                    issues.Add(Make(IssueLevel.Blocked, "R3", p.name + ": 크기 " + p.size + " 는 " + core.size + " 코어에 2등급 이상 차이"));
                else if (diff == 1)
                    issues.Add(Make(IssueLevel.Penalty, "R6", p.name + ": 크기 1등급 차이로 무게 +20%"));

                if (p.socket == "LC") hasLocomotion = true;
            }

            if (!hasLocomotion)
                issues.Add(Make(IssueLevel.Blocked, "R1", "이동계 소켓이 비어 있다"));

            var stats = BuildStats.Compute(core, parts);
            if (stats.partsWeight > stats.load * OverloadHardLimit)
                issues.Add(Make(IssueLevel.Blocked, "R4", "파츠 무게 " + stats.partsWeight.ToString("0") + "kg 가 적재 한도 " + stats.load.ToString("0") + "kg 의 130% 초과"));
            else if (stats.overload > 0f)
                issues.Add(Make(IssueLevel.Penalty, "R7", "과적 " + (stats.overload * 100f).ToString("0") + "% 로 속도 감소"));

            if (stats.legs != core.RecommendedLegs())
                issues.Add(Make(IssueLevel.Penalty, "R9", "권장 다리 수(" + core.RecommendedLegs() + ")와 달라 안정성 -10%"));

            return issues;
        }

        public static bool IsBuildable(List<AssemblyIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].level == IssueLevel.Blocked) return false;
            return true;
        }

        static bool HasSocket(CoreData core, string type)
        {
            for (int i = 0; i < core.sockets.Count; i++)
                if (core.sockets[i].type == type) return true;
            return false;
        }

        static AssemblyIssue Make(IssueLevel level, string code, string msg)
        {
            var i = new AssemblyIssue();
            i.level = level; i.code = code; i.message = msg;
            return i;
        }

        static int Abs(int v) { return v < 0 ? -v : v; }
    }
}
