using UnityEngine;
using UnityEngine.Networking;

public class PlayerSetup : NetworkBehaviour {

    [SerializeField]
    private Behaviour[] componentsToDisable;

    private void Start()
    {

        if (!isLocalPlayer)
        {

            for (int i = 0; i < componentsToDisable.Length; i++)
            {
                componentsToDisable[i].enabled = false;
            }

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
}
