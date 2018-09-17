using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager {
    
    //TODO: will fix all the GameObject.Find() at a later date

    public void StartupHost()
    {
        NetworkServer.Reset();
        SetPort();
        NetworkManager.singleton.StartHost();
    }

    public void JoinGame()
    {
        SetIPAddress();
        SetPort();
        NetworkManager.singleton.StartClient();
    }

    private void SetIPAddress()
    {
        string ipAddress = GameObject.Find("InputFieldIPAddress").transform.Find("Text").GetComponent<Text>().text;
        NetworkManager.singleton.networkAddress = ipAddress;
    } 

    public void SetPort()
    {
        NetworkManager.singleton.networkPort = 7777;
    }

    
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnLevelFinsihedLoading;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLevelFinsihedLoading;
    }

    private void OnLevelFinsihedLoading(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainLobby")
        {
            SetupMenuSceneButtons();
        }
        else
        {
            //SetupOtherSceneButtons();
        }
    }

    private void SetupMenuSceneButtons()
    {
        GameObject.Find("Host").GetComponent<Button>().onClick.RemoveAllListeners();
        GameObject.Find("Host").GetComponent<Button>().onClick.AddListener(StartupHost);

        GameObject.Find("Join").GetComponent<Button>().onClick.RemoveAllListeners();
        GameObject.Find("Join").GetComponent<Button>().onClick.AddListener(JoinGame);
    }
    // TODO: Implement Disconnect for client side
    //private void SetupOtherSceneButtons()
    //{
    //    GameObject.Find("Disconnect").GetComponent<Button>().onClick.RemoveAllListeners();
    //    GameObject.Find("Disconnect").GetComponent<Button>().onClick.AddListener(NetworkManager.singleton.StopHost);
    //}

}
