using UnityEngine;
using UnityEngine.Networking;

public class PlayerSetup : NetworkBehaviour {

    [SerializeField]
    private Behaviour[] componentsToDisable;

    [SerializeField]
    string enemyLayerName = "Enemy";

    private void Start()
    {

        if (!isLocalPlayer)
        {
            DisableComponents();
            AssignEnemyLayer();
        }
        else
        {
            GameObject.Find("SceneCamera").SetActive(false);
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
