using System.Collections;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 중간보스 B: 드립 서버 스펙터 패턴 ([[02_몹 디자인 - 커피 & 중간보스]] 참고, 번갈아 사용).
    /// "드립 웨이브" — 플레이어 주변에 뜨거운 커피 장판을 순서대로 떨어뜨려 안전지대를 찾아 움직이게 만든다.
    /// "투명화" — 잠시 무적 상태로 사라졌다가 플레이어 근처에 재등장하며 소형 장판을 하나 남긴다.
    /// </summary>
    public class DripSpecterDriver : MonoBehaviour
    {
        const float FirstDelay = 2.5f;
        const float Cooldown = 6f;

        const int DripWaveCount = 4;
        const float DripWaveZoneRadius = 1.3f;
        const float DripWaveDamagePerSecond = 9f;
        const float DripWaveZoneSeconds = 2f;
        const float DripWaveStagger = 0.4f;
        const float DripWaveSpread = 1.5f;

        const float InvisibleSeconds = 1.2f;
        const float TeleportDistance = 3.5f;
        const float ReappearZoneRadius = 1.1f;
        const float ReappearDamagePerSecond = 8f;
        const float ReappearZoneSeconds = 1.8f;

        Enemy enemy;
        GameManager game;
        float timer = FirstDelay;
        int patternIndex;

        public void Init(Enemy enemy, GameManager game)
        {
            this.enemy = enemy;
            this.game = game;
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead || game == null || game.State != GameState.Playing) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            timer = Cooldown;
            if (patternIndex++ % 2 == 0) StartCoroutine(DripWave());
            else StartCoroutine(Invisibility());
        }

        IEnumerator DripWave()
        {
            game.Hud.ShowBanner("드립 웨이브", "바닥에 뜨거운 커피가 순서대로 떨어진다!", 2f);

            for (int i = 0; i < DripWaveCount; i++)
            {
                if (enemy == null || enemy.IsDead || game.Player == null || game.Player.IsDead) yield break;

                Vector2 pos = game.Player.Position + Random.insideUnitCircle * DripWaveSpread;
                pos.y = Mathf.Clamp(pos.y, Balance.DepthMin, Balance.DepthMax);
                BurstZone.Spawn(pos, DripWaveZoneRadius, DripWaveDamagePerSecond, DripWaveZoneSeconds,
                    game.Player, enemy.transform.parent);

                yield return new WaitForSeconds(DripWaveStagger);
            }
        }

        IEnumerator Invisibility()
        {
            if (enemy == null || enemy.IsDead) yield break;

            game.Hud.ShowBanner("투명화", "스펙터가 잠시 사라진다...", 1.5f);
            enemy.Invulnerable = true;
            var normal = enemy.NormalColor;
            enemy.BaseColor = new Color(normal.r, normal.g, normal.b, 0.25f);

            yield return new WaitForSeconds(InvisibleSeconds);

            if (enemy == null || enemy.IsDead) yield break;

            enemy.BaseColor = normal;
            enemy.Invulnerable = false;

            if (game.Player == null || game.Player.IsDead) yield break;

            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector2 target = game.Player.Position + dir * TeleportDistance;
            target.y = Mathf.Clamp(target.y, Balance.DepthMin, Balance.DepthMax);
            enemy.transform.position = target;
            BurstZone.Spawn(target, ReappearZoneRadius, ReappearDamagePerSecond, ReappearZoneSeconds,
                game.Player, enemy.transform.parent);
        }
    }
}
