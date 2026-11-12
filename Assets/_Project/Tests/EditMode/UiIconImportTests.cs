using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-12 UI 아이콘 묶음(ArtSource/ui-icons-2026-11-12) 임포트 — 매니페스트 기준:
    /// Unity에 들어온 새 그림은 모두 매니페스트 status=ready이고 원본 PNG와 바이트 동일, 64×64·Sprite·Single·PPU 64·Point·밉맵 없음·무압축.
    /// 카드는 educationArtId로 자기 그림을 쓰고(공용 카테고리 그림 아님), 연구 노드는 바텐더 = 공용·티 소믈리에 = _tea 우선.</summary>
    public class UiIconImportTests
    {
        const string Art = "ArtSource/ui-icons-2026-11-12";
        [Serializable] class Item { public string id, group, artId, status, sha256; }
        [Serializable] class Manifest { public Item[] items; }

        static Item[] Items() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Art, "manifest.json"))).items;

        static string Sha(string path)
        {
            using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        static string Target(Item i) => i.group == "card" ? $"Assets/_Project/Resources/CardEducation/{i.artId}.png" : $"Assets/_Project/Resources/ResearchIcons/{i.artId}.png";

        /// <summary>Unity로 가져온 새 묶음 파일(카드 = 원래 2026-10-16 원본이 아닌 것, ResearchIcons 전부).</summary>
        static string[] ImportedNewFiles()
        {
            var cards = Directory.GetFiles("Assets/_Project/Resources/CardEducation", "*.png")
                .Where(p => !File.Exists("ArtSource/card-education-2026-10-16/" + Path.GetFileName(p)));
            var research = Directory.Exists("Assets/_Project/Resources/ResearchIcons") ? Directory.GetFiles("Assets/_Project/Resources/ResearchIcons", "*.png") : new string[0];
            return cards.Concat(research).Select(p => p.Replace('\\', '/')).ToArray();
        }

        [Test]
        public void EveryImportedNewIcon_IsReadyInManifest_ByteIdentical_PixelArtSettings()
        {
            var items = Items();
            var files = ImportedNewFiles();
            Assert.Greater(files.Length, 0);
            foreach (var path in files)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var it = items.FirstOrDefault(i => i.artId == name);
                Assert.IsNotNull(it, name + " 매니페스트에 없음");
                Assert.AreEqual("ready", it.status, name + " ready 아닌 그림을 임포트함");
                Assert.AreEqual(it.sha256, Sha(path), name + " 원본과 다름");
                Assert.AreEqual(it.sha256, Sha(Path.Combine(Art, name + ".png")), name + " 원본 해시가 매니페스트와 다름");
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.Sprite, imp.textureType, path);
                Assert.AreEqual(SpriteImportMode.Single, imp.spriteImportMode, path);
                Assert.AreEqual(64f, imp.spritePixelsPerUnit, path);
                Assert.AreEqual(FilterMode.Point, imp.filterMode, path);
                Assert.IsFalse(imp.mipmapEnabled, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression, path);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.AreEqual(64, tex.width, path); Assert.AreEqual(64, tex.height, path);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(path), path + " sprite");
            }
        }

        [Test]
        public void ResearchAndMasteryIcons_AreCroppedToOpaqueBounds_SameTexture()
        {
            ResearchIcons.ClearCacheForTests();
            var full = Resources.Load<Sprite>("ResearchIcons/research_espresso_shot");
            var shown = ResearchIcons.Research("espresso_shot", Job.Bartender);
            Assert.AreSame(full.texture, shown.texture, "같은 텍스처(원본 그림 불변)");
            Assert.Less(shown.rect.height, full.rect.height, "가로로 긴 그림은 투명 위아래 여백을 잘라 칸을 채운다");
            Assert.AreEqual("research_espresso_shot", shown.name);
            foreach (var t in DrinkResearch.Traits)
            {
                var m = ResearchIcons.Mastery(t.id);
                if (m != null) Assert.LessOrEqual(m.rect.width * m.rect.height, 64f * 64f, t.id);
            }
        }

        [Test]
        public void NotReadyItems_AreNotImported()
        {
            foreach (var it in Items().Where(i => i.status != "ready"))
                Assert.IsFalse(File.Exists(Target(it)), it.artId + " (" + it.status + ") 임포트 금지");
        }

        [Test]
        public void ImportedCards_UseOwnArt_NotCategoryFallback()
        {
            int n = 0;
            foreach (var it in Items().Where(i => i.group == "card" && File.Exists(Target(i))))
            {
                var card = Resources.Load<CardData>("Cards/Card_" + it.id);
                Assert.IsNotNull(card, it.id);
                Assert.AreEqual(it.artId, card.educationArtId, it.id);
                var s = CardEducationIcons.For(card);
                Assert.AreEqual(it.artId, s.name, it.id);
                Assert.AreNotSame(CardSelectView.CategoryIcon(card.category), s, it.id);
                n++;
            }
            Assert.Greater(n, 0);
        }

        [Test]
        public void ResearchIcons_TeaVariantPreferred_ForTeaSommelier_SharedForBartender()
        {
            ResearchIcons.ClearCacheForTests();
            foreach (var it in Items().Where(i => i.group == "research" && File.Exists(Target(i))))
            {
                Assert.AreEqual(it.artId, ResearchIcons.Research(it.id, Job.Bartender).name, it.id);
                bool tea = File.Exists($"Assets/_Project/Resources/ResearchIcons/{it.artId}_tea.png");
                bool teaPlanned = ResearchIcons.TeaVariantRequired(it.id); // 인계표 7종 ∪ 매니페스트 research_tea
                if (Items().Any(i => i.group == "research_tea" && i.id == it.id)) Assert.IsTrue(teaPlanned, it.id + " 매니페스트 차 변형은 계획 목록에 포함");
                var teaSprite = ResearchIcons.Research(it.id, Job.TeaSommelier);
                if (tea) Assert.AreEqual(it.artId + "_tea", teaSprite.name, it.id);
                else if (teaPlanned) Assert.IsNull(teaSprite, it.id + ": 차 변형이 계획됐는데 아직 없으면 커피 그림을 티 소믈리에에게 보이지 않는다");
                else Assert.AreEqual(it.artId, teaSprite.name, it.id + " 공용");
            }
            foreach (var node in SkillDatabase.LoadAll())
                if (!File.Exists($"Assets/_Project/Resources/ResearchIcons/research_{node.id}.png"))
                    Assert.IsNull(ResearchIcons.Research(node.id, Job.Bartender), node.id + " 그림 없으면 null(기존 표시)");
        }
    }
}
