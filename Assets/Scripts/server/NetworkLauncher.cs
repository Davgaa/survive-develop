using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Bayou;
using TMPro;
using UnityEngine;

public class NetworkLauncher : MonoBehaviour
{
    public static string LastTargetAddress { get; private set; } = "not selected";
    public static ushort LastTargetPort { get; private set; }
    public static int ConnectionAttemptId { get; private set; }

    [SerializeField] private string serverIP = "127.0.0.1";
    [SerializeField] private ushort port = 10000;
    [SerializeField] private TMP_InputField ipInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private bool preferLocalServerInEditor;

    private bool _subscribed;
    private bool _isConnecting;

    private void Awake()
    {
        bool forceInputText = false;

        if (ipInput == null)
            ipInput = FindSceneComponentByName<TMP_InputField>("IP");

        if (statusText == null)
            statusText = FindSceneComponentByName<TMP_Text>("Status");

#if UNITY_EDITOR
        if (preferLocalServerInEditor && !Application.isBatchMode && IsRenderAddress(serverIP))
        {
            serverIP = "127.0.0.1";
            port = 10000;
            forceInputText = true;
        }
#endif

        if (ipInput != null && (forceInputText || string.IsNullOrWhiteSpace(ipInput.text)))
            ipInput.text = serverIP;

        SetDiagnosticTarget(serverIP, port, incrementAttempt: false);
        SetStatus("Ready");
    }

    private void OnClientState(ClientConnectionStateArgs args)
    {
        Debug.Log("[ClientState] " + args.ConnectionState);

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _isConnecting = false;
            SetStatus("Connected");
            Debug.Log("[Client] CONNECT SUCCESS");
        }
        else if (args.ConnectionState == LocalConnectionState.Starting)
        {
            _isConnecting = true;
            SetStatus("Connecting...");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopping)
        {
            _isConnecting = false;
            SetStatus("Connection failed");
            Debug.LogError("[Client] CONNECT FAILED");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            _isConnecting = false;
            SetStatus("Disconnected");
            Debug.LogError("[Client] CONNECT STOPPED");
        }
    }

    public void OnJoinClick()
    {
        if (_isConnecting)
        {
            Debug.Log("[NetworkLauncher] Already connecting.");
            return;
        }

        string targetAddress = GetTargetAddress();
        ushort targetPort = GetTargetPort(targetAddress);
        SetDiagnosticTarget(targetAddress, targetPort, incrementAttempt: true);

        Debug.Log("[NetworkLauncher] OnJoinClick called");
        Debug.Log("[Client] Target IP: " + targetAddress + ", Port: " + targetPort);

        if (InstanceFinder.NetworkManager == null)
        {
            SetStatus("NetworkManager missing");
            Debug.LogError("NetworkManager is missing!");
            return;
        }

        if (InstanceFinder.ClientManager.Started)
        {
            SetStatus("Already connected");
            Debug.Log("[NetworkLauncher] Client is already connected.");
            return;
        }

        var bayou = InstanceFinder.NetworkManager.GetComponent<Bayou>();
        if (bayou == null)
        {
            SetStatus("Bayou transport missing");
            Debug.LogError("Bayou not found on NetworkManager!");
            return;
        }

        if (!_subscribed)
        {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientState;
            _subscribed = true;
        }

        serverIP = targetAddress;
        port = targetPort;
        bool useWss = IsRenderAddress(serverIP);
        bayou.SetClientAddress(serverIP);
        bayou.SetPort(port);
        bayou.SetUseWSS(useWss);

        SetStatus("Connecting...");
        _isConnecting = true;
        InstanceFinder.ClientManager.StartConnection();
    }

    public void OnPlayLocalClick()
    {
        SetAddress("127.0.0.1", 10000);
        OnJoinClick();
    }

    public void OnMultiplayerRenderClick()
    {
        SetAddress("survive-server-m1gl.onrender.com", 443);
        OnJoinClick();
    }

    public void UseLocalServer()
    {
        SetAddress("127.0.0.1", 10000);
    }

    public void UseRenderServer()
    {
        SetAddress("survive-server-m1gl.onrender.com", 443);
    }

    private string GetTargetAddress()
    {
        if (ipInput != null && !string.IsNullOrWhiteSpace(ipInput.text))
            return ipInput.text.Trim();

        return string.IsNullOrWhiteSpace(serverIP) ? "127.0.0.1" : serverIP.Trim();
    }

    private ushort GetTargetPort(string targetAddress)
    {
        if (IsLocalAddress(targetAddress) && port == 443)
            return 10000;

        if (IsRenderAddress(targetAddress) && port == 10000)
            return 443;

        return port;
    }

    private static bool IsLocalAddress(string address)
    {
        return address == "127.0.0.1" ||
               address == "localhost" ||
               address == "::1";
    }

    private static bool IsRenderAddress(string address)
    {
        return address.EndsWith(".onrender.com");
    }

    private void SetAddress(string address, ushort targetPort)
    {
        serverIP = address;
        port = targetPort;
        SetDiagnosticTarget(serverIP, port, incrementAttempt: false);

        if (ipInput != null)
            ipInput.text = serverIP;

        SetStatus($"Target: {serverIP}:{port}");
    }

    private static void SetDiagnosticTarget(string address, ushort targetPort, bool incrementAttempt)
    {
        LastTargetAddress = address;
        LastTargetPort = targetPort;

        if (incrementAttempt)
            ConnectionAttemptId++;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private static T FindSceneComponentByName<T>(string objectName) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component.gameObject.name == objectName && component.gameObject.scene.IsValid())
                return component;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (_subscribed && InstanceFinder.NetworkManager != null)
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientState;
    }
}
