using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Icefied.EditorTools
{
    /// <summary>
    /// İlk açılışta Assets/Scenes/Game.unity sahnesini kurar (içinde sadece GameRoot var),
    /// build listesine ekler ve dikey yönü ayarlar. Menüden tekrar çalıştırılabilir: Icefied > Create Game Scene.
    /// </summary>
    [InitializeOnLoad]
    static class IcefiedSetup
    {
        const string ScenePath = "Assets/Scenes/Game.unity";

        static IcefiedSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                CreateScene();
            };
        }

        [MenuItem("Icefied/Create Game Scene")]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Icefied").AddComponent<GameRoot>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            Debug.Log("[Icefied] Game sahnesi hazır. Game görünümünü dikey bir telefon çözünürlüğüne alıp Play'e bas.");
        }

        [MenuItem("Icefied/Reset Progress")]
        static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("icefied.level");
            Debug.Log("[Icefied] İlerleme sıfırlandı (Level 1).");
        }
    }
}
