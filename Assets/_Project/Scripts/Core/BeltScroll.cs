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
    }
}
