namespace CCT
{
    /// <summary>난이도 관련 가안 수치 모음. 플레이 테스트하며 여기서 조정한다.</summary>
    public static class Balance
    {
        // 스폰
        public const float SpawnIntervalStart = 1.2f;
        public const float SpawnIntervalMin = 0.35f;
        public const float SpawnIntervalDecayPerSecond = 0.001f;
        public const int MaxAliveEnemies = 100;
        /// <summary>40(2026-10-12): 유효 영구 해금 총수 N당 일반 스폰 +3%(최대 +60%). 일반 동시 상한도 100→floor(100×배율),
        /// 보스·소환 포함 전체 절대 상한 160(LoadScenario/진상 손님 소환 상한과 같음)은 넘지 않는다.</summary>
        public const int SpawnBonusPercentPerUnlock = 3;
        public const int SpawnBonusPercentMax = 60;
        public const int MaxAliveEnemiesAbsolute = 160;

        /// <summary>화면 가장자리 바깥으로 이 만큼 떨어진 곳에서 걸어들어온다 (벨트스크롤 진입 연출).</summary>
        public const float SpawnEdgeMargin = 1.5f;
        /// <summary>한 번 화면 안으로 들어온 몹은 카메라가 움직여도 이 여백만큼은 항상 화면 안에 남는다
        /// (Enemy.ClampToCameraView) — 화면 가장자리에서 반쯤 잘려 보이거나, 카메라를 못 따라잡는
        /// 느린/제자리 몹이 뒤에 남겨지는 걸 막는다. 스폰 직후(아직 한 번도 화면에 안 들어온 상태)는
        /// 적용하지 않아 SpawnEdgeMargin 진입 연출은 그대로 유지된다.</summary>
        public const float EnemyViewClampMargin = 0.2f;
        /// <summary>양쪽에서 동시에 몰려오는 웨이브 간격(초)과 한쪽당 마리 수.</summary>
        public const float WaveIntervalSeconds = 14f;
        public const int WaveCountPerSide = 4;

        // 사용자 후반 스폰 피드백: 20분 동안 계속 증가(영구 해금 배율과 별개).
        public static float TimeSpawnMultiplier(float seconds) => 1f + UnityEngine.Mathf.Clamp(seconds, 0f, RunTimeLimitSeconds) / 600f;
        public static int TimeWaveCountPerSide(float seconds) => WaveCountPerSide + UnityEngine.Mathf.FloorToInt(UnityEngine.Mathf.Clamp(seconds, 0f, RunTimeLimitSeconds) / 180f);
        public static int TimeAliveCap(float seconds, int permanentCap) => UnityEngine.Mathf.Min(MaxAliveEnemiesAbsolute,
            permanentCap + UnityEngine.Mathf.FloorToInt(UnityEngine.Mathf.Clamp(seconds, 0f, RunTimeLimitSeconds) / 20f));

        /// <summary>몹 체력 배율 = 1 + 런 시간(초) / 이 값.</summary>
        public const float EnemyHpGrowthSeconds = 240f;

        /// <summary>런 제한시간(06:148, "20분 영업 종료"). 이 시각까지 최종보스를 못 잡으면 실패로 종료한다.</summary>
        public const float RunTimeLimitSeconds = 1200f;

        // 플레이어 생존
        public const float PlayerInvulnerableSeconds = 0.8f;
        public const float PlayerRegenPerSecond = 0.5f;
        /// <summary>장판(DOT) 피해 틱 간격 (06:23). BurstZone류와 PlayerZoneDamage가 공유한다.</summary>
        public const float ZoneTickInterval = 0.4f;
        /// <summary>보스 등장 시 1초 "안내" — 이 동안 무적이고 접촉 피해도 없다. 보스 드라이버의
        /// "첫 지연"은 이 1초가 끝난 뒤부터 센다 (06:24).</summary>
        public const float BossArrivalGraceSeconds = 1f;
        /// <summary>카드 선택(레벨업) 시 최대 체력 대비 회복 비율.</summary>
        public const float LevelUpHealFraction = 0.3f;
        /// <summary>중간보스 처치 시 최대 체력 대비 회복 비율.</summary>
        public const float MidBossHealFraction = 0.3f;

        // 재료 게이지 (칵테일): 8 → 11 → 15 → 21 …
        public const float CocktailGaugeStart = 8f;
        public const float CocktailGaugeGrowth = 1.4f;

        // 07 §2 "커피 변경"(2026-10-04 적용): 원두 10개마다 커피 포인트 1, 임계치 증가 없음, 포인트 전부 보존.
        // 보스A 20→2, B 20→2, 강화 40→4 = 런당 8포인트(영구 원두 +20%면 96 → 9포인트·잔량6).
        // 이전의 8→11→15 성장 게이지와 포인트 1개 캡핑은 폐기했다(CoffeeBeanGauge 참고).
        public const int BeanPointThreshold = 10;

        /// <summary>
        /// 벨트스크롤(던파식) 시점: 좌우는 자유, 위아래는 이 "레인" 폭 안에서만 움직인다.
        /// 레인은 화면 정중앙이 아니라 아래쪽에 오도록, 카메라를 레인 중심보다 위에 고정한다.
        /// (2026-09-22 피드백 반영: 레인이 화면 세로 길이의 2/3을 차지하도록, 아래쪽에 치우치게 배치.
        ///  카메라 orthographicSize(6, GameManager.SetupCamera)가 바뀌면 이 두 값도 다시 계산해야 한다:
        ///  DepthHalfWidth = orthographicSize * (2/3), CameraAboveLaneOffset ≈ orthographicSize/4 정도로 아래쪽에 치우치게.)
        /// </summary>
        public const float LaneCenterY = -2.0f;
        public const float DepthHalfWidth = 4.0f;
        public const float DepthMin = LaneCenterY - DepthHalfWidth;
        public const float DepthMax = LaneCenterY + DepthHalfWidth;

        /// <summary>카메라가 레인 중심보다 이만큼 위에 고정되어, 플레이 영역이 화면 아래쪽에 놓인다.</summary>
        public const float CameraAboveLaneOffset = 1.5f;
        public const float CameraFixedY = LaneCenterY + CameraAboveLaneOffset;

        /// <summary>재료 픽업 아이콘도 캔버스 여백 때문에 작아 보여서 시각적으로만 키운다.</summary>
        public const float IngredientIconVisualScale = 1.6f;

        /// <summary>몹 발사체 아이콘도 캔버스 여백 때문에 작아 보여서 시각적으로만 키운다 (히트 판정 반지름은 무관).</summary>
        public const float EnemyProjectileIconVisualScale = 1.8f;

        // 강화 보스(에스프레소 리바이어던)의 "압력 게이지 폭발" 패턴
        public const float PressureExplosionKnockbackSpeed = 6f;
        public const float PressureKnockbackDuration = 0.25f;

        /// <summary>스페셜티(영구 재화) — 강화 보스 처치 시 드랍량. 실제 보너스는 자동 %가 아니라
        /// PermanentSkillTree에서 플레이어가 직접 찍는 노드 비용/효과로 정해진다 (2026-09-22 변경:
        /// "적용된 건지, 어떻게 찍는지 모르겠다"는 피드백으로 자동 % 방식에서 전환).</summary>
        public const int SpecialtyDropAmount = 3;

        public static float EnemyHpScale(float runTimeSeconds) => 1f + runTimeSeconds / EnemyHpGrowthSeconds;
    }
}
