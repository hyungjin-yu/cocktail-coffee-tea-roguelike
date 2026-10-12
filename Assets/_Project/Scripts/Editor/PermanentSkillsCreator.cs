using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    public static class PermanentSkillsCreator
    {
        public const string Folder = "Assets/_Project/Resources/PermanentSkills";
        [MenuItem("CCT/Create Permanent Skills (24)")]
        public static void Create()
        {
            Directory.CreateDirectory(Folder);
            var fresh = DefaultPermanentSkills.CreateRuntime();
            var baked = new Dictionary<string, PermanentSkillData>();
            foreach (var source in fresh)
            {
                string path = $"{Folder}/PermanentSkill_{source.id}.asset";
                var target = AssetDatabase.LoadAssetAtPath<PermanentSkillData>(path);
                if (target == null)
                {
                    target = ScriptableObject.CreateInstance<PermanentSkillData>();
                    AssetDatabase.CreateAsset(target, path);
                }
                EditorUtility.CopySerialized(source, target);
                target.name = source.id;
                baked.Add(source.id, target);
            }
            foreach (var source in fresh)
            {
                var target = baked[source.id];
                target.prerequisite = source.prerequisite != null ? baked[source.prerequisite.id] : null;
                target.additionalPrerequisites = source.additionalPrerequisites.Select(n => baked[n.id]).ToArray();
                EditorUtility.SetDirty(target);
            }
            AssetDatabase.SaveAssets();
            foreach (var source in fresh) UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.Refresh();
            Debug.Log("[Permanent24] baked 24 nodes, total cost 129; existing asset GUIDs preserved");
        }
        public static void CreateBatch()
        {
            try { ApplyDirectStatusDescriptions(); Create(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
        }

        static void ApplyDirectStatusDescriptions()
        {
            var cards = DefaultCards.CreateRuntime();
            cards.AddRange(DefaultTeaCards.CreateRuntime());
            foreach (var card in cards)
            {
                if (card.id == "molotov" || card.id == "blazer" || card.id == "nettle_tea" || card.id == "nightshade_blend")
                {
                    var baked = AssetDatabase.LoadAssetAtPath<CardData>($"Assets/_Project/Resources/Cards/Card_{card.id}.asset");
                    if (baked != null) { baked.description = card.description; EditorUtility.SetDirty(baked); }
                }
                UnityEngine.Object.DestroyImmediate(card);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
