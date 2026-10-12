using UnityEngine;

namespace CCT
{
    /// <summary>파티클 한 번 터뜨리는 설정값. 기본 원(SpriteFactory.Circle)을 여러 개 흩뿌려서
    /// 가산 블렌딩(글로우)으로 겹치게 하는 게 기본 컨셉 — 손그림 프레임 없이도 "그냥 도형"보다
    /// 한 단계 낫게 보이려는 목적.</summary>
    public struct ParticleBurstConfig
    {
        public int count;
        public float minSpeed;
        public float maxSpeed;
        public float minLifetime;
        public float maxLifetime;
        public float minSize;
        public float maxSize;
        /// <summary>수명 시작~끝 색. 알파는 끝에서 0으로 자동 페이드.</summary>
        public Color startColor;
        public Color endColor;
        /// <summary>양수면 위로 뜨고(불꽃), 음수면 가라앉는다(연기/독).</summary>
        public float gravityModifier;
        /// <summary>이 반지름의 원 안에서 무작위 위치+방향으로 터진다 (0이면 한 점에서).</summary>
        public float shapeRadius;
        public int sortingOrder;

        public static ParticleBurstConfig Default => new ParticleBurstConfig
        {
            count = 20,
            minSpeed = 1.5f,
            maxSpeed = 4f,
            minLifetime = 0.35f,
            maxLifetime = 0.55f,
            minSize = 0.2f,
            maxSize = 0.4f,
            startColor = Color.white,
            endColor = new Color(1f, 1f, 1f, 0f),
            gravityModifier = 0f,
            shapeRadius = 0.1f,
            sortingOrder = 3,
        };
    }

    /// <summary>
    /// 절차적 도형(원/사각형)만 쓰던 이펙트를 파티클 시스템으로 업그레이드하기 위한 공용 헬퍼.
    /// 가산 블렌딩 머티리얼을 하나 만들어 재사용하고, 없으면(셰이더를 못 찾으면) 일반 알파
    /// 블렌딩으로 조용히 대체한다 — 최악의 경우에도 분홍색 "셰이더 없음" 표시는 안 뜨게.
    /// </summary>
    public static class ParticleFx
    {
        static Material additiveMaterial;
        static bool additiveMaterialFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            additiveMaterial = null;
            additiveMaterialFailed = false;
        }

        static Material AdditiveMaterial
        {
            get
            {
                if (additiveMaterial != null || additiveMaterialFailed) return additiveMaterial;

                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    additiveMaterialFailed = true;
                    return null;
                }

                var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 2f); // URP enum: 0 Alpha, 1 Premultiply, 2 Additive, 3 Multiply
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                ApplyCircleTexture(mat);
                additiveMaterial = mat;
                return additiveMaterial;
            }
        }

        /// <summary>원(SpriteFactory.Circle)의 부드러운 알파 텍스처를 씌운다 — 이게 없으면 파티클이
        /// 그냥 네모(빌보드 쿼드 원본 모양)로 보인다. URP 셰이더는 _BaseMap, 예전 셰이더는 _MainTex라
        /// 이름이 달라서 둘 다 시도한다.</summary>
        static void ApplyCircleTexture(Material mat)
        {
            var texture = SpriteFactory.Circle.texture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        }

        public static ParticleSystem SpawnBurst(Vector2 position, Transform parent, ParticleBurstConfig config)
        {
            var go = new GameObject("ParticleFx");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // AddComponent가 자동재생을 시작시키므로,
            // 설정을 다 마치기 전까지는 멈춰 둔다 (재생 중에 main.duration 등을 바꾸면 경고/무시됨).
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = config.maxLifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(config.minLifetime, config.maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(config.minSpeed, config.maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(config.minSize, config.maxSize);
            main.startColor = config.startColor;
            main.gravityModifier = config.gravityModifier;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)DecorativeCount(config.count)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.01f, config.shapeRadius);
            shape.radiusThickness = 1f; // 원 둘레가 아니라 안쪽 전체에서 시작

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(config.startColor, 0f),
                    new GradientColorKey(config.endColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(config.startColor.a, 0f),
                    new GradientAlphaKey(config.endColor.a, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0.5f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = config.sortingOrder;
            var material = AdditiveMaterial;
            if (material != null)
            {
                renderer.material = material;
            }
            else
            {
                // 가산 블렌딩 셰이더를 못 찾으면(드문 경우) 최소한 렌더는 되는 기본 스프라이트 머티리얼로.
                var fallback = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                ApplyCircleTexture(fallback);
                renderer.material = fallback;
            }

            ps.Play();
            return ps;
        }

        /// <summary>08:123 효과 강도 "낮음"이면 장식 파티클 수 50%(경고·탄환·판정 표시는 이 경로를 쓰지 않아 그대로).</summary>
        public static int DecorativeCount(int count) =>
            SettingsService.Current.effectsLevel == SettingsData.EffectsLow ? Mathf.Max(1, Mathf.CeilToInt(count * 0.5f)) : count;
    }
}
