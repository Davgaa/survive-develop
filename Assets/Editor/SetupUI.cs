using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupUI
{
    [MenuItem("Tools/Setup UI Scenes")]
    public static void Run()
    {
        SetupScene("Assets/Scenes/Mainmenu.unity", "UIManager", "MainMenuUI");
        SetupScene("Assets/Scenes/Game.unity",     "HUDManager", "GameHUD");
        Debug.Log("[SetupUI] Done! Both scenes configured.");
    }

    static void SetupScene(string scenePath, string goName, string scriptName)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Remove old instance if any
        var old = GameObject.Find(goName);
        if (old != null) Object.DestroyImmediate(old);

        var go = new GameObject(goName);

        var type = System.Type.GetType(scriptName + ", Assembly-CSharp");
        if (type != null)
            go.AddComponent(type);
        else
            Debug.LogError($"[SetupUI] Script '{scriptName}' not found. Make sure it compiled.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupUI] Saved {scenePath}");
    }
}
