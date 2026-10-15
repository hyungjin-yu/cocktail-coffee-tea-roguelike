using UnityEngine;

namespace CCT
{
    /// <summary>벨트스크롤(던파식) 시점 공용 헬퍼: 화면 좌/우 바깥 스폰 위치 계산.</summary>
    public static class BeltScroll
    {
        /// <summary>카메라 기준 좌(side=-1)/우(side=+1) 바깥, 레인(깊이) 폭 안의 랜덤 위치.</summary>
        public static Vector2 EdgePosition(Camera cam, float side, float margin)
        {
            float halfWidth = cam != null ? cam.orthographicSize * cam.aspect : 10f;
            float x = cam != null ? cam.transform.position.x : 0f;
            float depth = Random.Range(Balance.DepthMin, Balance.DepthMax);
            return new Vector2(x + side * (halfWidth + margin), depth);
        }

        /// <summary>57 P0-1: EdgePosition과 같지만 유한 전투 구역(BattleArena) 안에서만 나온다. 벽 쪽이 막히면 반대쪽 화면 밖.
        /// 실제 등장 쪽을 usedSide로 돌려줘 안내 문구가 실제와 맞게 한다.</summary>
        public static Vector2 ArenaEdgePosition(Camera cam, float side, float margin, float radius, out float usedSide)
        {
            float halfWidth = cam != null ? cam.orthographicSize * cam.aspect : 10f;
            float camX = cam != null ? cam.transform.position.x : 0f;
            float depth = Random.Range(Balance.DepthMin, Balance.DepthMax);
            return new Vector2(BattleArena.SpawnX(camX, halfWidth, side, margin, radius, out usedSide), depth);
        }
    }
}
