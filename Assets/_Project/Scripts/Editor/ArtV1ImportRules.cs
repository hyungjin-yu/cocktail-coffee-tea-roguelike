using System;
using System.Collections.Generic;

namespace CCT.Editor
{
    public enum ArtV1PivotSource
    {
        /// <summary>04 §7 몸체 고정 피벗 (0.5, 0.125).</summary>
        BodySpec,
        /// <summary>VFX/UI/배경: manifest.pivotNormalized.</summary>
        Manifest,
        /// <summary>매니페스트나 피벗이 없어 중앙 (0.5, 0.5)으로 둠 — 검수 에러와 함께 보고된다.</summary>
        CenterFallback,
    }

    /// <summary>ArtV1ImportSettings가 TextureImporter에 그대로 옮겨 적는 값.</summary>
    public sealed class ArtV1ImportPlan
    {
        public bool Applies;
        public string AssetId;
        public ArtV1Kind Kind;
        public int PixelsPerUnit = ArtV1Spec.PixelsPerUnit;
        public float PivotX = 0.5f;
        public float PivotY = 0.5f;
        public ArtV1PivotSource PivotSource = ArtV1PivotSource.CenterFallback;
        /// <summary>UI 9-slice [좌, 하, 우, 상] 픽셀. 없으면 null.</summary>
        public int[] BorderPx;
        public bool IsReady;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Assets/_Project/Art/v1 PNG 임포트 규칙(04 §9). Unity API 없이 결정만 한다 — 테스트 가능하게.
    /// 기존 Resources/Icons 경로(IconImportSettings)는 여기 해당하지 않는다.
    /// 고정값: Single Sprite, 128 PPU, Point, mipmap off, 무압축, 알파 유지(ArtV1ImportSettings가 적용).
    /// 피벗: 몸체는 항상 (0.5, 0.125). VFX/UI/배경은 매니페스트 값, 없으면 중앙 + 에러.
    /// </summary>
    public static class ArtV1ImportRules
    {
        public const string Root = "Assets/_Project/Art/v1/";
        /// <summary>기획 20: 사용자가 1차 실행본에만 명시 허용한 미완성 소스(예: 바텐더 v5). 임포트 설정은 같지만
        /// ready가 아니어도 firstpass-acceptance.json이 있으면 에러로 보지 않는다. 출시 검사(BuildRelease)는 이 폴더를 보지 않는다.</summary>
        public const string FirstPassRoot = "Assets/_Project/Art/firstpass/";

        static string RootOf(string normalizedPath)
        {
            if (normalizedPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) return Root;
            if (normalizedPath.StartsWith(FirstPassRoot, StringComparison.OrdinalIgnoreCase)) return FirstPassRoot;
            return null;
        }

