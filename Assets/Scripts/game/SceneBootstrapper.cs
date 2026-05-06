using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneBootstrapper : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log("[Bootstrap] Awake");
    }

    void Start()
    {
        if (Application.isBatchMode)
        {
            Debug.Log("[Bootstrap] SERVER MODE");
            return;
        }

        Debug.Log("[Bootstrap] CLIENT MODE");
        Debug.Log("[Bootstrap] Loading Mainmenu...");
        SceneManager.LoadScene("Mainmenu");
    }
}
