using UnityEngine;

namespace CCT
{
    /// <summary>아트 에셋이 들어오기 전까지 쓰는 런타임 생성 플레이스홀더 스프라이트.</summary>
    public static class SpriteFactory
    {
        static Sprite square;
        static Sprite circle;
        static Sprite capsule;
        static Sprite tile;
        static Sprite cocktailIcon;
        static Sprite teaIcon;
        static Sprite coffeeBeanIcon;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            square = null;
            circle = null;
            capsule = null;
            tile = null;
            cocktailIcon = null;
            teaIcon = null;
            coffeeBeanIcon = null;
        }

        /// <summary>1x1 유닛 흰색 사각형.</summary>
        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var tex = NewTexture(4, 4, FilterMode.Point, TextureWrapMode.Clamp);
                    var px = new Color32[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    tex.SetPixels32(px);
                    tex.Apply();
                    square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                    square.hideFlags = HideFlags.HideAndDontSave;
                }
                return square;
            }
        }

        /// <summary>지름 1유닛 흰색 원.</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    const int size = 64;
                    var tex = NewTexture(size, size, FilterMode.Bilinear, TextureWrapMode.Clamp);
                    var px = new Color32[size * size];
                    float r = size * 0.5f;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                            float a = Mathf.Clamp01(r - d);
                            px[y * size + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                    circle.hideFlags = HideFlags.HideAndDontSave;
                }
                return circle;
            }
        }

        /// <summary>
        /// 세로로 긴 캡슐(스타디움) 모양. 벨트스크롤 시점에서 서 있는 캐릭터 실루엣 placeholder.
        /// 높이 1유닛 기준 너비 0.6유닛 — 원(Circle) 대신 플레이어·몹 본체에 쓴다.
        /// </summary>
        public static Sprite Capsule
        {
            get
            {
                if (capsule == null)
                {
                    const int w = 48;
                    const int h = 80;
                    var tex = NewTexture(w, h, FilterMode.Bilinear, TextureWrapMode.Clamp);
                    var px = new Color32[w * h];
                    float r = w * 0.5f;
                    float cx = w * 0.5f;
                    float topCy = r;
                    float botCy = h - r;

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float d;
                            if (y < topCy) d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, topCy));
                            else if (y > botCy) d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, botCy));
                            else d = Mathf.Abs(x + 0.5f - cx);
                            float a = Mathf.Clamp01(r - d);
                            px[y * w + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    capsule = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
                    capsule.hideFlags = HideFlags.HideAndDontSave;
                }
                return capsule;
            }
        }

        /// <summary>2x2 유닛 바닥 타일 (Tiled 드로우 모드용). 이동감을 주는 격자.</summary>
        public static Sprite Tile
        {
            get
            {
                if (tile == null)
                {
                    const int size = 128;
                    var tex = NewTexture(size, size, FilterMode.Bilinear, TextureWrapMode.Repeat);
                    var px = new Color32[size * size];
                    var fill = new Color(0.13f, 0.11f, 0.16f);
                    var line = new Color(0.17f, 0.15f, 0.21f);
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                            px[y * size + x] = (x < 2 || y < 2) ? line : fill;
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    tile = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f,
                        0, SpriteMeshType.FullRect);
                    tile.hideFlags = HideFlags.HideAndDontSave;
                }
                return tile;
            }
        }

        /// <summary>칵테일 재료 아이콘 (Resources/Icons). 아트가 없으면 null — 호출 쪽에서 Circle+색상으로 대체.</summary>
        public static Sprite CocktailIngredientIcon => LoadIcon(ref cocktailIcon, "Icons/cocktail_ingredient");

        /// <summary>찻잎(차) 아이콘.</summary>
        public static Sprite TeaIcon => LoadIcon(ref teaIcon, "Icons/tea");

        /// <summary>커피 원두 아이콘.</summary>
        public static Sprite CoffeeBeanIcon => LoadIcon(ref coffeeBeanIcon, "Icons/coffee_bean");

        static Sprite LoadIcon(ref Sprite cache, string resourcesPath)
        {
            if (cache == null) cache = Resources.Load<Sprite>(resourcesPath);
            return cache;
        }

        static Texture2D NewTexture(int w, int h, FilterMode filter, TextureWrapMode wrap)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = wrap,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
    }
}
