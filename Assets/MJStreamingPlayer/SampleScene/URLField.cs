using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using MJMedia;

public class URLField : MonoBehaviour {

	public InputField field;
	public MJStreamingPlayer player;

	// Use this for initialization
	void Start () {
		field.text = player.serverUrl;	
	}
	
	// Update is called once per frame
	void Update () {
		field.enabled = !player.Playing;	
	}

	public void StartStreaming() {
		if (field.text != "") {
			player.serverUrl = field.text;
		} else {
			field.text = player.serverUrl;
		}
		player.StartStreaming ();
	}
}
