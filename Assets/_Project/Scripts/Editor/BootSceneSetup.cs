using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CCT.Editor
{
    /// <summary>
    /// 10_검증 빌드 납품 기준 v1.0 §4 — "Assets/_Project/Scenes/Boot.unity를 단일 시작 씬으로 등록한다.
    /// 기존 SampleScene 자동 부팅으로 중복 생성되지 않게 정리한다."
    /// GameBootstrap이 씬 내용과 무관하게(RuntimeInitializeOnLoadMethod) 게임을 구성하므로 Boot.unity는
    /// 완전히 빈 씬이면 충분하다 — 이 스크립트는 그 빈 씬을 만들고 빌드 씬 목록을 Boot 하나로 정리한다.
    /// 한 번만 실행하면 되는 설정 작업이라 BuildRelease와 분리했다.
    /// </summary>
    public static class BootSceneSetup
    {
        const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        [MenuItem("CCT/Create Boot Scene And Fix Build List")]
        public static void CreateBootSceneAndFixBuildList()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BootScenePath)!);

            if (!File.Exists(BootScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, BootScenePath);
                Debug.Log($"[CCT] Boot 씬 생성: {BootScenePath}");
            }
            else
            {
                Debug.Log($"[CCT] Boot 씬 이미 존재: {BootScenePath} (건드리지 않음)");
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
            Debug.Log("[CCT] 빌드 씬 목록을 Boot 하나로 정리 — 기존 SampleScene 등은 빠짐");
        }
    }
}
