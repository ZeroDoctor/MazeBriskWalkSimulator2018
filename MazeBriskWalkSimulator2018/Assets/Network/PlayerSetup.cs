using UnityEngine;
using UnityEngine.Networking;

public class PlayerSetup : NetworkBehaviour {

    [SerializeField]
    private Behaviour[] componentsToDisable;

    [SerializeField]
    string enemyLayerName = "Enemy";

    private void Start()
    {
        Debug.Log("Hey!");
        if (!isLocalPlayer)
        {
            Debug.Log("This is not the local player!");
            //DisableComponents();
            AssignEnemyLayer();
            
        }
        else
        {
            GameObject.Find("SceneCamera").SetActive(false);
            Debug.Log("SceneCamera has been deactived");
            /*sceneCamera = Camera.main;
            if(sceneCamera != null)
            {
                //sceneCamera.gameObject.SetActive(false);
            }*/
        }
    }

    void RegisterPlayer()
    {
        string ID = "Player: " + GetComponent<NetworkIdentity>().netId;
        transform.name = ID;
    }

    void AssignEnemyLayer()
    {
        gameObject.layer = LayerMask.NameToLayer(enemyLayerName);
    }

    void DisableComponents()
    {
        for (int i = 0; i < componentsToDisable.Length; i++)
        {
            if(componentsToDisable[i] != null)
            {
                componentsToDisable[i].enabled = false;
            }
        }
    }
}
