using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class camMouseLook : MonoBehaviour {


	private Vector2 mouseLook;
	private Vector2 smooth;
	private Vector2 md;

	public float sensitivity = 3.0f;
	public float smoothing = 2.0f;

	GameObject player;


	void Start () {
		Cursor.lockState = CursorLockMode.Locked;
		player = this.transform.parent.gameObject;
	}
	
	// Update is called once per frame
	void Update () {

		if(Input.GetKeyDown("escape")) {
			Cursor.lockState = CursorLockMode.None;
		}

		md = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

		md = Vector2.Scale(md, new Vector2(sensitivity * smoothing, sensitivity * smoothing));
		smooth.x = Mathf.Lerp(smooth.x, md.x, 1f / smoothing);
		smooth.y = Mathf.Lerp(smooth.y, md.y, 1f / smoothing);
		mouseLook += smooth;

		mouseLook.y = Mathf.Clamp(mouseLook.y, -84f, 84f);
		transform.localRotation = Quaternion.AngleAxis(-mouseLook.y, Vector3.right);
		player.transform.localRotation = Quaternion.AngleAxis(mouseLook.x, player.transform.up);
	}
}
