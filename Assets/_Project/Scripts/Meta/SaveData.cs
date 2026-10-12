using System.Collections.Generic;

namespace CCT
{
    /// <summary>
    /// 런 종료 시에도 초기화되지 않는 영구 데이터 — 08 §5 save.json 스키마 v2. JsonUtility로 그대로
    /// 직렬화되도록 필드만 둔다. 읽기/이전/검증은 SaveSystem, 런 수명주기 갱신은 PermanentProgress가 맡는다.
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        /// <summary>08:138 세이브 스키마 버전(현재 2). 버전 필드가 없던 v0 파일, "version"=1을 쓰던 v1 파일은
        /// SaveSystem이 원본 텍스트로 판별해 이전한다 — 이 필드를 손으로 올리지 않는다.</summary>
        public int schemaVersion;

        /// <summary>08:138 성공한 저장마다 1씩 증가. 저장이 실패하면 메모리 값도 그대로 둔다.</summary>
        public int saveRevision;

        /// <summary>누적으로 획득한 스페셜티 총량 (쓴 만큼 줄어들지 않음 — "총 획득", 07:151). 잔액은
        /// 이 값에서 유효한 영구 노드 비용 합을 뺀 값이다(PermanentSkillTree.SpentPoints).</summary>
        public int specialtyPoints;

        /// <summary>스페셜티로 해금한 영구 강화 노드 id 목록. 알 수 없는 id·선행이 빠진 옛 기록도 지우지 않고
        /// 보존한다(효과/비용 계산에서만 제외, 08:169~170).</summary>
        public List<string> unlockedPermanentSkillIds = new List<string>();

        /// <summary>지금까지 한 번이라도 획득한 카드 id 목록 (카드 도감).</summary>
        public List<string> discoveredCardIds = new List<string>();

        /// <summary>08:140 지금까지 한 번이라도 해금한 커피 노드 id 목록 (도감 "최초 획득" 표시).</summary>
        public List<string> discoveredSkillIds = new List<string>();

        /// <summary>지금까지 한 번이라도 나온 재료 품종 id 목록 (MaterialVariety).</summary>
        public List<string> discoveredMaterialVarietyIds = new List<string>();

        /// <summary>재료별 누적 수집량. 런 중에는 체크포인트마다 "마지막 반영량과의 차이"만 더한다(08:152).</summary>
        public int cocktailIngredientsCollected;
        public int teaLeavesCollected;
        public int coffeeBeansCollected;

        /// <summary>08:142 런 통계. 시작 시 totalRuns+1, 종료 시 결과 하나만 +1(클리어/실패(사망·시간초과)/포기).</summary>
        public int totalRuns;
        public int clearedRuns;
        public int failedRuns;
        public int abandonedRuns;

        /// <summary>08:143 클리어 없음 0, 이후 최단 클리어 시간(초).</summary>
        public float bestClearSeconds;

        /// <summary>08:144 진행 중 런의 중복 집계 방지/강제종료 판정 기록. 없으면 null — 파일에는 JSON null로
        /// 쓴다(SaveSystem). 런 재개용(HP/적/카드/좌표)이 아니다(08:148).</summary>
        public ActiveRunRecord activeRun;

        /// <summary>08:145 직전에 종료 집계를 마친 runId. 같은 runId 종료 집계는 다시 하지 않는다.</summary>
        public string lastCommittedRunId = "";
    }

    /// <summary>08:144 activeRun 레코드. cocktail/tea/coffee/specialtyAwarded는 "이 런에서 전역 합계에 이미
    /// 반영한 양"이다 — 같은 저장 스냅샷에 전역 합계와 함께 기록된다.</summary>
    [System.Serializable]
    public class ActiveRunRecord
    {
        public string runId = "";
        public int seed;
        public string startedUtc = "";
        public float checkpointGameSeconds;
        public int cocktail;
        public int tea;
        public int coffee;
        public int specialtyAwarded;
        public bool resultCommitted;

        public ActiveRunRecord Clone() => (ActiveRunRecord)MemberwiseClone();
    }
}
