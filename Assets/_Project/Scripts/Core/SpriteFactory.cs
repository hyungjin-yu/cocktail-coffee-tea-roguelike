using UnityEngine;

namespace CCT
{
    /// <summary>아트 에셋이 들어오기 전까지 쓰는 런타임 생성 플레이스홀더 스프라이트.</summary>
    public static class SpriteFactory
    {
        static Sprite square;
        static Sprite circle;
        static Sprite semicircle;
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
            semicircle = null;
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
        /// 지름 1유닛 반원(D자 모양). 피벗은 평평한 지름 변의 중점 — +X 쪽으로 볼록하게 그려서,
        /// 이 피벗을 캐릭터 위치에 놓고 바라보는 방향으로 회전시키면 "앞쪽 반원을 벤다"는 시각적
        /// 슬래시 이펙트가 된다 (근접 무기 횡베기 VFX용, 2026-09-25).
        /// </summary>
        static Sprite crescent;
        /// <summary>2026-10-09 사용자 요청: 근접 베기용 얇은 반월(초승달) 궤적. 오른쪽(+x)으로 볼록, 가운데가 가장 두껍고
        /// 양 끝으로 가늘어진다. 바깥 가장자리는 흰 하이라이트, 안쪽은 부드럽게 사라지며 넓은 면 채움이 없다.
        /// 48px Point 필터 — 카페 픽셀 스타일과 같은 굵은 픽셀. 색은 Spawn 색으로 곱한다.</summary>
        public static Sprite Crescent
        {
            get
            {
                if (crescent == null)
                {
                    const int size = 48;
                    var tex = NewTexture(size, size, FilterMode.Point, TextureWrapMode.Clamp);
                    var px = new Color32[size * size];
                    float r = size * 0.5f - 0.5f;
                    var c = new Vector2(size * 0.5f, size * 0.5f);
                    var inner = c - new Vector2(r * 0.16f, 0f);
                    float ri = r * 0.95f;
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            var p = new Vector2(x + 0.5f, y + 0.5f);
                            float dOuter = r - Vector2.Distance(p, c);       // >0: 바깥 원 안
                            float dInner = Vector2.Distance(p, inner) - ri;  // >0: 안쪽 원 밖
                            Color col = Color.clear;
                            if (dOuter >= 0f && dInner >= 0f && p.x >= c.x - r * 0.2f)
                            {
                                float band = dOuter + dInner;
                                float t = Mathf.Clamp01(dOuter / Mathf.Max(0.5f, band)); // 0=바깥 가장자리, 1=안쪽
                                float a = t < 0.35f ? 1f : Mathf.Lerp(1f, 0.35f, (t - 0.35f) / 0.65f);
                                float core = t < 0.3f ? 1f : 0.82f;
                                col = new Color(core, core, core * 0.96f, Mathf.Round(a * 4f) / 4f); // 계단식 알파(픽셀 느낌)
                            }
                            px[y * size + x] = col;
                        }
                    tex.SetPixels32(px);
                    tex.Apply();
                    crescent = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                    crescent.hideFlags = HideFlags.HideAndDontSave;
                }
                return crescent;
            }
        }

        public static Sprite Semicircle
        {
            get
            {
                if (semicircle == null)
                {
                    const int size = 64;
                    var tex = NewTexture(size, size, FilterMode.Bilinear, TextureWrapMode.Clamp);
                    var px = new Color32[size * size];
                    float r = size * 0.5f;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x + 0.5f - r;
                            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                            float a = dx >= 0f ? Mathf.Clamp01(r - d) : 0f;
                            px[y * size + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    semicircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                    semicircle.hideFlags = HideFlags.HideAndDontSave;
                }
                return semicircle;
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
