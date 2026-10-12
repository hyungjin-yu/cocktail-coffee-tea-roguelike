using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 04 §8 배경(야간 카페 겸 바 1개): cafe_floor(바닥 반복) + cafe_wall(이동 레인 위 벽, 가로 반복) + cafe_counter·cafe_lamp
    /// 장식. 카메라·레인 범위는 그대로, 충돌·전경 가림 없음. 아트가 하나도 없으면 기존 격자 타일(SpriteFactory.Tile).
    /// 루트는 CameraRig가 SnapStep 단위로만 좌우 이동시켜 무한 스크롤처럼 보이게 한다(반복 단위의 배수여야 접합이 안 튄다).
    /// </summary>
    public static class CafeBackground
    {
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
            if (job != null && TryJobMap(root.transform, JobPreset.For(job.Value).mapArtPrefix)) return root.transform;

            var floor = ArtLibrary.Get("cafe_floor");
            var wall = ArtLibrary.Get("cafe_wall");
            UsesArt = floor?.FirstSprite != null || wall?.FirstSprite != null;
            SnapStep = UsesArt ? ArtSnapStep : PlaceholderSnapStep;

            if (!UsesArt)
            {
                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Tile;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(60f, 40f);
                sr.sortingOrder = SortOrders.Background;
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
            return true;
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
