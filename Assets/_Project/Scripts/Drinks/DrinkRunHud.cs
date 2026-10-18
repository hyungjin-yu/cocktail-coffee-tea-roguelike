using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 69 런 중 표시: 준비 음료 칩(이름·버프·보존 상태 n/5킬)과 보스 음료 재료 확보 알림 + 전용 픽업 연출.
    /// 지급은 보스 사망 순간 DrinkLab에 이미 확정 기록된다 — 픽업 연출은 플레이어에게 날아가는 표시일 뿐 놓칠 수 없다.
    /// </summary>
    public class DrinkRunHud : MonoBehaviour
    {
        GameManager game;
        GameObject chip;
        Text chipTitle, chipBuff, chipState;
        Image chipSwatch;
        GameObject toast;
        Text toastText;
        float toastRemaining;
        int shownKills = -1;
        bool shownLocked;

        public string ChipStateText => chipState != null ? chipState.text : "";
        public bool ChipVisible => chip != null && chip.activeSelf;
        public string ToastText => toast != null && toast.activeSelf ? toastText.text : "";

        public static DrinkRunHud Create(Transform canvas, GameManager game)
        {
            var root = UiFactory.Stretch("DrinkHud", canvas);
            var view = root.gameObject.AddComponent<DrinkRunHud>();
            view.game = game;

            var chipRt = UiFactory.Place("DrinkChip", root, new Vector2(1f, 1f), new Vector2(-30f, -196f), new Vector2(500f, 96f));
            UiFactory.AddImage(chipRt, new Color(0.08f, 0.06f, 0.1f, 0.86f)).raycastTarget = false;
            view.chipSwatch = UiFactory.AddImage(UiFactory.Place("Swatch", chipRt, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(44f, 72f)), Color.clear);
            view.chipSwatch.raycastTarget = false;
            view.chipTitle = UiFactory.AddText(UiFactory.Place("Title", chipRt, new Vector2(0f, 1f), new Vector2(68f, -6f), new Vector2(420f, 30f)), "", 22, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.55f));
            view.chipBuff = UiFactory.AddText(UiFactory.Place("Buff", chipRt, new Vector2(0f, 1f), new Vector2(68f, -36f), new Vector2(420f, 28f)), "", 19, TextAnchor.MiddleLeft, Color.white);
            view.chipState = UiFactory.AddText(UiFactory.Place("State", chipRt, new Vector2(0f, 1f), new Vector2(68f, -64f), new Vector2(420f, 26f)), "", 18, TextAnchor.MiddleLeft, new Color(0.75f, 0.9f, 1f));
            view.chip = chipRt.gameObject;

            var toastRt = UiFactory.Place("DrinkAwardToast", root, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 70f));
            UiFactory.AddImage(toastRt, new Color(0.14f, 0.09f, 0.04f, 0.92f)).raycastTarget = false;
            view.toastText = UiFactory.AddText(UiFactory.Stretch("Text", toastRt), "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            view.toast = toastRt.gameObject;
            view.toast.SetActive(false);
            view.Refresh(true);
            if (!string.IsNullOrEmpty(DrinkLab.LastBeginError))
            {
                // 69 입장 음용 기록 저장 실패: 버프 없이 시작했음을 알린다(음료는 Ready로 남아 있음).
                view.toastText.text = DrinkLab.LastBeginError;
                view.toast.SetActive(true);
                view.toastRemaining = 6f;
            }
            return view;
        }

        public void RefreshForTests() => Refresh(false);

        void Update()
        {
            Refresh(false);
            if (toastRemaining > 0f)
            {
                toastRemaining -= Time.unscaledDeltaTime;
                if (toastRemaining <= 0f) toast.SetActive(false);
            }
        }

        void Refresh(bool force)
        {
            if (game == null) return;
            var drink = DrinkLab.DrinkInRun(game.RunId);
            bool has = drink != null && game.DrinkBuff != null;
            if (chip.activeSelf != has) chip.SetActive(has);
            if (!has) return;
            bool locked = drink.locked || game.Kills >= DrinkLab.PreserveKillLimit;
            if (!force && shownKills == game.Kills && shownLocked == locked) return;
            shownKills = game.Kills;
            shownLocked = locked;
            chipTitle.text = $"준비 음료 · {drink.recipe.name}";
            chipBuff.text = game.DrinkBuff.Describe();
            var look = DrinkCalculator.Look(drink.recipe);
            var c = look.color; c.a = Mathf.Max(0.55f, c.a);
            chipSwatch.color = c;
            chipState.text = locked ? "이번 판에 사용됨 — 판이 끝나면 사라집니다"
                : $"지금 포기하면 음료 보존 (처치 {Mathf.Min(game.Kills, DrinkLab.PreserveKillLimit)}/{DrinkLab.PreserveKillLimit})";
        }

        /// <summary>보스 처치: 전용 픽업 연출 + 알림. 지급은 이미 확정.</summary>
        public void OnBossAward(Vector2 bossPosition, List<DrinkAmount> items)
        {
            toastText.text = $"보스 음료 재료 확보 — {DrinkLab.Describe(items)} (판이 끝나면 창고에 저장)";
            toast.SetActive(true);
            toastRemaining = 4.5f;
            if (game != null && game.Player != null)
                DrinkMaterialPickup.Spawn(game.transform, bossPosition, game.Player.transform, game.Job);
        }
    }

    /// <summary>보스 음료 재료 전용 픽업 표시(월드). 플레이어에게 날아가 사라진다. 판정·지급 없음(이미 확정).</summary>
    public class DrinkMaterialPickup : MonoBehaviour
    {
        Transform target;
        Vector3 start;
        float t;
        const float Duration = 1.1f;

        public static DrinkMaterialPickup Spawn(Transform parent, Vector2 from, Transform target, Job job)
        {
            var go = new GameObject("DrinkMaterialPickup");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DrinkArt.Sprite("dl_boss_material_drop") ?? IngredientPickup.DropArt(job == Job.TeaSommelier ? IngredientType.Tea : IngredientType.Cocktail);
            sr.sortingOrder = 50;
            go.transform.localScale = Vector3.one * 1.6f;
            var p = go.AddComponent<DrinkMaterialPickup>();
            p.target = target;
            p.start = from;
            return p;
        }

        void Update()
        {
            // 결과 화면(timeScale 0)에서도 끝까지 날아간다.
            t += Time.unscaledDeltaTime / Duration;
            if (target == null || t >= 1f) { Destroy(gameObject); return; }
            float e = t * t * (3f - 2f * t);
            var arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.4f;
            transform.position = Vector3.Lerp(start, target.position, e) + arc;
            transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 0.6f, e);
        }
    }
}
