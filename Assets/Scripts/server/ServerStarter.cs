using UnityEngine;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Transporting.Bayou;
using System;

public class ServerStarter : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";

    private void Awake()
    {
        if (Application.isBatchMode)
            StartServer();
    }

    private void StartServer()
    {
        string portStr = Environment.GetEnvironmentVariable("PORT");
        ushort port = 10000;

        if (!string.IsNullOrWhiteSpace(portStr))
        {
            if (!ushort.TryParse(portStr, out port))
                port = 10000;
        }

        Debug.Log("[Server] ENV PORT = " + port);

        if (InstanceFinder.NetworkManager == null)
        {
            Debug.LogError("[Server] NetworkManager missing!");
            return;
        }

        var bayou = InstanceFinder.NetworkManager.GetComponent<Bayou>();
        if (bayou == null)
        {
            Debug.LogError("[Server] Bayou not found!");
            return;
        }

        bayou.SetPort(port);
        bayou.SetServerBindAddress("0.0.0.0", FishNet.Transporting.IPAddressType.IPv4);

        Debug.Log("[Server] Starting on port: " + port);

        InstanceFinder.ServerManager.StartConnection();

        SceneLoadData sld = new SceneLoadData(gameSceneName)
        {
            ReplaceScenes = ReplaceOption.All
        };

        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
        Debug.Log("[Server] Loading scene: " + gameSceneName);
    }
}
