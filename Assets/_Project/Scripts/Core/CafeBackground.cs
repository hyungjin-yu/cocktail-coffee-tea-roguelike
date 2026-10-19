using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 04 §8 배경(야간 카페 겸 바 1개): cafe_floor(바닥 반복) + cafe_wall(이동 레인 위 벽, 가로 반복) + cafe_counter·cafe_lamp
    /// 장식. 카메라·레인 범위는 그대로, 충돌·전경 가림 없음. 아트가 하나도 없으면 기존 격자 타일(SpriteFactory.Tile).
    /// 57(2026-10-15): 무한 스크롤 대신 유한 전투 구역 — 루트는 월드 x=0에 고정, 양 끝에 경계(AddArenaBounds), 가독성 톤 적용.
    /// </summary>
    public static class CafeBackground
    {
        /// <summary>배경 가로 폭. 57 유한 구역(±BattleArena.HalfWidth)과 벽 너머 OutsideReveal·여유를 덮는다.</summary>
        public const float Width = 72f;
        /// <summary>아트 사용 시 스냅 단위: 벽 8 유닛(1024px/128)·바닥 4 유닛·장식 배치 간격 24 유닛의 공약수 배수.</summary>
        public const float ArtSnapStep = 24f;
        public const float PlaceholderSnapStep = 2f;

        public static float SnapStep { get; private set; } = PlaceholderSnapStep;
        public static bool UsesArt { get; private set; }

        /// <summary>47(2026-10-12) 직업 맵: 바텐더=칵테일 바(bg_bar_*), 티 소믈리에=찻집(bg_tea_*). PixelLab+Aseprite 제작
        /// Resources/ArtExtra/&lt;prefix&gt;_floor|_wall|_counter|_lamp. 네 장이 모두 있을 때만 쓰고, 아니면 공용 카페(아래 기존 경로).</summary>
        public static string ActiveMapPrefix { get; private set; }

        public static Sprite JobSprite(string prefix, string part)
        {
            if (string.IsNullOrEmpty(prefix)) return null;
            var all = Resources.LoadAll<Sprite>($"ArtExtra/{prefix}_{part}");
            return all != null && all.Length > 0 ? all[0] : null;
        }

        public static Transform Create(Transform parent, Job? job = null)
        {
            var root = new GameObject("Background");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, Balance.LaneCenterY, 0f);
            ActiveMapPrefix = null;
            // 72 맵 외형(판매 코인): 공용 야간 카페를 장착하면 직업 맵 대신 기존 공용 카페 아트(4부속). 배경만 바뀌고 구역·충돌·스폰은 같다.
            bool cafeSkin = job != null && DrinkLab.EquippedMap == DrinkSales.CafeMap;
            if (job != null && !cafeSkin && TryJobMap(root.transform, JobPreset.For(job.Value).mapArtPrefix)) return root.transform;

            var floor = ArtLibrary.Get("cafe_floor");
            var wall = ArtLibrary.Get("cafe_wall");
            UsesArt = floor?.FirstSprite != null || wall?.FirstSprite != null;
            SnapStep = UsesArt ? ArtSnapStep : PlaceholderSnapStep;

            if (!UsesArt)
            {
                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Tile;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(Width, 40f);
                sr.sortingOrder = SortOrders.Background;
                AddArenaBounds(root.transform, Balance.DepthMax - Balance.LaneCenterY + 6f);
                return root.transform;
            }

            float wallBottomLocal = Balance.DepthMax - Balance.LaneCenterY + 0.6f;
            if (floor?.FirstSprite != null)
            {
                float floorHeight = wallBottomLocal + Balance.DepthHalfWidth + 3f;
                Tiled(root.transform, "Floor", floor.FirstSprite, new Vector2(Width, floorHeight),
                    new Vector2(0f, wallBottomLocal - floorHeight * 0.5f), SortOrders.Background);
            }
            else
            {
                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Tile;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(60f, 40f);
                sr.sortingOrder = SortOrders.Background;
            }

            if (wall?.FirstSprite != null)
            {
                float h = wall.frameSizePx.y / wall.pixelsPerUnit;
                Tiled(root.transform, "Wall", wall.FirstSprite, new Vector2(Width, h),
                    new Vector2(0f, wallBottomLocal + h * 0.5f), SortOrders.Background);
            }

            var counter = ArtLibrary.Sprite("cafe_counter");
            var lamp = ArtLibrary.Sprite("cafe_lamp");
            for (float x = -Width * 0.5f + 6f; x < Width * 0.5f; x += 12f)
            {
                if (counter != null) Prop(root.transform, "Counter", counter, new Vector2(x, wallBottomLocal + counter.bounds.size.y * 0.5f - 0.2f));
                if (lamp != null) Prop(root.transform, "Lamp", lamp, new Vector2(x + 6f, wallBottomLocal + 2.4f));
            }
            ApplyReadabilityTone(root.transform, ToneFor(null), wallBottomLocal, wallBottomLocal + Balance.DepthHalfWidth + 3f);
            AddArenaBounds(root.transform, wallBottomLocal + (wall?.FirstSprite != null ? wall.frameSizePx.y / wall.pixelsPerUnit : 6f));
            return root.transform;
        }

        static bool TryJobMap(Transform root, string prefix)
        {
            var floor = JobSprite(prefix, "floor");
            var wall = JobSprite(prefix, "wall");
            var counter = JobSprite(prefix, "counter");
            var lamp = JobSprite(prefix, "lamp");
            if (floor == null || wall == null || counter == null || lamp == null) return false;
            UsesArt = true;
            SnapStep = ArtSnapStep; // 벽 4·바닥 4 유닛 반복, 소품 12 유닛 — 24의 약수
            ActiveMapPrefix = prefix;
            float wallBottomLocal = Balance.DepthMax - Balance.LaneCenterY + 0.6f;
            float floorHeight = wallBottomLocal + Balance.DepthHalfWidth + 3f;
            Tiled(root, "Floor", floor, new Vector2(Width, floorHeight), new Vector2(0f, wallBottomLocal - floorHeight * 0.5f), SortOrders.Background);
            float h = wall.bounds.size.y;
            Tiled(root, "Wall", wall, new Vector2(Width, h), new Vector2(0f, wallBottomLocal + h * 0.5f), SortOrders.Background);
            for (float x = -Width * 0.5f + 6f; x < Width * 0.5f; x += 12f)
            {
                Prop(root, "Counter", counter, new Vector2(x, wallBottomLocal + counter.bounds.size.y * 0.5f - 0.2f));
                Prop(root, "Lamp", lamp, new Vector2(x + 6f, wallBottomLocal + 2.4f));
            }
            ApplyReadabilityTone(root, ToneFor(prefix), wallBottomLocal, floorHeight);
            AddArenaBounds(root, wallBottomLocal + h, prefix);
            return true;
        }

        /// <summary>57 P0-2 배경 가독성 톤(이미지 수정 없이 렌더 색만). floor/wall/prop 곱셈 색 + 바닥 위 반투명 중립 막(대비·채도 완화).</summary>
        public struct Tone
        {
            public Color floor, wall, prop, veil;
        }

        /// <summary>바(bg_bar): 첫 플레이 피드백 "배경이 복잡·진하고·밝아 적·탄·드롭이 묻힌다" — 판자 대비와 주황 채도를 크게 낮춘다.
        /// 찻집(bg_tea): 원래 차분해 부작용이 없도록 아주 약하게만. 그 외(공용 카페)는 바와 찻집 사이.</summary>
        public static Tone ToneFor(string prefix)
        {
            switch (prefix)
            {
                case "bg_bar":
                    return new Tone { floor = new Color(0.66f, 0.6f, 0.58f), wall = new Color(0.6f, 0.57f, 0.6f),
                        prop = new Color(0.72f, 0.68f, 0.68f), veil = new Color(0.13f, 0.11f, 0.13f, 0.34f) };
                case "bg_tea":
                    return new Tone { floor = new Color(0.9f, 0.9f, 0.88f), wall = new Color(0.86f, 0.86f, 0.86f),
                        prop = new Color(0.9f, 0.9f, 0.9f), veil = new Color(0.14f, 0.13f, 0.12f, 0.12f) };
                default:
                    return new Tone { floor = new Color(0.8f, 0.76f, 0.74f), wall = new Color(0.76f, 0.74f, 0.76f),
                        prop = new Color(0.82f, 0.8f, 0.8f), veil = new Color(0.13f, 0.11f, 0.13f, 0.22f) };
            }
        }

        /// <summary>테스트/보고용 — 마지막으로 만든 배경에 적용한 톤.</summary>
        public static Tone ActiveTone { get; private set; }

        static void ApplyReadabilityTone(Transform root, Tone tone, float wallBottomLocal, float floorHeight)
        {
            ActiveTone = tone;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                switch (sr.gameObject.name)
                {
                    case "Floor": sr.color = tone.floor; break;
                    case "Wall": sr.color = tone.wall; break;
                    case "Counter":
                    case "Lamp": sr.color = tone.prop; break;
                }
            }
            if (tone.veil.a <= 0f) return;
            // 같은 정렬 순서(Background)에서 z를 카메라 쪽으로 당겨 바닥 위·액터 아래에 그린다(직교 카메라 z 정렬).
            var veil = Rect(root, "ReadabilityVeil", new Vector2(Width, floorHeight),
                new Vector3(0f, wallBottomLocal - floorHeight * 0.5f, -0.01f), tone.veil, SortOrders.Background);
            veil.transform.SetAsLastSibling();
        }

        /// <summary>테스트/보고용 — 마지막 배경이 새 경계 모듈 아트를 썼는지.</summary>
        public static bool BoundaryArtUsed { get; private set; }

        /// <summary>57 P0-1 눈에 보이는 경계: 벽 너머 어두운 영역 + 경계 모듈. 판정은 BattleArena가 담당하고 이건 표시만.
        /// 57: 직업 맵에 &lt;prefix&gt;_boundary(Aseprite+PixelLab 64x128 기둥, 바닥 중앙 pivot)가 있으면 벽 위치에 깊이를 따라 여러 개 세우고(왼쪽은 좌우 반전),
        /// 없을 때만 임시 놋쇠 난간(개발용 대체)을 그린다.</summary>
        public static void AddArenaBounds(Transform root, float topLocal, string prefix = null)
        {
            float bottomLocal = Balance.DepthMin - Balance.LaneCenterY - 3.5f;
            float height = topLocal - bottomLocal;
            float midY = (topLocal + bottomLocal) * 0.5f;
            var module = JobSprite(prefix, "boundary");
            BoundaryArtUsed = module != null;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float edge = side * BattleArena.HalfWidth;
                Rect(root, side < 0 ? "ArenaOutsideLeft" : "ArenaOutsideRight", new Vector2(12f, height + 2f),
                    new Vector3(edge + side * 6f, midY, -0.02f), new Color(0.03f, 0.02f, 0.04f, 0.9f), SortOrders.BackgroundProp);
                if (module != null)
                {
                    // 기둥(바닥 중앙 pivot, 64x128 @64PPU)을 늘리지 않고 레인 깊이를 따라 여러 개 세운다(아래쪽일수록 앞).
                    int k = 0;
                    for (float y = Balance.DepthMin - Balance.LaneCenterY - 0.6f; y <= Balance.DepthMax - Balance.LaneCenterY + 0.7f; y += 1.45f, k++)
                    {
                        var post = new GameObject(side < 0 ? "ArenaBoundaryLeft" : "ArenaBoundaryRight");
                        post.transform.SetParent(root, false);
                        post.transform.localPosition = new Vector3(edge, y, -0.03f + y * 0.001f); // 아래(앞) 기둥이 카메라 쪽
                        var msr = post.AddComponent<SpriteRenderer>();
                        msr.sprite = module;
                        msr.flipX = side < 0;
                        msr.sortingOrder = SortOrders.BackgroundProp;
                    }
                    continue;
                }
                Rect(root, "ArenaRail", new Vector2(0.32f, height),
                    new Vector3(edge, midY, -0.03f), new Color(0.93f, 0.72f, 0.38f, 1f), SortOrders.BackgroundProp);
                for (float y = bottomLocal + 1f; y < topLocal; y += 2f)
                    Rect(root, "ArenaRailStud", new Vector2(0.6f, 0.22f),
                        new Vector3(edge, y, -0.04f), new Color(0.45f, 0.3f, 0.16f, 1f), SortOrders.BackgroundProp);
            }
        }

        static SpriteRenderer Rect(Transform parent, string name, Vector2 size, Vector3 localPos, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static void Tiled(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
            sr.sortingOrder = order;
        }

        static void Prop(Transform parent, string name, Sprite sprite, Vector2 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortOrders.BackgroundProp;
        }
    }
}