        public static bool IsArtV1Png(string assetPath)
        {
            string p = Normalize(assetPath);
            return RootOf(p) != null && p.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsFirstPassPath(string assetPath) =>
            Normalize(assetPath).StartsWith(FirstPassRoot, StringComparison.OrdinalIgnoreCase);

        /// <summary>Assets/_Project/Art/v1(또는 firstpass)/&lt;assetId&gt;/... 에서 assetId. 루트 바로 아래 파일이면 null.</summary>
        public static string AssetIdFromPath(string assetPath)
        {
            string p = Normalize(assetPath);
            string root = RootOf(p);
            if (root == null) return null;
            string rest = p.Substring(root.Length);
            int slash = rest.IndexOf('/');
            return slash > 0 ? rest.Substring(0, slash) : null;
        }

        public static string ManifestPathFor(string assetPath)
        {
            string id = AssetIdFromPath(assetPath);
            return id == null ? null : RootOf(Normalize(assetPath)) + id + "/" + ArtV1Validator.ManifestFileName;
        }

        /// <summary>firstpass 폴더에 사용자 허용 기록이 있는지(파일 존재만 — 내용 검사는 통합기가 한다).</summary>
        public static bool HasFirstPassAcceptance(string assetPath)
        {
            string id = AssetIdFromPath(assetPath);
            return id != null && IsFirstPassPath(assetPath) &&
                   System.IO.File.Exists(FirstPassRoot + id + "/firstpass-acceptance.json");
        }

        /// <param name="manifest">없으면 null.</param>
        /// <param name="manifestError">JSON 파싱 실패 사유(있으면).</param>
        public static ArtV1ImportPlan Resolve(string assetPath, ArtV1Manifest manifest, string manifestError = null)
        {
            var plan = new ArtV1ImportPlan();
            if (!IsArtV1Png(assetPath)) return plan;

            plan.Applies = true;
            plan.AssetId = AssetIdFromPath(assetPath);
            plan.Kind = ArtV1Spec.InferKind(plan.AssetId);

            string fileName = System.IO.Path.GetFileName(Normalize(assetPath));
            if (string.Equals(fileName, ArtV1Validator.PreviewFileName, StringComparison.OrdinalIgnoreCase))
                plan.Warnings.Add("preview.png는 게임 임포트 대상이 아니다 (04 §7) — Art/v1에서 빼는 것을 권장");

            if (plan.AssetId == null)
            {
                plan.Errors.Add("Art/v1 바로 아래 PNG — Art/v1/<assetId>/ 폴더 안에 둬야 매니페스트를 찾을 수 있다");
                ApplyBodyPivotIfKnownBody(plan);
                return plan;
            }

            if (manifest == null)
            {
                plan.Errors.Add(manifestError != null
                    ? $"manifest.json 파싱 실패: {manifestError}"
                    : "manifest.json이 없다 — ready 확인 불가");
                ApplyBodyPivotIfKnownBody(plan);
                return plan;
            }

            plan.IsReady = manifest.IsReady;
            if (!plan.IsReady && HasFirstPassAcceptance(assetPath))
                plan.Warnings.Add($"status {manifest.Status ?? "없음"} — 1차 실행본 전용 사용자 허용(firstpass-acceptance.json). 출시 ready 아님");
            else if (!plan.IsReady)
                plan.Errors.Add($"status가 ready가 아니다({manifest.Status ?? "없음"}) — needs_fix 에셋은 Art/v1에 복사하지 않는다 (04 §9)");
            if (manifest.AssetId != null && manifest.AssetId != plan.AssetId)
                plan.Errors.Add($"manifest assetId \"{manifest.AssetId}\"가 폴더 \"{plan.AssetId}\"와 다르다");

            plan.Kind = manifest.ResolveKind();
            if (manifest.PixelsPerUnit.HasValue && Math.Abs(manifest.PixelsPerUnit.Value - ArtV1Spec.PixelsPerUnit) > 0.001f)
                plan.Errors.Add($"manifest pixelsPerUnit {manifest.PixelsPerUnit.Value} — 임포트는 {ArtV1Spec.PixelsPerUnit}로 고정한다");

            if (plan.Kind == ArtV1Kind.Body)
            {
                SetBodyPivot(plan);
                var p = manifest.PivotNormalized;
                if (p != null && (Math.Abs(p[0] - ArtV1Spec.BodyPivotX) > 0.0005f || Math.Abs(p[1] - ArtV1Spec.BodyPivotY) > 0.0005f))
                    plan.Warnings.Add($"몸체 manifest 피벗 [{p[0]}, {p[1]}]을 무시하고 04 기준 [0.5, 0.125]를 적용했다");
            }
            else if (manifest.PivotNormalized != null)
            {
                plan.PivotX = manifest.PivotNormalized[0];
                plan.PivotY = manifest.PivotNormalized[1];
                plan.PivotSource = ArtV1PivotSource.Manifest;
            }
            else
            {
                plan.Errors.Add("VFX/UI 매니페스트에 pivotNormalized가 없어 중앙(0.5, 0.5)으로 두었다 — 매니페스트에 적어야 한다");
            }

            if (manifest.SpriteBorderPx != null)
            {
                if (plan.Kind == ArtV1Kind.Ui) plan.BorderPx = (int[])manifest.SpriteBorderPx.Clone();
                else plan.Warnings.Add("spriteBorderPx는 UI 9-slice에만 적용한다 — 무시했다");
            }
            return plan;
        }

        static void ApplyBodyPivotIfKnownBody(ArtV1ImportPlan plan)
        {
            if (plan.Kind == ArtV1Kind.Body) SetBodyPivot(plan);
        }

        static void SetBodyPivot(ArtV1ImportPlan plan)
        {
            plan.PivotX = ArtV1Spec.BodyPivotX;
            plan.PivotY = ArtV1Spec.BodyPivotY;
            plan.PivotSource = ArtV1PivotSource.BodySpec;
        }

        static string Normalize(string path) => (path ?? "").Replace('\\', '/');
    }
}
