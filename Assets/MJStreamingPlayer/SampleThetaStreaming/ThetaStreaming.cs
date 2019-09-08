//
// MJStreamingPlayer for Unity
//
// Copyright (c) Makoto Hamanaka All Rights Reserved.
//

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MJMedia;

[System.Serializable]
public class StartSessionCmdResult {
	[System.Serializable]
	public class Result {
		public string sessionId;
		public int timeout;
	}
	public string name;
	public string state;
	public Result results;
}

public class Theta {
	public string thetaUrl = "http://192.168.1.1";
	private string execCmd = "/osc/commands/execute";
	private MJStreamingPlayer player;

	public delegate void OnSuccess(StartSessionCmdResult result);
	public delegate void OnFailure();

	public Theta(MJStreamingPlayer _player) {
		player = _player;
	}

	public IEnumerator StartThetaSession(MonoBehaviour behavior) {
		byte[] paramsBytes = JsonToBytes ("{'name':'camera.startSession', 'parameters':{} }");
		yield return behavior.StartCoroutine( SendThetaCmd( thetaUrl, paramsBytes, OnStartSessionSuccess, OnStartSessionFailure) );
	}

	private void StartStreaming(string sessionId) {
		Debug.Log ("Theta: StartStreaming() sessionId:"+sessionId);
		player.serverUrl = thetaUrl + execCmd;
		byte[] postBytes = JsonToBytes ("{'name':'camera._getLivePreview','parameters': {'sessionId': '" + sessionId + "'}}");
		player.StartStreamingPOST (postBytes);
	}

	private void OnStartSessionSuccess(StartSessionCmdResult result) {
		StartStreaming (result.results.sessionId);
	}

	private void OnStartSessionFailure() {
		Debug.LogError ("Start Session Failed.");
	}

	private IEnumerator SendThetaCmd ( string thetaUrl, byte[] postBytes, OnSuccess onSuccess, OnFailure onFailure) {
		var headers = new Dictionary<string, string> ();
		headers.Add ("Content-Type", "application/json; charset=utf-8");
		string url = thetaUrl + execCmd;
		WWW www = new WWW (url, postBytes, headers);
		yield return www;
		if (www.error != null) {
			Debug.Log ("Error! url:<"+url+"> "+www.error);          
			onFailure ();
			yield break;
		}
		Debug.Log("Success"+www.text);
		var result = JsonUtility.FromJson<StartSessionCmdResult> (www.text);
		onSuccess (result);
	}

	private byte[] JsonToBytes(string jsonStr) {
		return Encoding.ASCII.GetBytes (jsonStr.Replace('\'','"'));
	}
}
	
public class ThetaStreaming : MonoBehaviour {
	private Theta theta;

	void Start () {
		theta = new Theta (GetComponent<MJStreamingPlayer> ());
		StartCoroutine (theta.StartThetaSession (this));
	}

	void Update () {

	}
}