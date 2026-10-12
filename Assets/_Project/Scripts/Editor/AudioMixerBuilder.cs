using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace CCT.Editor
{
    /// <summary>09:71 "Unity AudioMixer의 Master/Music/SFX 그룹 분리"를 실제 .mixer 에셋으로 만든다.
    ///
    /// UnityEditor.Audio.AudioMixerController/AudioMixerGroupController는 internal 클래스라 공식 public
    /// API가 없지만(그래서 이전 초안은 코드 레벨 AudioBus로 대체했었다), 리플렉션으로 실제 멤버를
    /// 확인해보니(6000.3.25f1) CreateMixerControllerAtPath/CreateNewGroup/AddChildToParent/masterGroup 등
    /// 핵심 메서드 자체는 전부 public이다 — internal인 건 "클래스 선언"뿐이라 리플렉션으로 충분히 안전하게
    /// 호출 가능하다. 이 스크립트가 실제 Master(루트)/Music/SFX 3그룹 짜리 .mixer를 만든다.
    ///
    /// 볼륨 실시간 제어(마스터/BGM/SFX 슬라이더, Pause 덕킹)는 여전히 AudioBus가 AudioSource.volume에
    /// 직접 곱하는 방식으로 맡는다 — 이 믹서는 "그룹 분리·라우팅"만 담당하고 그룹 페이더는 0dB(단위 이득)로
    /// 둬서 AudioBus가 계산한 값을 그대로 통과시킨다. AudioService가 각 AudioSource.outputAudioMixerGroup을
    /// 이 믹서의 Music/SFX 그룹으로 연결한다(LoadClip 근처가 아니라 BuildVoicePool/MakeMusicSource에서).</summary>
    public static class AudioMixerBuilder
    {
        public const string MixerResourcePath = "Audio/CCTMixer"; // Resources.Load<AudioMixer> 런타임 경로
        const string MixerAssetPath = "Assets/_Project/Resources/Audio/CCTMixer.mixer";

        [MenuItem("CCT/Build Audio Mixer (Master Music SFX)")]
        public static void Build()
        {
            try
            {
                BuildInternal();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CCT] AudioMixer 생성 실패: {e}");
                throw;
            }
        }

        static void BuildInternal()
        {
            var asm = typeof(UnityEditor.Editor).Assembly;
            var controllerType = asm.GetType("UnityEditor.Audio.AudioMixerController");
            if (controllerType == null)
                throw new InvalidOperationException("UnityEditor.Audio.AudioMixerController 타입을 찾을 수 없음(Unity 버전 차이)");

            var dir = Path.GetDirectoryName(MixerAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            const BindingFlags PubInst = BindingFlags.Public | BindingFlags.Instance;
            const BindingFlags PubStatic = BindingFlags.Public | BindingFlags.Static;

            var createAtPath = controllerType.GetMethod("CreateMixerControllerAtPath", PubStatic);
            object controller = createAtPath.Invoke(null, new object[] { MixerAssetPath });
            if (controller == null) throw new InvalidOperationException("CreateMixerControllerAtPath가 null 반환");

            var masterGroupProp = controllerType.GetProperty("masterGroup", PubInst);
            object master = masterGroupProp.GetValue(controller);

            var createGroup = controllerType.GetMethod("CreateNewGroup", PubInst);
            var addChildToParent = controllerType.GetMethod("AddChildToParent", PubInst);
            var addGroupToView = controllerType.GetMethod("AddGroupToCurrentView", PubInst);

            object music = EnsureChildGroup(controllerType, controller, master, createGroup, addChildToParent, addGroupToView, "Music");
            object sfx = EnsureChildGroup(controllerType, controller, master, createGroup, addChildToParent, addGroupToView, "SFX");

            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 실제로 저장된 에셋을 공개 API(AudioMixer/FindMatchingGroups)로 다시 읽어 검증한다 —
            // "리플렉션 호출이 예외 없이 끝났다"와 "실제로 쓸 수 있는 그룹이 생겼다"는 다른 문제라서.
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            if (mixer == null) throw new InvalidOperationException("저장된 .mixer를 AudioMixer로 다시 로드하지 못함");
            var musicGroups = mixer.FindMatchingGroups("Music");
            var sfxGroups = mixer.FindMatchingGroups("SFX");
            if (musicGroups.Length == 0 || sfxGroups.Length == 0)
                throw new InvalidOperationException($"공개 API로 그룹을 못 찾음: Music={musicGroups.Length}, SFX={sfxGroups.Length}");

            Debug.Log($"[CCT] {MixerAssetPath} 생성/검증 완료 — Master(루트)/Music({musicGroups.Length})/SFX({sfxGroups.Length}). " +
                      $"Resources.Load<AudioMixer>(\"{MixerResourcePath}\")로 런타임 로드 가능.");
        }

        static object EnsureChildGroup(Type controllerType, object controller, object master,
            MethodInfo createGroup, MethodInfo addChildToParent, MethodInfo addGroupToView, string name)
        {
            var childrenProp = controllerType.GetProperty("children", BindingFlags.Public | BindingFlags.Instance)
                ?? master.GetType().GetProperty("children", BindingFlags.Public | BindingFlags.Instance);
            // AudioMixerGroupController.children — master 쪽 타입에서 가져온다(컨트롤러가 아니라 그룹 프로퍼티).
            var groupType = master.GetType();
            var groupChildrenProp = groupType.GetProperty("children", BindingFlags.Public | BindingFlags.Instance);

            object existing = FindChildByName(groupChildrenProp, master, name);
            if (existing != null) return existing;

            object created = createGroup.Invoke(controller, new object[] { name, true });
            var already = FindChildByName(groupChildrenProp, master, name);
            if (already == null) addChildToParent.Invoke(controller, new object[] { created, master });
            // AddGroupToCurrentView는 배치모드(믹서 창을 연 적 없어 views가 비어있음)에서
            // IndexOutOfRangeException을 던진다 — 순수 에디터 UI 편의 기능(뷰에 표시)일 뿐 그룹
            // 생성·부모연결·라우팅 기능과 무관해서 생략한다. 그룹은 Project 창에서 에셋을 열면 보인다.
            return created;
        }

        static object FindChildByName(PropertyInfo groupChildrenProp, object group, string name)
        {
            var children = groupChildrenProp.GetValue(group) as Array;
            if (children == null) return null;
            foreach (var c in children)
            {
                var displayName = ((UnityEngine.Object)c).name;
                if (displayName == name) return c;
            }
            return null;
        }
    }
}
