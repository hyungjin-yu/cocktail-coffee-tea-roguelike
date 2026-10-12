using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CCT
{
    /// <summary>스토어프런트별 소유 기록 1건. tokenHash는 검증된 구매 토큰의 SHA-256(원문 토큰은 저장하지 않음).</summary>
    [Serializable]
    public class OwnedSkin
    {
        public string storefront;
        public string productId;
        public string tokenHash;
    }

    /// <summary>
    /// commerce.json(save.json과 같은 폴더). 소유권 캐시 — 스토어 기록 + 서버 검증이 근거이고 이 파일은 그 결과를 기억한다.
    /// 소유는 스토어프런트별(googleplay/steam)로 분리: Google 구매가 Steam에서 열리지 않는다.
    /// installationId: 이 설치본의 임의 GUID(하드웨어 ID 아님).
    /// </summary>
    [Serializable]
    public class CommerceState
    {
        public int schemaVersion = 1;
        public string installationId = "";
        public List<OwnedSkin> owned = new List<OwnedSkin>();
        /// <summary>장착 스킨 productId 목록(직업당 최대 1개).</summary>
        public List<string> equipped = new List<string>();

        public bool IsOwned(string storefront, string productId)
        {
            foreach (var o in owned)
                if (o != null && o.storefront == storefront && o.productId == productId) return true;
            return false;
        }

        public bool HasToken(string storefront, string tokenHash)
        {
            if (string.IsNullOrEmpty(tokenHash)) return false;
            foreach (var o in owned)
                if (o != null && o.storefront == storefront && o.tokenHash == tokenHash) return true;
            return false;
        }

        public void Grant(string storefront, string productId, string tokenHash)
        {
            foreach (var o in owned)
            {
                if (o != null && o.storefront == storefront && o.productId == productId)
                {
                    if (!string.IsNullOrEmpty(tokenHash)) o.tokenHash = tokenHash;
                    return;
                }
            }
            owned.Add(new OwnedSkin { storefront = storefront, productId = productId, tokenHash = tokenHash });
        }

        public string EquippedFor(Job job)
        {
            foreach (var id in equipped)
            {
                var def = SkinCatalog.Find(id);
                if (def != null && def.job == job) return id;
            }
            return null;
        }

        public void SetEquipped(Job job, string productIdOrNull)
        {
            equipped.RemoveAll(id => { var d = SkinCatalog.Find(id); return d == null || d.job == job; });
            if (!string.IsNullOrEmpty(productIdOrNull)) equipped.Add(productIdOrNull);
        }

        public CommerceState Clone() => JsonUtility.FromJson<CommerceState>(JsonUtility.ToJson(this));

        public void Normalize()
        {
            if (owned == null) owned = new List<OwnedSkin>();
            if (equipped == null) equipped = new List<string>();
            owned.RemoveAll(o => o == null || string.IsNullOrEmpty(o.storefront) || !SkinCatalog.IsKnown(o.productId));
            equipped.RemoveAll(id => !SkinCatalog.IsKnown(id));
            if (string.IsNullOrEmpty(installationId)) installationId = Guid.NewGuid().ToString("N");
        }

        public static string HashToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    public interface ICommercePersister
    {
        CommerceState Load();
        bool Save(CommerceState state);
    }

    /// <summary>
    /// 원자적 저장(SaveSystem과 같은 방식): .tmp 완전 쓰기/flush → 재읽기 검증 → File.Replace(.bak 보존) 또는 Move.
    /// 경로 기본값은 PermanentProgress 저장 폴더(-cct-data-dir/테스트 격리를 그대로 따른다)의 commerce.json.
    /// </summary>
    public sealed class FileCommercePersister : ICommercePersister
    {
        public const string FileName = "commerce.json";
        readonly Func<string> pathProvider;

        public FileCommercePersister(Func<string> pathProvider) => this.pathProvider = pathProvider;
        public FileCommercePersister(string path) : this(() => path) { }

        public static string DefaultPath()
        {
            var dir = Path.GetDirectoryName(PermanentProgress.ActiveSavePath);
            return string.IsNullOrEmpty(dir) ? FileName : Path.Combine(dir, FileName);
        }

        public CommerceState Load()
        {
            var path = pathProvider();
            var state = TryRead(path) ?? TryRead(path + ".bak");
            if (state == null)
            {
                state = new CommerceState();
                if (File.Exists(path)) Debug.LogWarning($"[Commerce] {FileName} 읽기 실패 — 새 상태로 시작(원본은 그대로 둠)");
            }
            state.Normalize();
            return state;
        }

        static CommerceState TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path, Encoding.UTF8);
                if (!JsonSyntax.IsCompleteObject(json)) return null;
                return JsonUtility.FromJson<CommerceState>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commerce] {FileName} 읽기 실패: {e.GetType().Name}");
                return null;
            }
        }

        public bool Save(CommerceState state)
        {
            if (state == null) return false;
            var path = pathProvider();
            var tmp = path + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonUtility.ToJson(state, true);
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (TryRead(tmp) == null) throw new IOException("commerce.tmp 검증 실패");
                if (File.Exists(path)) File.Replace(tmp, path, path + ".bak", true);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commerce] {FileName} 저장 실패: {e.GetType().Name}");
                return false;
            }
        }
    }
}
