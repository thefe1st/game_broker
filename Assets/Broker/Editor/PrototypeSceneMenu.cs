using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Broker.Editor
{
    public static class PrototypeSceneMenu
    {
        [MenuItem("Broker/Create 3D Office Scene")]
        public static void Create3DScene() { CreateScene(); }

        [MenuItem("Broker/Create Prototype Scene")]
        public static void CreateScene()
        {
            const string path = "Assets/Scenes/Prototype.unity";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(path))
            {
                EditorSceneManager.OpenScene(path);
                return;
            }
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            if (EditorBuildSettings.scenes.Length == 0)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            AssetDatabase.Refresh();
        }
    }
}
