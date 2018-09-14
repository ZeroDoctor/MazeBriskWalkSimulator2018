using UnityEngine;
using System.Collections;

public class FaceUpdate : MonoBehaviour
{
	public AnimationClip[] animations;

	Animator anim;

	public float delayWeight;

	void Start ()
	{
		anim = GetComponent<Animator> ();
	}

	float current = 0;


	void Update ()
	{

		if (Input.GetMouseButton (0)) {
			current = 1;
		} else {
			current = Mathf.Lerp (current, 0, delayWeight);
		}
		anim.SetLayerWeight (0, current);
	}
}
