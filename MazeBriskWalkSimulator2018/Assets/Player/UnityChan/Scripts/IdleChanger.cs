using UnityEngine;
using System.Collections;

//
// ↑↓キーでループアニメーションを切り替えるスクリプト（ランダム切り替え付き）Ver.3
// 2014/04/03 N.Kobayashi
//

// Require these components when using this script
[RequireComponent(typeof(Animator))]



public class IdleChanger : MonoBehaviour
{
    private Random rand;
	private Animator anim;						// Animatorへの参照
	private AnimatorStateInfo currentState;		// 現在のステート状態を保存する参照
	private AnimatorStateInfo previousState;	// ひとつ前のステート状態を保存する参照
    private AnimationClip[] animationClips;
	public bool _random = false;				// ランダム判定スタートスイッチ
	public float _threshold = 0.5f;				// ランダム判定の閾値
	public float _interval = 2f;                // ランダム判定のインターバル
    //private float _seed = 0.0f;					// ランダム判定用シード
    private int length;
	


	// Use this for initialization
	void Start ()
	{
		// 各参照の初期化
		anim = GetComponent<Animator> ();
        animationClips = anim.runtimeAnimatorController.animationClips;
        length = animationClips.Length;
		currentState = anim.GetCurrentAnimatorStateInfo (0);
		previousState = currentState;
		// ランダム判定用関数をスタートする
		//StartCoroutine ("RandomChange"); // produces error
	}
	
	// Update is called once per frame
	void  Update ()
    {
        
	}

}
