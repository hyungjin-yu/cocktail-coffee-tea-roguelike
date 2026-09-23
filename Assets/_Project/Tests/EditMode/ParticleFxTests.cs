using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>ParticleFx.SpawnBurst (절차적 도형 대신 파티클로 이펙트를 만드는 공용 헬퍼).</summary>
    public class ParticleFxTests
    {
        [Test]
        public void SpawnBurst_CreatesOneShotBurstThatCleansUpAfterItself()
        {
            var config = ParticleBurstConfig.Default;
            config.count = 15;

            var ps = ParticleFx.SpawnBurst(Vector2.zero, null, config);

            var main = ps.main;
            Assert.IsFalse(main.loop, "한 번 터지고 끝나야 한다(반복 재생 아님)");
            Assert.AreEqual(ParticleSystemStopAction.Destroy, main.stopAction,
                "재생이 끝나면 스스로 정리되어야 한다 (씬에 안 남게)");

            Object.DestroyImmediate(ps.gameObject);
        }

        [Test]
        public void SpawnBurst_EmitsExactlyConfiguredParticleCount_AsOneBurst()
        {
            var config = ParticleBurstConfig.Default;
            config.count = 15;

            var ps = ParticleFx.SpawnBurst(Vector2.zero, null, config);
            var emission = ps.emission;

            Assert.AreEqual(1, emission.burstCount);
            var burst = emission.GetBurst(0);
            Assert.AreEqual(15, burst.count.constant);
            Assert.AreEqual(0f, emission.rateOverTime.constant, "버스트 외에 지속적으로 나오면 안 된다");

            Object.DestroyImmediate(ps.gameObject);
        }

        [Test]
        public void SpawnBurst_HasColorAndSizeOverLifetime_ForFadeOutLook()
        {
            var ps = ParticleFx.SpawnBurst(Vector2.zero, null, ParticleBurstConfig.Default);

            Assert.IsTrue(ps.colorOverLifetime.enabled, "시간에 따라 색이 바뀌어야(그라디언트) 밋밋해 보이지 않는다");
            Assert.IsTrue(ps.sizeOverLifetime.enabled);

            Object.DestroyImmediate(ps.gameObject);
        }

        [Test]
        public void SpawnBurst_AlwaysHasAMaterial_NeverRendersAsMissingShaderPink()
        {
            var ps = ParticleFx.SpawnBurst(Vector2.zero, null, ParticleBurstConfig.Default);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            // EditMode에서 renderer.material(getter)을 부르면 "에디트 모드에서 인스턴스화" 경고가 뜨므로
            // (실제로 씬에 남는 것도 아니고 테스트 목적상 의미 없음) sharedMaterial로 확인한다.
            Assert.IsNotNull(renderer.sharedMaterial, "가산 블렌딩 셰이더를 못 찾아도 기본 머티리얼로 대체되어야 한다");

            Object.DestroyImmediate(ps.gameObject);
        }

        [Test]
        public void SpawnBurst_MaterialUsesTheCircleTexture_NotAFlatQuad()
        {
            // 실제로 겪은 버그: 텍스처를 못 붙이면 알파 컷아웃이 없어서 파티클이 그냥 네모(빌보드
            // 쿼드 원본 모양)로 보인다. mainTexture가 SpriteFactory.Circle과 같은지 반드시 확인한다.
            var ps = ParticleFx.SpawnBurst(Vector2.zero, null, ParticleBurstConfig.Default);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            Assert.AreEqual(SpriteFactory.Circle.texture, renderer.sharedMaterial.mainTexture,
                "파티클이 둥글게 보이려면 원 텍스처가 실제로 적용되어 있어야 한다");

            Object.DestroyImmediate(ps.gameObject);
        }

        [Test]
        public void SpawnBurst_PositionsAtGivenWorldPosition()
        {
            var pos = new Vector2(3f, -1f);
            var ps = ParticleFx.SpawnBurst(pos, null, ParticleBurstConfig.Default);

            Assert.AreEqual(pos.x, ps.transform.position.x, 0.001f);
            Assert.AreEqual(pos.y, ps.transform.position.y, 0.001f);

            Object.DestroyImmediate(ps.gameObject);
        }
    }
}
