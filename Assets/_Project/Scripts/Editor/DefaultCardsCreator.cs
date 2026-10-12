using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>
    /// 기본 카드/스킬 목록을 Resources 아래 ScriptableObject 에셋으로 저장.
    /// 이미 있는 에셋은 건드리지 않는다 (인스펙터에서 튜닝한 값 보존).
    /// </summary>
    public static class DefaultCardsCreator
    {
        const string CardFolder = "Assets/_Project/Resources/Cards";
        const string SkillFolder = "Assets/_Project/Resources/Skills";

        [MenuItem("CCT/Create Default Card Assets")]
        public static void CreateAll()
        {
            int cards = CreateCards();
            int skills = CreateSkills();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CCT] 기본 에셋 생성 — 카드 {cards}개 ({CardFolder}), 스킬 {skills}개 ({SkillFolder})");
        }

        static int CreateCards()
        {
            Directory.CreateDirectory(CardFolder);

            var all = DefaultCards.CreateRuntime();
            all.AddRange(DefaultTeaCards.CreateRuntime());
            all.AddRange(DefaultHybridCards.CreateRuntime());

            int created = 0;
            foreach (var card in all)
            {
                string path = $"{CardFolder}/Card_{card.id}.asset";
                if (File.Exists(path))
                {
                    Object.DestroyImmediate(card);
                    continue;
                }
                AssetDatabase.CreateAsset(card, path);
                created++;
            }
            return created;
        }

        static int CreateSkills()
        {
            Directory.CreateDirectory(SkillFolder);

            // 선행 노드 참조를 메모리 인스턴스가 아닌 저장된 에셋으로 이어붙이기 위한 매핑
            var persisted = new Dictionary<SkillData, SkillData>();
            var fresh = DefaultSkills.CreateRuntime();

            int created = 0;
            foreach (var skill in fresh)
            {
                string path = $"{SkillFolder}/Skill_{skill.id}.asset";
                if (File.Exists(path))
                {
                    persisted[skill] = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                    continue;
                }

                if (skill.prerequisite != null && persisted.TryGetValue(skill.prerequisite, out var prereq))
                    skill.prerequisite = prereq;

                AssetDatabase.CreateAsset(skill, path);
                persisted[skill] = skill;
                created++;
            }

            foreach (var skill in fresh)
                if (!AssetDatabase.Contains(skill)) Object.DestroyImmediate(skill);

            return created;
        }
    }
}
