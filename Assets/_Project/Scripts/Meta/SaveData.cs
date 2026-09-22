using System.Collections.Generic;

namespace CCT
{
    /// <summary>
    /// 런 종료 시에도 초기화되지 않는 영구 데이터 (기획서 11장, 13장). JsonUtility로 그대로 직렬화되도록
    /// 필드만 추가하면 된다.
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        /// <summary>누적으로 획득한 스페셜티 총량 (쓴 만큼 줄어들지 않음 — "총 획득"). 실제로 쓸 수 있는
        /// 잔여량은 이 값에서 unlockedPermanentSkillIds에 해당하는 비용 합을 뺀 값이다.</summary>
        public int specialtyPoints;

        /// <summary>스페셜티로 직접 해금한 영구 강화 노드 id 목록 (PermanentSkillTree).</summary>
        public List<string> unlockedPermanentSkillIds = new List<string>();

        /// <summary>지금까지 한 번이라도 획득한 카드 id 목록 (카드 도감, CardCodexView). 런이 끝나도 유지된다.</summary>
        public List<string> discoveredCardIds = new List<string>();

        /// <summary>재료별 누적 수집량 (도감 표시용). 런 종료 시 그 런에서 모은 양만큼 더해진다 — 픽업마다
        /// 저장하면 I/O가 너무 잦아서 런 단위로만 반영한다.</summary>
        public int cocktailIngredientsCollected;
        public int teaLeavesCollected;
        public int coffeeBeansCollected;
    }
}
