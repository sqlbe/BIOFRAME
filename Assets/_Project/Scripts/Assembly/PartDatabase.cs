using System.Collections.Generic;
using UnityEngine;
using Bioframe.Rules;

namespace Bioframe.Assembly
{
    // Resources/Data 아래 JSON을 읽어 코어와 파츠를 메모리에 올린다.
    // 밸런스 수정은 JSON만 고치면 되고 코드는 건드리지 않는다.
    public static class PartDatabase
    {
        static readonly Dictionary<string, CoreData> _cores = new Dictionary<string, CoreData>();
        static readonly Dictionary<string, PartData> _parts = new Dictionary<string, PartData>();
        static bool _loaded;

        public static IEnumerable<CoreData> Cores { get { Load(); return _cores.Values; } }
        public static IEnumerable<PartData> Parts { get { Load(); return _parts.Values; } }

        public static void Load(bool force = false)
        {
            if (_loaded && !force) return;
            _cores.Clear();
            _parts.Clear();

            foreach (var ta in Resources.LoadAll<TextAsset>("Data/Cores"))
            {
                var core = JsonUtility.FromJson<CoreData>(ta.text);
                if (core != null && !string.IsNullOrEmpty(core.id)) _cores[core.id] = core;
                else Debug.LogWarning("코어 JSON을 읽지 못했다: " + ta.name);
            }

            foreach (var ta in Resources.LoadAll<TextAsset>("Data/Parts"))
            {
                var part = JsonUtility.FromJson<PartData>(ta.text);
                if (part != null && !string.IsNullOrEmpty(part.id)) _parts[part.id] = part;
                else Debug.LogWarning("파츠 JSON을 읽지 못했다: " + ta.name);
            }

            _loaded = true;
            Debug.Log("[BIOFRAME] 데이터 로드 완료 - 코어 " + _cores.Count + "종, 파츠 " + _parts.Count + "종");
        }

        public static CoreData GetCore(string id)
        {
            Load();
            CoreData c;
            return _cores.TryGetValue(id, out c) ? c : null;
        }

        public static PartData GetPart(string id)
        {
            Load();
            PartData p;
            return _parts.TryGetValue(id, out p) ? p : null;
        }

        public static List<PartData> GetParts(IEnumerable<string> ids)
        {
            var list = new List<PartData>();
            if (ids == null) return list;
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var p = GetPart(id);
                if (p != null) list.Add(p);
                else Debug.LogWarning("[BIOFRAME] 없는 파츠 ID: " + id);
            }
            return list;
        }
    }
}
