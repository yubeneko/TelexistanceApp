using UnityEngine;
using System.Collections;

public class Rotator : MonoBehaviour {
	private float rotY = 0;
	public float angularVelocityY = 3.0f;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		rotY += angularVelocityY * Time.deltaTime;
		transform.localRotation = Quaternion.Euler (0, rotY, 0);
	}
}
