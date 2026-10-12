using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>합성 ArtCatalog로 런타임 아트 소비 경로(04 §9)를 검사한다 — 실제 이미지 품질·실제 렌더는 검사하지 않는다.</summary>
    public abstract class SyntheticArtFixture
    {
        protected readonly List<Object> created = new List<Object>();
        protected ArtCatalog catalog;

        [SetUp]
        public void CatalogSetUp()
        {
            catalog = ScriptableObject.CreateInstance<ArtCatalog>();
            created.Add(catalog);
            ArtLibrary.SetCatalogForTests(catalog);
        }

        [TearDown]
        public void CatalogTearDown()
        {
            ArtLibrary.SetCatalogForTests(null);
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Time.timeScale = 1f;
        }

        protected Sprite MakeSprite(int w, int h, Vector2 pivot, Vector4 border = default)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            created.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 128f, 0, SpriteMeshType.FullRect, border);
            created.Add(s);
            return s;
        }

        protected ArtClip Clip(string name, int frames, float fps, bool loop, int w = 256, int h = 256, Vector2? pivot = null)
        {
            var c = new ArtClip { name = name, fps = fps, loop = loop, frames = new Sprite[frames] };
            for (int i = 0; i < frames; i++) c.frames[i] = MakeSprite(w, h, pivot ?? new Vector2(0.5f, 0.125f));
            return c;
        }

        protected ArtAsset AddBody(string id, float targetH, float bodyPx, params ArtClip[] clips)
        {
            var a = new ArtAsset
            {
                assetId = id, kind = "body", status = "ready", frameSizePx = new Vector2(256, 256),
                pivotNormalized = new Vector2(0.5f, 0.125f), targetBodyHeightWorld = targetH, bodyHeightPx = bodyPx, clips = clips,
            };
            Add(a);
            return a;
        }

        protected ArtAsset AddFlat(string id, int w, int h, Rect content, ArtClip clip, float opacity = 1f)
        {
            var a = new ArtAsset
            {
                assetId = id, kind = "vfx", status = "ready", frameSizePx = new Vector2(w, h),
                pivotNormalized = new Vector2(0.5f, 0.5f), contentRectNormalized = content, runtimeOpacity = opacity,
                clips = new[] { clip },
            };
            Add(a);
            return a;
        }

        protected void Add(ArtAsset a)
        {
            catalog.assets = catalog.assets.Concat(new[] { a }).ToArray();
            ArtLibrary.SetCatalogForTests(catalog);
        }
    }

    public class ArtRuntimeConsumerTests : SyntheticArtFixture
    {
        [Test]
        public void ArtClip_FrameTiming_LoopsOrHoldsLastFrame()
        {
            var loop = Clip("move", 6, 10f, true);
            Assert.AreSame(loop.frames[0], loop.FrameAt(0.05f));
            Assert.AreSame(loop.frames[3], loop.FrameAt(0.35f));
            Assert.AreSame(loop.frames[0], loop.FrameAt(0.61f), "반복 클립은 0.6초 후 처음으로");
            var once = Clip("defeat", 4, 10f, false);
            Assert.AreSame(once.frames[3], once.FrameAt(5f), "1회 클립은 마지막 프레임 유지");
            Assert.AreEqual(0.4f, once.Duration, 1e-4f);
        }

        [Test]
        public void MissingArt_ReturnsNull_SoCallersKeepPlaceholders()
        {
            Assert.IsNull(ArtLibrary.Get("nope"));
            Assert.IsNull(ArtVfx.Spawn("nope", Vector2.zero, 0f, 1f, 0.1f, Color.white));
            Assert.IsNull(GroundShadow.Attach(new GameObject("x").transform, Vector2.zero, 1f));
        }

        [Test]
        public void PlayerVisual_WithBodyArt_UsesFootPivotScaleFlipOnVisualOnly_IdleMoveAndHandAnchor()
        {
            var idle = Clip("idle", 4, 3f, true);
            var move = Clip("move", 6, 10f, true);
            var body = AddBody("bartender", 1.6f, 186f, idle, move);
            body.anchors = new[] { new ArtAnchor { clip = "idle", frame = 0, name = "hand", px = new Vector2(164, 87) } };
            AddFlat("ground_shadow", 256, 128, new Rect(0.1f, 0.3f, 0.8f, 0.4f), Clip("effect", 1, 0f, false, 256, 128, new Vector2(0.5f, 0.5f)), 0.22f);

            var go = new GameObject("Player", typeof(Rigidbody2D));
            created.Add(go);
            var player = go.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.cyan, "bartender");
            var v = player.Visual;

            Assert.IsTrue(v.UsesBodyArt);
            Assert.AreEqual(Vector3.one, go.transform.localScale, "물리 루트는 스케일 1");
            Assert.AreEqual(PlayerController.Radius, go.GetComponent<CircleCollider2D>().radius, 1e-4f);
            Assert.AreEqual(1.6f / (186f / 128f), v.transform.localScale.y, 1e-4f, "04 §9 visualScale");
            Assert.AreEqual(-PlayerController.Radius, v.transform.localPosition.y, 1e-4f, "발 피벗을 판정 원 바닥에");
            Assert.AreSame(idle, v.GetComponent<SpriteAnimator>().Current);

            v.Tick(-1f, true, false);
            Assert.Less(v.transform.localScale.x, 0f, "왼쪽은 Visual만 반전");
            Assert.AreEqual(Vector3.one, go.transform.localScale);
            Assert.AreSame(move, v.GetComponent<SpriteAnimator>().Current);
            v.Tick(1f, false, false);
            Assert.AreSame(idle, v.GetComponent<SpriteAnimator>().Current);

            Assert.IsNotNull(v.Shadow);
            Assert.AreEqual(0.22f, v.Shadow.color.a, 1e-4f);
            Assert.AreEqual(SortOrders.Shadow, v.Shadow.sortingOrder);
            Assert.AreEqual(PlayerController.Radius * 2.4f, v.Shadow.transform.localScale.x * 0.8f * 2f, 1e-3f, "그림자 알파 폭");

            v.AttachWeapon(MakeSprite(512, 512, new Vector2(0.5f, 0.5f)), 0.8f, false);
            Assert.IsFalse(v.WeaponUsesProvisionalAnchor, "manifest hand anchor 사용");
            var expected = body.AnchorToLocal(new Vector2(164, 87));
            Assert.AreEqual(expected.x, v.Weapon.transform.parent.localPosition.x, 1e-4f);
        }

        [Test]
        public void PlayerVisual_NoBodyArt_FallsBackToCapsule_AndActionClipsAreOptional()
        {
            var go = new GameObject("Player", typeof(Rigidbody2D));
            created.Add(go);
            var player = go.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.green, "tea_sommelier");
            Assert.IsFalse(player.Visual.UsesBodyArt);
            Assert.AreSame(SpriteFactory.Capsule, player.Visual.Body.sprite);
            Assert.AreEqual(Color.green, player.Visual.Body.color);
            Assert.IsFalse(player.Visual.PlayAction("melee"));
            Assert.IsFalse(player.Visual.PlayDefeat());
        }

        [Test]
        public void PlayerVisual_ActionHurtDefeat_PlayWhenDelivered()
        {
            var idle = Clip("idle", 4, 3f, true);
            var melee = Clip("melee", 4, 12f, false);
            var hurt = Clip("hurt", 1, 0f, false);
            var defeat = Clip("defeat", 4, 10f, false);
            AddBody("bartender", 1.6f, 186f, idle, Clip("move", 6, 10f, true), melee, hurt, defeat);
            var go = new GameObject("Player", typeof(Rigidbody2D));
            created.Add(go);
            var player = go.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white, "bartender");
            var anim = player.Visual.GetComponent<SpriteAnimator>();

            Assert.IsTrue(player.Visual.PlayAction("melee"));
            Assert.AreSame(melee, anim.Current);
            player.Visual.Tick(1f, false, false);
            Assert.AreSame(melee, anim.Current, "행동 클립 동안 대기 클립으로 덮지 않는다");
            player.TakeDamage(1f);
            Assert.AreSame(hurt, anim.Current, "피격 hurt");
            player.TakeZoneDamage(9999f); // 단발 무적에 막히지 않는 장판 피해로 사망
            Assert.AreSame(defeat, anim.Current, "사망 defeat");
        }

        [Test]
        public void Enemy_WithBodyArt_UsesH_OverBodyHeight_NoIconScale_HurtAndActionClips()
        {
            var idle = Clip("idle", 4, 6f, true);
            var hurt = Clip("hurt", 1, 0f, false);
            var action = Clip("action", 4, 12f, false);
            AddBody("lime_wedge", 0.9f, 184f, idle, Clip("move", 6, 10f, true), hurt, action);
            var parent = new GameObject("Parent").transform;
            created.Add(parent.gameObject);

            var enemy = Enemy.SpawnAt(Vector2.zero, EnemyDef.Imp, 1f, null, parent);
            var sr = enemy.GetComponentInChildren<SpriteRenderer>();
            Assert.IsTrue(enemy.UsesBodyArt);
            Assert.AreEqual(0.9f / (184f / 128f), sr.transform.localScale.y, 1e-4f, "EnemyIconVisualScale을 곱하지 않는다");
            Assert.AreEqual(-EnemyDef.Imp.radius, sr.transform.localPosition.y, 1e-4f);
            Assert.AreEqual(EnemyDef.Imp.radius, enemy.GetComponent<CircleCollider2D>().radius, 1e-4f, "판정 반지름 보존");

            enemy.TakeDamage(1f);
            Assert.AreSame(hurt, enemy.Animator.Current);
            Assert.IsTrue(enemy.PlayAction("action"));
            Assert.AreSame(action, enemy.Animator.Current);
            Assert.IsFalse(enemy.PlayAction("charge_prepare"), "없는 클립은 false");
        }

        [Test]
        public void PlayerVisual_SyncToCurrentFrame_WeaponFollowsBodyFrameChangedAfterTick_AndFacingSnap()
        {
            var idle = Clip("idle", 2, 10f, true);
            var body = AddBody("bartender", 1.6f, 186f, idle, Clip("move", 6, 10f, true));
            body.anchors = new[]
            {
                new ArtAnchor { clip = "idle", frame = 0, name = "hand", px = new Vector2(100, 100) },
                new ArtAnchor { clip = "idle", frame = 1, name = "hand", px = new Vector2(160, 120) },
            };
            var go = new GameObject("Player", typeof(Rigidbody2D));
            created.Add(go);
            var player = go.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white, "bartender");
            var v = player.Visual;
            v.AttachWeapon(MakeSprite(32, 32, new Vector2(0.5f, 0.5f)), 0.8f, false);
            var pivot = v.Weapon.transform.parent;
            v.Tick(1f, false, false);
            Assert.AreEqual((Vector3)body.AnchorToLocal(new Vector2(100, 100)), pivot.localPosition, "프레임0 손");

            // Tick 뒤 같은 프레임에 몸체 SpriteAnimator가 다음 프레임으로 넘어간 상황
            v.GetComponent<SpriteAnimator>().Advance(0.11f);
            Assert.AreEqual(1, v.GetComponent<SpriteAnimator>().FrameIndex);
            v.SyncToCurrentFrame();
            Assert.AreEqual((Vector3)body.AnchorToLocal(new Vector2(160, 120)), pivot.localPosition, "LateUpdate 동기화 뒤 무기가 현재 몸체 프레임 손을 따른다");

            // 무기가 Tick 뒤에 SetFacing으로 방향을 바꾼 상황 — 같은 프레임 안에 Visual 반전
            player.SetFacing(go.transform.position.x - 5f);
            v.SyncToCurrentFrame();
            Assert.Less(v.transform.localScale.x, 0f, "공격 방향 전환이 한 프레임 늦지 않는다");
        }

        [Test]
        public void PlayerDefeat_PlaysOnUnscaledTime_SoItIsNotFrozenByRunEnd()
        {
            var defeat = Clip("defeat", 4, 10f, false);
            AddBody("bartender", 1.6f, 186f, Clip("idle", 4, 3f, true), Clip("hurt", 1, 0f, false), defeat);
            var go = new GameObject("Player", typeof(Rigidbody2D));
            created.Add(go);
            var player = go.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white, "bartender");
            player.TakeZoneDamage(9999f);
            var anim = player.Visual.GetComponent<SpriteAnimator>();
            Assert.AreSame(defeat, anim.Current);
            Assert.IsTrue(anim.UseUnscaledTime, "결과 화면(timeScale 0) 뒤에서도 defeat가 끝까지 재생된다");
        }

        [Test]
        public void Enemy_HurtDuringActionClip_KeepsActionClip_ThenHurtAfterActionEnds()
        {
            var idle = Clip("idle", 4, 6f, true);
            var hurt = Clip("hurt", 1, 0f, false);
            var prepare = Clip("charge_prepare", 4, 8f, true);
            AddBody("grinder_golem", 2.4f, 200f, idle, Clip("move", 6, 10f, true), hurt, prepare);
            var parent = new GameObject("Parent").transform;
            created.Add(parent.gameObject);
            var boss = Enemy.SpawnAt(Vector2.zero, EnemyDef.GrinderGolem, 1f, null, parent);
            boss.Invulnerable = false;

            Assert.IsTrue(boss.PlayAction("charge_prepare", 0.8f));
            boss.TakeDamage(1f);
            Assert.AreSame(prepare, boss.Animator.Current, "예고 클립 중 피격이 hurt로 덮으면 예고가 사라진다");

            var plain = Enemy.SpawnAt(new Vector2(3f, 0f), EnemyDef.GrinderGolem, 1f, null, parent);
            plain.Invulnerable = false;
            plain.TakeDamage(1f);
            Assert.AreSame(hurt, plain.Animator.Current, "행동 중이 아니면 기존대로 hurt");
        }

        [Test]
        public void Enemy_ActionAnchors_MapThroughFootPivotScaleAndFlip_RootColliderUnchanged()
        {
            var action = Clip("action", 4, 12f, false);
            var kettle = AddBody("kettle_cannon", 1.3f, 166f, Clip("idle", 4, 6f, true), action);
            kettle.anchors = new[]
            {
                new ArtAnchor { clip = "action", frame = 0, name = "muzzle", px = new Vector2(200, 150) },
                new ArtAnchor { clip = "action", frame = 2, name = "muzzle", px = new Vector2(220, 100) },
            };
            var martini = AddBody("martini_spirit", 1.25f, 182f, Clip("idle", 4, 6f, true), Clip("action", 4, 12f, false));
            martini.anchors = new[] { new ArtAnchor { clip = "action", frame = 0, name = "emitter", px = new Vector2(172, 62) } };
            var parent = new GameObject("Parent").transform;
            created.Add(parent.gameObject);

            var enemy = Enemy.SpawnAt(new Vector2(3f, 1f), EnemyDef.KettleCannon, 1f, null, parent);
            Vector2 root = enemy.transform.position;
            float r = EnemyDef.KettleCannon.radius;
            float s = 1.3f / (166f / 128f);
            Vector2 Expected(Vector2 px, float dir) =>
                root + new Vector2(dir * (px.x - 128f) / 128f * s, (px.y - 32f) / 128f * s - r);

            Assert.IsTrue(enemy.TryGetActionAnchorWorld("action", "muzzle", out _), "재생 전에는 0프레임 anchor");
            Assert.IsTrue(enemy.PlayAction("action"));
            Assert.IsTrue(enemy.TryGetActionAnchorWorld("action", "muzzle", out var w0));
            Assert.That(Vector2.Distance(Expected(new Vector2(200, 150), 1f), w0), Is.LessThan(1e-4f));
            Assert.That(Vector2.Distance(w0, RangedAttackDriver.EmitterOrigin(enemy)), Is.LessThan(1e-4f), "T 투척은 muzzle에서");

            enemy.Animator.Advance(2f / 12f + 0.001f);
            Assert.AreEqual(2, enemy.Animator.FrameIndex);
            Assert.IsTrue(enemy.TryGetActionAnchorWorld("action", "muzzle", out var w2), "지금 보이는 프레임의 anchor");
            Assert.That(Vector2.Distance(Expected(new Vector2(220, 100), 1f), w2), Is.LessThan(1e-4f));
            Assert.IsFalse(enemy.TryGetActionAnchorWorld("action", "muzzle", out var none, 1), "anchor 없는 프레임은 false");
            Assert.AreEqual(root, none, "없으면 몸 중심(기존 발사 위치)");
            Assert.IsFalse(enemy.TryGetActionAnchorWorld("review", "reviewOrigin", out _), "없는 클립은 false");

            enemy.FaceTowards(root.x - 10f, 1f);
            Assert.IsTrue(enemy.TryGetActionAnchorWorld("action", "muzzle", out var wl, 0));
            Assert.That(Vector2.Distance(Expected(new Vector2(200, 150), -1f), wl), Is.LessThan(1e-4f), "왼쪽을 보면 anchor도 반전");
            Assert.AreEqual(Vector3.one, enemy.transform.localScale, "루트 스케일 1 유지");
            Assert.AreEqual(r, enemy.GetComponent<CircleCollider2D>().radius, 1e-4f, "판정 반지름 불변");

            var spirit = Enemy.SpawnAt(Vector2.zero, EnemyDef.MartiniSpirit, 1f, null, parent);
            spirit.PlayAction("action");
            Assert.IsTrue(spirit.TryGetActionAnchorWorld("action", "emitter", out var we));
            Assert.That(Vector2.Distance(we, RangedAttackDriver.EmitterOrigin(spirit)), Is.LessThan(1e-4f), "muzzle 없으면 emitter");
            Assert.AreNotEqual((Vector2)spirit.transform.position, we);

            var iconOnly = Enemy.SpawnAt(new Vector2(-2f, 0f), EnemyDef.Imp, 1f, null, parent);
            Assert.AreEqual((Vector2)iconOnly.transform.position, RangedAttackDriver.EmitterOrigin(iconOnly), "아이콘 몹은 몸 중심");
        }

        [Test]
        public void Enemy_WithoutBodyArt_KeepsExistingIcon()
        {
            var parent = new GameObject("Parent").transform;
            created.Add(parent.gameObject);
            var enemy = Enemy.SpawnAt(Vector2.zero, EnemyDef.Imp, 1f, null, parent);
            Assert.IsFalse(enemy.UsesBodyArt);
            Assert.AreEqual(Resources.Load<Sprite>(EnemyDef.Imp.spriteResourcePath), enemy.GetComponentInChildren<SpriteRenderer>().sprite);
            Assert.AreEqual("lemon_brute", EnemyDef.Brute.ArtId);
            Assert.AreEqual("milk_foamer", EnemyDef.MilkFoamer.ArtId, "_bgfix 접미사 제거");
        }

        [Test]
        public void GroundZones_ScaleSoCircularHitboxFitsInsideEllipse()
        {
            AddFlat("olive_fire", 256, 128, new Rect(0.05f, 0.1f, 0.9f, 0.8f), Clip("effect", 4, 8f, true, 256, 128, new Vector2(0.5f, 0.5f)));
            AddFlat("sugar_pool", 256, 128, new Rect(0.05f, 0.1f, 0.9f, 0.8f), Clip("effect", 1, 0f, false, 256, 128, new Vector2(0.5f, 0.5f)));
            var parent = new GameObject("Zones").transform;
            created.Add(parent.gameObject);
            const float r = 1.5f;
            BurstZone.Spawn(Vector2.zero, r, 5f, 3f, null, parent);
            SlowZone.Spawn(Vector2.zero, r, 0.5f, 3f, null, parent);
            foreach (Transform z in parent)
            {
                string v1 = z.name == "BurstZone" ? "olive_fire" : "sugar_pool";
                string v2 = BurstZone.V2For(v1);
                if (ZoneVisualV2.Available(v2))
                {
                    // 43(2026-10-12): v2는 그림 경계 = 판정 원(가로·세로 2r)으로 정확히 맞춘다(v1처럼 원을 타원 안에 넣어 넓게 보이지 않게).
                    var half = ZoneVisualV2.VisualHalfExtents(v2, r);
                    Assert.AreEqual(r, half.x, 1e-3f, $"{z.name}: v2 가로 경계 = 판정 반경");
                    Assert.AreEqual(r, half.y, 1e-3f, $"{z.name}: v2 세로 경계 = 판정 반경");
                    Assert.IsNotNull(z.GetComponentInChildren<SpriteAnimator>(), "v2 장판은 반복 재생");
                    Assert.AreEqual(SortOrders.GroundZone, z.GetChild(0).GetComponent<SpriteRenderer>().sortingOrder);
                    continue;
                }
                var art = ArtLibrary.Get(v1);
                float shortAxisWorld = Mathf.Min(art.ContentWorldSize.x, art.ContentWorldSize.y) * z.localScale.x;
                Assert.GreaterOrEqual(shortAxisWorld + 1e-4f, 2f * r, $"{z.name}: 판정 원이 그림 안에");
                Assert.AreEqual(SortOrders.GroundZone, z.GetComponent<SpriteRenderer>().sortingOrder);
            }
            Assert.IsNotNull(parent.Find("BurstZone").GetComponentInChildren<SpriteAnimator>(), "4프레임 장판은 반복 재생");
        }

        [Test]
        public void Projectiles_UseArtFacingRight_WithIconFallback()
        {
            AddFlat("bullet", 128, 128, new Rect(0.3f, 0.45f, 0.4f, 0.1f), Clip("effect", 1, 0f, false, 128, 128, new Vector2(0.5f, 0.5f)));
            AddFlat("glass_shard", 128, 128, new Rect(0.3f, 0.4f, 0.4f, 0.2f), Clip("effect", 1, 0f, false, 128, 128, new Vector2(0.5f, 0.5f)));
            var parent = new GameObject("P").transform;
            created.Add(parent.gameObject);

            PlayerProjectile.Spawn(Vector2.zero, Vector2.up, 5f, 1f, 0, parent, "bullet");
            var pp = parent.GetComponentInChildren<PlayerProjectile>();
            Assert.AreSame(ArtLibrary.Sprite("bullet"), pp.GetComponent<SpriteRenderer>().sprite);
            Assert.AreEqual(90f, pp.transform.eulerAngles.z, 0.01f, "오른쪽이 앞인 탄을 진행 방향으로");

            EnemyProjectile.Spawn(Vector2.zero, Vector2.right, 1f, 1f, null, parent, EnemyProjectile.ArtPrefix + "glass_shard");
            var ep = parent.GetComponentInChildren<EnemyProjectile>();
            Assert.AreSame(ArtLibrary.Sprite("glass_shard"), ep.GetComponent<SpriteRenderer>().sprite);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, ep.transform.eulerAngles.z), 0.01f, "새 VFX 탄은 +90° 보정 없음");

            EnemyProjectile.Spawn(Vector2.zero, Vector2.right, 1f, 1f, null, parent, EnemyProjectile.ArtPrefix + "ice_shard");
            var fallback = parent.GetComponentsInChildren<EnemyProjectile>().Last();
            Assert.AreEqual(Resources.Load<Sprite>("Icons/Enemies/ice_shard"), fallback.GetComponent<SpriteRenderer>().sprite,
                "v1 아트가 없으면 기존 아이콘");
        }

        [Test]
        public void UiSkin_UsesSlicedSprite_AndIconsAreOptional()
        {
            var button = MakeSprite(256, 64, new Vector2(0.5f, 0.5f), new Vector4(12, 12, 12, 12));
            Add(new ArtAsset { assetId = "ui_button", kind = "ui", status = "ready", frameSizePx = new Vector2(256, 64),
                clips = new[] { new ArtClip { name = "effect", frames = new[] { button } } } });
            var go = new GameObject("Btn", typeof(RectTransform));
            created.Add(go);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.5f, 0.2f, 0.2f, 0.9f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            UiFactory.ApplyHoverColors(b);
            Assert.AreSame(button, img.sprite);
            Assert.AreEqual(Image.Type.Sliced, img.type, "12px 9-slice");
            Assert.AreEqual(0.9f, img.color.a, 1e-4f);
            Assert.IsNull(UiFactory.AddIcon(go.transform, "ui_missing", Vector2.zero, Vector2.zero, 40f));
        }

        [Test]
        public void CafeBackground_UsesArtTilesAndSnapStep_OrPlaceholder()
        {
            var parent = new GameObject("Game").transform;
            created.Add(parent.gameObject);
            var placeholder = CafeBackground.Create(parent);
            Assert.IsFalse(CafeBackground.UsesArt);
            Assert.AreSame(SpriteFactory.Tile, placeholder.GetComponent<SpriteRenderer>().sprite);

            Add(new ArtAsset { assetId = "cafe_floor", kind = "background", status = "ready", frameSizePx = new Vector2(512, 256),
                clips = new[] { Clip("effect", 1, 0f, false, 512, 256, new Vector2(0.5f, 0.5f)) } });
            Add(new ArtAsset { assetId = "cafe_wall", kind = "background", status = "ready", frameSizePx = new Vector2(1024, 512), pixelsPerUnit = 128,
                clips = new[] { Clip("effect", 1, 0f, false, 1024, 512, new Vector2(0.5f, 0.5f)) } });
            var bg = CafeBackground.Create(parent);
            Assert.IsTrue(CafeBackground.UsesArt);
            Assert.AreEqual(CafeBackground.ArtSnapStep, CafeBackground.SnapStep);
            var wall = bg.Find("Wall").GetComponent<SpriteRenderer>();
            Assert.AreEqual(SpriteDrawMode.Tiled, wall.drawMode);
            Assert.GreaterOrEqual(wall.transform.position.y - 2f, Balance.DepthMax, "벽은 이동 레인 위");
            Assert.AreEqual(0f, CafeBackground.ArtSnapStep % 8f, 1e-4f, "벽 반복(8유닛)의 배수로 스냅");
        }
    }

    /// <summary>실제로 구운 Resources/ArtV1/ArtCatalog(통합기 산출물) 내용 검사 — 1차 반입 범위와 상태 구분.</summary>
    public class BakedArtCatalogTests
    {
        static readonly string[] Support42 =
        {
            "ui_pause", "ui_settings", "ui_codex", "ui_save_ok", "ui_save_error", "seal_lock", "skill_attack", "skill_survival",
            "skill_ingredient", "specialty", "status_suppressed", "status_invulnerable", "status_slow", "glass_shard", "bullet",
            "arrow", "tea_arrow", "leaf_mote", "ground_shadow", "warning_line", "warning_ring", "boss_arrival", "summon_ring",
            "sugar_pool", "coffee_pool", "player_melee_area", "hit_spark", "pickup_glint", "levelup_glint", "melee_streak",
            "steam_jet", "review_star", "seal_burst", "pressure_wave", "hybrid_tea_martini", "weapon_tea_bow", "olive_fire",
            "ui_panel", "ui_card_frame", "ui_button",
        };

        [SetUp]
        public void Load() => ArtLibrary.SetCatalogForTests(null);

        /// <summary>사용자 지시(2026-10-08): 총 시각 크기 2배 — manifest 목표 0.4→0.8. 손잡이 grip은 손 피벗에 고정, 좌우 모두 같은 크기,
        /// 총구 anchor는 바라보는 쪽 앞. 피해·사거리·투사체는 이 값과 무관하다.</summary>
        [TestCase(1f)]
        [TestCase(-1f)]
        public void HeldGun_IsTwiceOldLength_GripOnHand_MuzzleForward(float facing)
        {
            var root = new GameObject("GunSizeTest");
            try
            {
                var visual = PlayerVisual.Create(root.transform, PlayerController.Radius, Color.white, "bartender");
                visual.Tick(facing, false, false);
                var gun = ArtLibrary.Get("weapon_gun");
                Assert.AreEqual(0.8f, gun.targetVisualLengthRelativeToPlayer, 1e-4f, "구운 카탈로그 총 목표 길이(기존 0.4의 2배)");
                Assert.IsTrue(visual.AttachWeaponArt("weapon_gun", 0.4f));
                var r = visual.Weapon;
                float body = visual.BodyArt.targetBodyHeightWorld;
                var content = gun.ContentWorldSize;
                float visibleLength = Mathf.Max(content.x, content.y) * Mathf.Abs(r.transform.lossyScale.y);
                Assert.AreEqual(body * 0.8f, visibleLength, 0.002f, "보이는 총 길이 = 몸 높이 × 0.8");
                Assert.IsTrue(gun.TryGetAnchor(gun.Main.name, 0, "grip", out var grip));
                Vector2 gripWorld = r.transform.TransformPoint(gun.AnchorToLocal(grip));
                Assert.Less(Vector2.Distance(gripWorld, r.transform.parent.position), 0.0001f, "손잡이가 손 피벗에 붙어 있다");
                Assert.IsTrue(visual.TryGetMuzzleWorld(facing, out var muzzle));
                Assert.Greater((muzzle.x - gripWorld.x) * facing, 0f, "총구는 바라보는 쪽 앞");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("weapon_bow", false, 1f)]
        [TestCase("weapon_bow", false, -1f)]
        [TestCase("weapon_tea_bow", true, 1f)]
        [TestCase("weapon_tea_bow", true, -1f)]
        public void HeldBow_FacesForward_WithGripAndLaunchAnchorsPreserved(string id, bool secondary, float facing)
        {
            var root = new GameObject("BowOrientationTest");
            try
            {
                var visual = PlayerVisual.Create(root.transform, PlayerController.Radius, Color.white, "tea_sommelier");
                visual.Tick(facing, false, false); // 왼쪽을 보는 중에 장착해도 확대율이 같아야 한다.
                Assert.IsTrue(secondary ? visual.AttachSecondaryWeaponArt(id) : visual.AttachWeaponArt(id, 0.7f));
                var renderer = secondary ? visual.SecondaryWeapon : visual.Weapon;
                var asset = ArtLibrary.Get(id);
                Assert.IsTrue(asset.TryGetAnchor(asset.Main.name, 0, "grip", out var grip));
                Vector2 gripWorld = renderer.transform.TransformPoint(asset.AnchorToLocal(grip));
                Assert.Less(Vector2.Distance(gripWorld, renderer.transform.parent.position), 0.0001f,
                    "반전해도 손잡이는 손 피벗에 고정돼야 한다");
                Assert.Less(renderer.transform.localScale.x, 0f, "활대가 전방, 시위가 몸 쪽을 향해야 한다");
                Assert.AreEqual(Mathf.Abs(renderer.transform.localScale.x), renderer.transform.localScale.y, 0.0001f);
                Assert.Less(renderer.transform.localScale.y, 10f, "왼쪽 장착 시 부모 음수 스케일로 거대해지면 안 된다");
                if (!secondary)
                {
                    // 2026-10-09 사용자 피드백: 주무기 활은 몸 높이의 0.5(원래 0.7은 몸통을 덮음).
                    // 2026-10-09: 손에 든 활은 v2 그림(있으면)의 실제 알파 길이 기준.
                    float visible = PlayerVisual.HeldWeaponContentLongestWorld(asset, id) * Mathf.Abs(renderer.transform.lossyScale.y);
                    Assert.AreEqual("weapon_bow_v2_right_00", renderer.sprite.name, "사용자 피드백 활 v2 사용");
                    Assert.AreEqual(visual.BodyArt.targetBodyHeightWorld * PlayerVisual.HeldBowLengthRatio, visible, 0.002f);
                }
                Vector2 launch;
                Assert.IsTrue(secondary ? visual.TryGetSecondaryLaunchWorld(out launch) : visual.TryGetMuzzleWorld(facing, out launch));
                Assert.Less((launch.x - gripWorld.x) * facing, 0f, "시위의 발사 앵커도 그림과 함께 반전돼야 한다");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BakedCatalog_HasAll42ApprovedSupportAssets_WithManifestFrameCountsAndPivots()
        {
            var catalog = ArtLibrary.Catalog;
            Assert.IsNotNull(catalog, "Resources/ArtV1/ArtCatalog — CCT/Art/Integrate First Pass Art 실행 필요");
            var byId = catalog.assets.ToDictionary(a => a.assetId);
            int frames = 0;
            foreach (var id in Support42)
            {
                Assert.IsTrue(byId.ContainsKey(id), id);
                var a = byId[id];
                Assert.AreEqual("ready", a.status, id);
                Assert.IsFalse(a.firstPassProvisional, id);
                Assert.IsTrue(a.clips.All(c => c.frames.All(f => f != null)), $"{id} 스프라이트 참조");
                Assert.AreEqual(new Vector2(0.5f, 0.5f), a.pivotNormalized, id);
                frames += a.clips.Sum(c => c.frames.Length);
            }
            Assert.AreEqual(73, frames, "35 removes two floor effects (8 frames) from 42 support assets; 40/73 active remain.");
            Assert.AreEqual(0.22f, byId["ground_shadow"].runtimeOpacity, 1e-4f);
            Assert.AreEqual(0.12f, byId["player_melee_area"].runtimeOpacity, 1e-4f);
            Assert.IsTrue(byId["summon_ring"].TryGetTint("tea", out _));
            Assert.IsTrue(byId["weapon_tea_bow"].TryGetAnchor("effect", 0, "grip", out var grip));
            Assert.AreEqual(new Vector2(94, 126), grip);
            Assert.AreNotEqual(Vector4.zero, byId["ui_panel"].FirstSprite.border, "9-slice border 임포트");
            Assert.AreEqual(16f, byId["hit_spark"].Main.fps);
        }

        [Test]
        public void BakedCatalog_EmitterAnchors_CoverEveryActionFrame_ForTheirFiringDrivers()
        {
            var byId = ArtLibrary.Catalog.assets.ToDictionary(a => a.assetId);
            var contracts = new[]
            {
                (def: EnemyDef.DifficultCustomer, clip: "review", anchor: CustomerBossDriver.ReviewOriginAnchor),
                (def: EnemyDef.KettleCannon, clip: "action", anchor: "muzzle"),
                (def: EnemyDef.ShakerGolem, clip: "action", anchor: "muzzle"),
                (def: EnemyDef.TeaCeremonyGuardian, clip: "action", anchor: "muzzle"),
                (def: EnemyDef.MartiniSpirit, clip: "action", anchor: "emitter"),
                (def: EnemyDef.MilkFoamer, clip: "action", anchor: "emitter"),
                (def: EnemyDef.LeafWarden, clip: "action", anchor: "summon"),
                (def: EnemyDef.SwirlConjuror, clip: "action", anchor: "summon"),
            };
            foreach (var c in contracts)
            {
                string id = c.def.ArtId;
                Assert.IsTrue(byId.ContainsKey(id), id);
                var a = byId[id];
                var clip = a.Clip(c.clip);
                Assert.IsNotNull(clip, $"{id}.{c.clip}");
                for (int f = 0; f < clip.frames.Length; f++)
                {
                    Assert.IsTrue(a.TryGetAnchor(c.clip, f, c.anchor, out var px), $"{id}.{c.clip}[{f}].{c.anchor}");
                    Assert.That(px.x, Is.InRange(0f, a.frameSizePx.x), id);
                    Assert.That(px.y, Is.InRange(0f, a.frameSizePx.y), id);
                }
                if (c.anchor == "summon") Assert.AreEqual(EnemyArchetype.Summoner, c.def.archetype, id);
                else if (!c.def.IsBoss)
                    Assert.That(c.def.archetype, Is.EqualTo(EnemyArchetype.Ranged).Or.EqualTo(EnemyArchetype.EliteBarrage), id);
            }
        }

        [Test]
        public void BakedCatalog_ApprovedBartender_IsReady_AndNoInterpolatedV3V4()
        {
            var catalog = ArtLibrary.Catalog;
            var bartender = catalog.assets.SingleOrDefault(a => a.assetId == "bartender");
            Assert.IsNotNull(bartender);
            Assert.IsFalse(bartender.firstPassProvisional, "실제 동작 검수 승인 후 정식 import에는 provisional이 없어야 한다");
            Assert.AreEqual("ready", bartender.status);
            Assert.IsFalse(catalog.assets.Any(a => a.firstPassProvisional), "정식 catalog 전체 임시 아트 제외");
            var idle = bartender.Clip("idle");
            var move = bartender.Clip("move");
            Assert.AreEqual((4, 3f), (idle.frames.Length, idle.fps), "v5 idle 4/3fps");
            Assert.AreEqual((6, 10f), (move.frames.Length, move.fps), "v5 move 6/10fps(v3/v4 12/15장 보간본 아님)");
            Assert.IsTrue(bartender.sourcePath.StartsWith("Assets/_Project/Art/v1/bartender"));
            Assert.IsTrue(File.Exists(bartender.sourcePath + "/manifest.json"), "정식 원본 매니페스트도 임포트돼야 한다");
            Assert.IsNotNull(bartender.Clip("melee"), "승인된 최종 바텐더 주검 클립");
            Assert.AreEqual(4, bartender.Clip("melee").frames.Length);
            Assert.Greater(bartender.anchors.Length, 0, "23프레임 소스의 프레임별 anchor가 구워져야 한다");
        }

        [Test]
        public void BakedCatalog_ReadyBodies_HaveMeasuredHeightsAnd04ClipCounts()
        {
            foreach (var a in ArtLibrary.Catalog.assets.Where(x => x.IsBody && !x.firstPassProvisional))
            {
                Assert.Greater(a.bodyHeightPx, 0f, a.assetId);
                Assert.Greater(a.targetBodyHeightWorld, 0f, a.assetId);
                Assert.AreEqual(4, a.Clip("idle").frames.Length, a.assetId);
                if (a.Clip("move") != null) Assert.AreEqual(6, a.Clip("move").frames.Length, a.assetId);
            }
        }
    }

    /// <summary>08:122~124 설정 효과 — 흔들림/효과 강도/피해 숫자.</summary>
    public class SettingsEffectsTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            SettingsService.SetShakePercent(50);
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            SettingsService.SetDamageNumbers(true);
            DamageNumbers.EnableInEditModeForTests = false;
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            foreach (var n in Object.FindObjectsByType<DamageNumbers>(FindObjectsSortMode.None)) Object.DestroyImmediate(n.gameObject);
        }

        [Test]
        public void Shake_ScalesWithSetting_AndZeroDisables()
        {
            var cam = new GameObject("Cam").AddComponent<Camera>();
            var target = new GameObject("T").transform;
            created.Add(cam.gameObject);
            created.Add(target.gameObject);
            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Init(cam, target, null); // Init이 현재 리그로 등록(EditMode에선 OnEnable이 불리지 않음)

            SettingsService.SetShakePercent(0);
            CameraRig.Shake(1f, 0.5f);
            Assert.AreEqual(0f, CameraRig.CurrentShakeAmplitude, "0이면 완전 비활성");
            SettingsService.SetShakePercent(50);
            CameraRig.Shake(1f, 0.5f);
            Assert.AreEqual(0.5f, CameraRig.CurrentShakeAmplitude, 1e-4f);
        }

        [Test]
        public void LowEffects_HalvesDecorativeParticles()
        {
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            Assert.AreEqual(22, ParticleFx.DecorativeCount(22));
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            Assert.AreEqual(11, ParticleFx.DecorativeCount(22));
            Assert.AreEqual(1, ParticleFx.DecorativeCount(1));
        }

        [Test]
        public void DamageNumbers_MergeSameEnemy_Cap30_AndHiddenWhenOff()
        {
            DamageNumbers.EnableInEditModeForTests = true;
            var parent = new GameObject("P").transform;
            created.Add(parent.gameObject);
            var a = Enemy.SpawnAt(Vector2.zero, EnemyDef.Imp, 1f, null, parent);
            var first = DamageNumbers.Show(a, 3f);
            var merged = DamageNumbers.Show(a, 4f);
            Assert.AreSame(first, merged, "0.12초 안 같은 적 적중은 합산");
            Assert.AreEqual("7", merged.Text);
            for (int i = 0; i < 40; i++)
                DamageNumbers.Show(Enemy.SpawnAt(new Vector2(i, 0), EnemyDef.Imp, 1f, null, parent), 1f);
            Assert.LessOrEqual(DamageNumbers.ActiveCount, DamageNumbers.MaxConcurrent);
            SettingsService.SetDamageNumbers(false);
            Assert.IsNull(DamageNumbers.Show(a, 5f), "끄면 숫자만 숨김");
        }

        [Test]
        public void SettingsView_EffectRows_TogglePersistAndLabel()
        {
            var canvas = new GameObject("C", typeof(RectTransform));
            created.Add(canvas);
            var view = SettingsView.Create(canvas.transform);
            view.Show();
            SettingsService.SetShakePercent(75);
            view.CycleShake();
            Assert.AreEqual(100, SettingsService.Current.shakePercent);
            view.CycleShake();
            Assert.AreEqual(0, SettingsService.Current.shakePercent);
            StringAssert.Contains("끔", view.ShakeLabelText);
            view.ToggleEffectsLevel();
            Assert.AreEqual(SettingsData.EffectsLow, SettingsService.Current.effectsLevel);
            StringAssert.Contains("낮음", view.EffectsLabelText);
            view.ToggleDamageNumbers();
            Assert.IsFalse(SettingsService.Current.damageNumbers);
            Assert.AreEqual("꺼짐", view.DamageNumbersLabelText);
        }

        [Test]
        public void QuitGuard_ReportsEachUnsavedServiceSeparately()
        {
            string dir = Path.Combine(Application.temporaryCachePath, "cct_quitguard_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                SettingsService.SetForTests(Path.Combine(dir, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
                PermanentProgress.SetPathForTests(Path.Combine(dir, "save.json"));
                Assert.IsFalse(QuitGuard.HasUnsaved);
                SettingsService.FaultHookForTests = _ => throw new IOException("x");
                SettingsService.SetVolume(VolumeChannel.Master, 10);
                Assert.IsTrue(QuitGuard.HasUnsaved, "설정만 실패해도 종료 확인");
                StringAssert.Contains("설정", QuitGuard.UnsavedMessage);
                StringAssert.DoesNotContain("진행", QuitGuard.UnsavedMessage);
                Assert.IsFalse(QuitGuard.TrySaveEverything());
                SettingsService.FaultHookForTests = null;
                Assert.IsTrue(QuitGuard.TrySaveEverything(), "재시도 성공 시 종료 허용");
            }
            finally
            {
                SettingsService.FaultHookForTests = null;
                SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
                PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }

    /// <summary>1차 빌드 캡처(01_title.png)에서 발견한 타이틀 메뉴·제목 겹침의 회귀 방지(1920×1080 명목 좌표).</summary>
    public class TitleLayoutTests
    {
        [Test]
        public void MenuButtons_DoNotOverlapTitleOrSubtitle_AndClearSaveNotice()
        {
            var view = TitleView.Show();
            try
            {
                var root = view.transform.Find("Root");
                Rect Nom(string name)
                {
                    var rt = (RectTransform)root.Find(name);
                    var o = new Vector2(rt.anchorMin.x * 1920f, rt.anchorMin.y * 1080f) + rt.anchoredPosition - Vector2.Scale(rt.pivot, rt.sizeDelta);
                    return new Rect(o, rt.sizeDelta);
                }
                foreach (var button in new[] { "StartButton", "PermanentShopButton", "CardCodexButton", "SettingsButton", "CreditsButton", "QuitButton" })
                {
                    Assert.IsFalse(Nom(button).Overlaps(Nom("Title")), $"{button} ↔ Title");
                    Assert.IsFalse(Nom(button).Overlaps(Nom("Subtitle")), $"{button} ↔ Subtitle");
                    Assert.IsFalse(Nom(button).Overlaps(Nom("SaveNotice")), $"{button} ↔ SaveNotice");
                }
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }
    }

    /// <summary>도감 상세(08:95)·키보드 스크롤(G31)·탭/재료 행 겹침 수정.</summary>
    public class CodexDetailTests
    {
        GameObject canvas;

        [TearDown]
        public void TearDown()
        {
            if (canvas != null) Object.DestroyImmediate(canvas);
        }

        [Test]
        public void Rows_OpenDetail_RespectingUndiscoveredRules()
        {
            canvas = new GameObject("C", typeof(RectTransform));
            var codex = CardCodexView.Create(canvas.transform, null);
            codex.Show();
            Assert.IsTrue(codex.OpenDetailFor("dry_gin"));
            Assert.IsTrue(codex.IsDetailOpen);
            if (!PermanentProgress.Current.discoveredCardIds.Contains("dry_gin"))
                Assert.AreEqual("???", codex.DetailTitleText, "미발견 카드는 이름 숨김");
            codex.HideDetail();
            Assert.IsFalse(codex.IsDetailOpen);
            Assert.IsTrue(codex.OpenDetailFor("bean_cupper"), "영구 강화 행");
            StringAssert.Contains("영구 강화", codex.DetailBodyText);
            codex.HideDetail();
            var coffee = SkillDatabase.LoadAll()[0];
            Assert.IsTrue(codex.OpenDetailFor(coffee.id), "커피 노드 행");
            StringAssert.Contains("커피 노드", codex.DetailBodyText);
        }

        [Test]
        public void TabsAndMaterials_DoNotOverlap_AndScrollRevealsLowerRow()
        {
            canvas = new GameObject("C", typeof(RectTransform));
            var codex = CardCodexView.Create(canvas.transform, null);
            codex.Show();
            var root = codex.transform;
            var materials = (RectTransform)root.Find("Materials");
            var tabs = (RectTransform)root.Find("Tabs");
            float materialsBottom = materials.anchoredPosition.y - materials.sizeDelta.y; // 위 고정 피벗
            float tabsTop = tabs.anchoredPosition.y;
            Assert.LessOrEqual(tabsTop, materialsBottom, "탭 행이 재료 행 아래");

            var scrollArea = root.Find("ScrollArea");
            var content = (RectTransform)scrollArea.Find("Viewport/Content");
            var reveal = scrollArea.GetComponent<ScrollToSelected>();
            var lower = content.Cast<RectTransform>().OrderBy(t => t.anchoredPosition.y).First();
            ((RectTransform)scrollArea).sizeDelta = new Vector2(1450f, 600f);
            reveal.Reveal(lower);
            Assert.Greater(content.anchoredPosition.y, 0f, "아래쪽 행으로 스크롤");
        }
    }

    /// <summary>r4/r5 GUI QA(2026-10-05, 720p) 결함: 결과 화면 08:105~106, Pause 아래 비침, 도감 상세 겹침.</summary>
    public class QaUiDefectTests
    {
        GameObject canvas;

        [TearDown]
        public void TearDown()
        {
            if (canvas != null) Object.DestroyImmediate(canvas);
        }

        [Test]
        public void Result_TitlesFollow08_105_ForEveryOutcome()
        {
            Assert.AreEqual("영업 완료", ResultView.TitleFor(true, "클리어"));
            Assert.AreEqual("쓰러졌습니다", ResultView.TitleFor(false, "사망"));
            Assert.AreEqual("영업 종료", ResultView.TitleFor(false, "시간 초과"));
            Assert.AreEqual("런을 포기했습니다", ResultView.TitleFor(false, "포기"));
            Assert.AreEqual("오늘의 마지막 주문까지 마쳤습니다.", ResultView.ClearLine);
        }

        [Test]
        public void Result_StatsShowEvery08_106_Field()
        {
            var s = new ResultSummary
            {
                cleared = false, reason = "포기", job = Job.Bartender, weapon = WeaponKind.Rapier, hybridUnlocked = true,
                runSeconds = 26.7f, kills = 26, cocktailLevel = 2, teaLevel = 3, coffeeSkillsUnlocked = 1,
                cocktailCollected = 11, teaCollected = 7, beansCollected = 4, specialtyEarned = 2, seed = 123456789,
            };
            string text = ResultView.BuildStats(s, 5);
            foreach (var part in new[]
                     {
                         "바텐더", "레이피어", "하이브리드 티니 보우", "생존 시간  00:26", "처치  26", "칵테일 Lv 2", "차 Lv 3",
                         "커피 스킬 해금  1", "칵테일 11", "찻잎 7", "원두 4", "이번 스페셜티  +2", "잔액  5", "seed  123456789",
                     })
                StringAssert.Contains(part, text);
            s.hybridUnlocked = false;
            s.job = Job.TeaSommelier;
            s.weapon = WeaponKind.Bow;
            s.cleared = true;
            text = ResultView.BuildStats(s, 0);
            StringAssert.Contains("티 소믈리에", text);
            StringAssert.Contains("활", text);
            StringAssert.Contains("하이브리드 없음", text);
            StringAssert.Contains("클리어 시간", text);
            Assert.AreEqual(6, text.Split('\n').Length, "6줄(1920×1080 기준 380px 상자)");
        }

        [Test]
        public void Pause_SettingsAndGuide_HidePauseMenuWhileOpen_AndRestoreOnClose()
        {
            canvas = new GameObject("C", typeof(RectTransform));
            var pause = PauseView.Create(canvas.transform, null);
            var settingsButton = pause.transform.Find("SettingsButton").GetComponent<Button>();
            settingsButton.onClick.Invoke();
            Assert.IsTrue(pause.IsSettingsOpen);
            Assert.IsTrue(pause.MenuHiddenForSubView, "설정 중 Pause 메뉴가 비치지 않는다");
            var group = pause.GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, group.alpha);
            Assert.IsFalse(group.blocksRaycasts, "가려진 Pause 버튼이 클릭을 먹지 않는다");
            pause.CloseSettings();
            Assert.IsFalse(pause.MenuHiddenForSubView, "설정을 닫으면 Pause 메뉴 복귀");
            Assert.AreEqual(1f, group.alpha);

            pause.transform.Find("TutorialButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(pause.MenuHiddenForSubView, "조작 안내 중에도 숨김");
            pause.CloseTutorial();
            Assert.IsFalse(pause.MenuHiddenForSubView);
        }

        [Test]
        public void CodexDetail_BodyEndsAboveCloseButton_AndCannotOverflowIntoIt()
        {
            canvas = new GameObject("C", typeof(RectTransform));
            var codex = CardCodexView.Create(canvas.transform, null);
            codex.Show();
            var longest = CardDatabase.LoadAll().OrderByDescending(c => (c.description + c.flavorText).Length).First();
            Assert.IsTrue(codex.OpenDetailFor(longest.id));
            var panel = (RectTransform)codex.transform.Find("Detail");
            var body = (RectTransform)panel.Find("Body");
            var close = (RectTransform)panel.Find("Close");
            float panelH = panel.sizeDelta.y;
            // UiFactory.Place는 피벗=앵커: 본문은 위 중앙 기준, 닫기 버튼은 아래 중앙 기준.
            float bodyBottomFromTop = -body.anchoredPosition.y + body.sizeDelta.y;
            float closeTopFromTop = panelH - (close.anchoredPosition.y + close.sizeDelta.y);
            Assert.Less(bodyBottomFromTop, closeTopFromTop, "본문 상자가 닫기 버튼 위에서 끝난다");
            var text = body.GetComponent<Text>();
            Assert.AreEqual(VerticalWrapMode.Truncate, text.verticalOverflow, "넘쳐도 버튼 영역으로 그려지지 않음");
            Assert.IsFalse(text.resizeTextForBestFit, "08:49 본문 24를 줄이지 않는다");
            Assert.GreaterOrEqual(text.fontSize, CardCodexView.MinBodyFontSize);
            // 가장 긴 카드 상세 전문(발견 상태와 같은 구성)이 상자 안에 다 들어간다.
            text.text = $"칵테일 카드\n{longest.description}\n최대 중첩 {longest.maxStacks}\n{longest.flavorText}";
            Assert.LessOrEqual(text.preferredHeight, body.sizeDelta.y, "가장 긴 상세도 잘리지 않음");
        }

        [Test]
        public void CoffeeTree_DisabledNodes_StayOpaque_NotWashedOutByDefaultTint()
        {
            canvas = new GameObject("C", typeof(RectTransform));
            var view = SkillTreeView.Create(canvas.transform);
            var skills = DefaultSkills.CreateRuntime();
            try
            {
                view.ShowReadOnly(new SkillTree(skills), () => 0, () => { }); // 포인트 0 → 1단계 "포인트 부족", 나머지 "선행 필요"
                var nodes = view.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Node_")).ToList();
                Assert.AreEqual(15, nodes.Count);
                foreach (var b in nodes)
                {
                    Assert.IsFalse(b.interactable, "조회 전용·포인트 0이라 전부 비활성");
                    Assert.AreEqual(Color.white, b.colors.disabledColor, b.name);
                }
            }
            finally
            {
                foreach (var s in skills) if (s != null) Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void Codex_AllText_AtLeast24At1080_AndColumnsFitSafeViewport()
        {
            canvas = new GameObject("C", typeof(RectTransform));
            var codex = CardCodexView.Create(canvas.transform, null);
            codex.Show();
            foreach (var t in codex.GetComponentsInChildren<Text>(true))
                Assert.GreaterOrEqual(t.fontSize, CardCodexView.MinBodyFontSize, $"{t.transform.parent.name}/{t.name} (720p 16px)");
            Assert.LessOrEqual(CardCodexView.ViewportWidth, 1920f - 2f * 48f, "08:49 안전 여백 48");
            var content = (RectTransform)codex.transform.Find("ScrollArea/Viewport/Content");
            foreach (RectTransform row in content)
            {
                float halfW = row.sizeDelta.x * 0.5f;
                Assert.LessOrEqual(Mathf.Abs(row.anchoredPosition.x) + halfW, CardCodexView.ViewportWidth * 0.5f + 0.01f, row.name);
                foreach (RectTransform child in row)
                {
                    if (child.GetComponent<Text>() == null) continue;
                    // 행 안 글줄은 행 높이 안에서 끝난다(이웃 행과 겹침 없음).
                    float bottom = -child.anchoredPosition.y + child.sizeDelta.y;
                    Assert.LessOrEqual(bottom, row.sizeDelta.y + 0.01f, $"{row.name}/{child.name}");
                }
            }
        }
    }

    /// <summary>
    /// r6 QA(2026-10-05, 실제 720p): 소스 문자열은 있었지만 Truncate 상자가 폰트 줄 높이보다 낮아 카드 이름·???·재료 합계가
    /// 화면에 그려지지 않았다. "문자열이 있다"가 아니라 실제 Text 렌더 경로(TextGenerator, 각 Text의 생성 설정·상자 크기)로
    /// 1080(배율 1)과 720p(배율 2/3)에서 보이는 글자 수를 검사한다.
    /// </summary>
    public class RenderedTextLayoutTests
    {
        GameObject canvas;
        string tempSave;

        [TearDown]
        public void TearDown()
        {
            if (canvas != null) Object.DestroyImmediate(canvas);
            if (tempSave != null)
            {
                PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
                try { File.Delete(tempSave); File.Delete(tempSave + ".bak"); } catch { /* 임시 파일 */ }
                tempSave = null;
            }
        }

        static readonly float[] Scales = { 1f, 2f / 3f }; // 1920×1080, 1280×720 (CanvasScaler match 0.5)

        /// <summary>Text가 실제로 그릴 글자 수(그 Text의 잘림 설정) / 잘림 없이 그렸을 때의 글자 수.</summary>
        static (int shown, int full, int lines, int fullLines) Render(Text t, float scale)
        {
            var size = t.rectTransform.rect.size;
            var s = t.GetGenerationSettings(size);
            s.scaleFactor = scale;
            var g = new TextGenerator();
            g.Populate(t.text, s);
            int shown = g.characterCountVisible, lines = g.lineCount;
            s.verticalOverflow = VerticalWrapMode.Overflow;
            g.Populate(t.text, s);
            return (shown, g.characterCountVisible, lines, g.lineCount);
        }

        static void AssertFullyVisible(Text t, string label)
        {
            foreach (var scale in Scales)
            {
                var r = Render(t, scale);
                Assert.Greater(r.full, 0, $"{label} 비어 있음: '{t.text}'");
                Assert.AreEqual(r.full, r.shown, $"{label} 배율 {scale:0.00}: '{t.text}' 중 일부/전부가 상자({t.rectTransform.rect.size})에서 잘림");
            }
        }

        static void AssertFirstLinesVisible(Text t, int maxLines, string label)
        {
            foreach (var scale in Scales)
            {
                var r = Render(t, scale);
                if (r.full == 0) continue;
                Assert.Greater(r.shown, 0, $"{label} 배율 {scale:0.00}: '{t.text}' 한 줄도 안 보임");
                Assert.GreaterOrEqual(r.lines, Mathf.Min(r.fullLines, maxLines), $"{label} 배율 {scale:0.00}: '{t.text}' {maxLines}줄까지 보여야 함");
            }
        }

        [Test]
        public void Checker_DetectsR6Geometry_26ptNameIn34pxTruncateBox_AsNotRendered()
        {
            canvas = new GameObject("C", typeof(RectTransform));
            var rt = UiFactory.Place("R6Name", canvas.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(440f, 34f));
            var t = UiFactory.AddText(rt, "롱 스터러  (일반)", 26, TextAnchor.MiddleLeft, Color.white);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            bool anyHidden = Scales.Any(sc => { var r = Render(t, sc); return r.shown < r.full; });
            Assert.IsTrue(anyHidden, "r6 실제 결함(이름 공백)을 이 검사가 잡아야 한다");
            Assert.Greater(UiFactory.LineHeight(26), 34f, "Noto Sans KR 26pt 줄 높이 > r6 상자 34");
            rt.sizeDelta = new Vector2(440f, UiFactory.TruncatedBoxHeight(26));
            AssertFullyVisible(t, "r7 높이의 이름");
        }

        void CheckCodex()
        {
            canvas = new GameObject("C", typeof(RectTransform));
            var codex = CardCodexView.Create(canvas.transform, null);
            codex.Show();
            var root = codex.transform;
            AssertFullyVisible(root.Find("Count").GetComponentInChildren<Text>(), "발견 수");
            foreach (Transform slot in root.Find("Materials"))
            {
                AssertFullyVisible(slot.Find("Header").GetComponent<Text>(), $"{slot.name} 합계");
                AssertFullyVisible(slot.Find("Varieties").GetComponent<Text>(), $"{slot.name} 품종");
            }
            int names = 0;
            foreach (RectTransform row in root.Find("ScrollArea/Viewport/Content"))
            {
                var name = row.Find("Name")?.GetComponent<Text>();
                if (name == null) continue;
                names++;
                AssertFullyVisible(name, $"{row.name} 이름");
                var desc = row.Find("Desc")?.GetComponent<Text>();
                if (desc != null) AssertFirstLinesVisible(desc, row.name.StartsWith("SkillRow_") ? 3 : 2, $"{row.name} 설명");
                foreach (var extra in new[] { "Flavor", "Prereq", "Effect" })
                {
                    var t = row.Find(extra)?.GetComponent<Text>();
                    if (t != null) AssertFirstLinesVisible(t, 1, $"{row.name} {extra}");
                }
            }
            Assert.AreEqual(35 + 24 + 15 + 24, names, "32: 카드35 + 영구24 + 커피15 + 41 히든24행 모두 검사");

            // 상세: 가장 긴 카드의 전문(발견 상태)이 닫기 버튼 위 상자 안에 전부 보인다.
            var longest = CardDatabase.LoadAll().OrderByDescending(c => (c.description + c.flavorText).Length).First();
            Assert.IsTrue(codex.OpenDetailFor(longest.id));
            var body = root.Find("Detail/Body").GetComponent<Text>();
            AssertFullyVisible(body, "상세 본문");
            AssertFullyVisible(root.Find("Detail/Title").GetComponent<Text>(), "상세 제목");
        }

        [Test]
        public void Codex_UndiscoveredState_NamesTotalsAndPlaceholdersActuallyRender()
        {
            tempSave = Path.Combine(Application.temporaryCachePath, $"cct_r7_codex_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempSave); // 빈 세이브: 카드 대부분 "???", 재료 0, 품종 ???
            CheckCodex();
        }

        [Test]
        public void Codex_FullyDiscoveredState_LongestNamesAndTotalsActuallyRender()
        {
            tempSave = Path.Combine(Application.temporaryCachePath, $"cct_r7_codex_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempSave);
            foreach (var c in CardDatabase.LoadAll()) PermanentProgress.DiscoverCard(c.id);
            foreach (var h in HiddenSkillDatabase.All) PermanentProgress.DiscoverCard(h.id); // 41 히든 발견 상태 조건·효과 줄
            foreach (var v in MaterialVarieties.Cocktail.Concat(MaterialVarieties.Tea).Concat(MaterialVarieties.Coffee))
                PermanentProgress.DiscoverMaterialVariety(v.id);
            PermanentProgress.AddMaterialsCollected(12345, 6789, 4321); // 큰 합계 숫자도 한 줄 안
            CheckCodex();
        }

        [Test]
        public void Result_Hierarchy38_BigTimeAndSpecialty_SmallStats_FoldedSeed_AboveButtons()
        {
            // 38(2026-10-09): 결과·시간·스페셜티를 크게, 직업/무기/하이브리드 보조 줄, 처치·레벨·재료 작은 통계, seed는 접힘.
            canvas = new GameObject("C", typeof(RectTransform));
            var result = ResultView.Create(canvas.transform, null);
            result.Show(new ResultSummary
            {
                cleared = true, reason = "클리어", job = Job.TeaSommelier, weapon = WeaponKind.Bow, hybridUnlocked = true,
                runSeconds = 1199f, kills = 9999, cocktailLevel = 12, teaLevel = 12, coffeeSkillsUnlocked = 15,
                cocktailCollected = 9999, teaCollected = 9999, beansCollected = 9999, specialtyEarned = 4, seed = int.MinValue,
            });
            var root = result.transform;
            Text T(string path) => root.Find(path).GetComponent<Text>();
            Assert.AreEqual("19:59", T("SummaryPanel/TimeValue").text);
            Assert.AreEqual("+4", T("SummaryPanel/SpecialtyValue").text);
            Assert.Greater(T("SummaryPanel/TimeValue").fontSize, T("SummaryPanel/Identity").fontSize, "시간이 보조 줄보다 크다");
            Assert.Greater(T("SummaryPanel/Identity").fontSize, T("StatsPanel/Stats").fontSize, "통계는 가장 작은 정보");
            StringAssert.Contains("하이브리드 티니 보우", T("SummaryPanel/Identity").text);
            foreach (var part in new[] { "처치  9999", "칵테일 Lv 12", "차 Lv 12", "커피 스킬 해금  15", "칵테일 9999", "찻잎 9999", "원두 9999" })
                StringAssert.Contains(part, T("StatsPanel/Stats").text);
            Assert.IsFalse(result.SeedVisible, "seed는 접힌 상세");
            root.Find("SeedToggle").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(result.SeedVisible);
            StringAssert.Contains($"seed  {int.MinValue}", T("Seed").text);
            StringAssert.Contains($"seed  {int.MinValue}", result.StatsText, "로그/검증 요약에는 seed 유지");

            foreach (var scale in Scales)
            {
                foreach (var (path, lines) in new[] { ("SummaryPanel/TimeValue", 1), ("SummaryPanel/SpecialtyValue", 1),
                             ("SummaryPanel/Identity", 1), ("SummaryPanel/BalanceValue", 1), ("StatsPanel/Stats", 2), ("Seed", 1), ("SeedToggle/Label", 1) })
                {
                    var t = T(path);
                    Assert.AreEqual(lines, Render(t, scale).fullLines, $"배율 {scale:0.00}: {path} 줄 수");
                    Assert.LessOrEqual(t.preferredHeight, t.rectTransform.rect.height + 1f, $"{path} 상자 안");
                }
            }
            float Bottom(string n) { var r = (RectTransform)root.Find(n); return r.anchoredPosition.y - r.sizeDelta.y * 0.5f; }
            float TopY(string n) { var r = (RectTransform)root.Find(n); return r.anchoredPosition.y + r.sizeDelta.y * 0.5f; }
            Assert.Greater(Bottom("SummaryPanel"), TopY("StatsPanel"), "핵심 패널 아래가 통계 위");
            Assert.Greater(Bottom("StatsPanel"), TopY("RestartButton"), "통계 아래가 버튼 위");
            Assert.Greater(Bottom("RestartButton"), TopY("SeedToggle"), "버튼 아래 접힌 상세");
            Assert.Greater(((RectTransform)root.Find("RestartButton")).sizeDelta.x, ((RectTransform)root.Find("TitleButton")).sizeDelta.x, "다시 영업 시작이 주 버튼");
            AssertFullyVisible(T("Title"), "결과 제목");
            AssertFullyVisible(T("Subtitle"), "클리어 문구");
        }
    }

    /// <summary>08:61~66 직업/무기 카드 문구 — 직업에 숨은 재료 편향이 있는 것처럼 보이는 문구 금지(r4 QA 지적).</summary>
    public class JobWeaponCopyTests
    {
        [Test]
        public void JobAndWeaponCards_UsePlanning08Copy_WithoutIngredientBias()
        {
            // 사용자 직업 차별화 승인: 정확한 발동 조건/수치가 선택 화면에 보여야 한다.
            StringAssert.Contains("새 칵테일 3종마다 12초", JobPreset.Bartender.description);
            StringAssert.Contains("피해 +20% · 공속 +15%", JobPreset.Bartender.description);
            StringAssert.Contains("같은 차의 추가 중첩당", JobPreset.TeaSommelier.description);
            StringAssert.Contains("피해 +2% · 초당 회복 +0.1", JobPreset.TeaSommelier.description);
            foreach (var job in new[] { JobPreset.Bartender, JobPreset.TeaSommelier })
                StringAssert.DoesNotContain("재료", job.description, job.name);
            Assert.AreEqual("전방 레인을 연속 공격. 칵테일3+차3 획득 시 티니 보우 해금.", WeaponPreset.Rapier.description.Replace("\n", " "));
            Assert.AreEqual("자동 조준 사격. 다중 발사·관통 카드로 강화.", WeaponPreset.Gun.description.Replace("\n", " "));
            // 48(2026-10-12) 사용자 승인 검 밸런스: 넓은 베기·피해·경직 설명
            Assert.AreEqual("넓게 베기 · 피해 +25%. 맞은 일반 몹 0.25초 경직(보스 제외). 근접 강화 카드를 사용.", WeaponPreset.Sword.description.Replace("\n", " "));
            Assert.AreEqual("자동 조준 사격. 기본 관통 1. 다중 발사·관통 카드로 강화.", WeaponPreset.Bow.description.Replace("\n", " "));
        }
    }
}
