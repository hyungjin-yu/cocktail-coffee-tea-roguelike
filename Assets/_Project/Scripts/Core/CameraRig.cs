using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 벨트스크롤(던파식) 카메라: 플레이어를 좌우로만 따라가고 위아래는 고정.
    /// 바닥 타일도 좌우로만 스냅 이동해 무한 스크롤처럼 보이게 한다.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        const float TileSize = 2f;

        Camera cam;
        Transform target;
        Transform background;
        float fixedCamY;
        float fixedBackgroundY;

        public void Init(Camera cam, Transform target, Transform background)
        {
            this.cam = cam;
            this.target = target;
            this.background = background;
            fixedCamY = cam.transform.position.y;
            fixedBackgroundY = background != null ? background.position.y : 0f;
            Snap();
        }

        void LateUpdate()
        {
            if (cam == null || target == null) return;

            float t = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            float x = Mathf.Lerp(cam.transform.position.x, target.position.x, t);
            cam.transform.position = new Vector3(x, fixedCamY, -10f);
            SnapBackground(x);
        }

        void Snap()
        {
            if (cam == null || target == null) return;
            cam.transform.position = new Vector3(target.position.x, fixedCamY, -10f);
            SnapBackground(target.position.x);
        }

        void SnapBackground(float camX)
        {
            if (background == null) return;
            background.position = new Vector3(Mathf.Round(camX / TileSize) * TileSize, fixedBackgroundY, 0f);
        }
    }
}
