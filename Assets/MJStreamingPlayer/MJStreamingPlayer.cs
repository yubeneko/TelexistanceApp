//
// MJStreamingPlayer for Unity
//
// Copyright (c) Makoto Hamanaka All Rights Reserved.
//

using UnityEngine;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using MJMedia;

public class MJStreamingPlayer : MonoBehaviour {
	public string serverUrl; // example of mjpg_streamer: "http://192.168.1.26:8080/?action=stream";
	public bool playAutomatically = true;
	public bool showMovieFps = false;
	private FrameQueue frameQueue = new FrameQueue(3);
	private Texture2D tex;
	private float lastUpdateTime = 0;
	private FramePusher framePusher;
	private GUIStyle style = new GUIStyle();
	public bool autoReconnect = true;
	private DelaylessFrameDecoder frameDecoder;

	[SerializeField]
	private bool _playing;
	[SerializeField]
	private bool _failed;
	[SerializeField]
	private float _fpsLoad;
	[SerializeField]
	private float _fpsShow;
	[SerializeField]
	private float _fpsSkip;

	void Awake() {
		style.fontSize = 30;
	}

	// Use this for initialization
	void Start () {
		tex = new Texture2D(2, 2);
		if (playAutomatically) {
			StartStreaming ();
		}
	}

	// Update is called once per frame
	void Update () {
		if ( Input.GetKey(KeyCode.Escape) ) {
			if (framePusher != null) {
				framePusher.Stop ();
			}
		}
		if (Time.fixedTime - lastUpdateTime > 1.0 / 31.0) {
			lastUpdateTime = Time.fixedTime;
			TryUpdateImage ();
		}
		_playing = (framePusher != null) && framePusher.Playing;
		_failed = (framePusher != null) && framePusher.Failed;
		_fpsLoad = frameQueue.Stats.fpsLoad ();
		_fpsShow = frameQueue.Stats.fpsShow ();
		_fpsSkip = frameQueue.Stats.fpsSkip ();
	}

	void OnGUI() {
		if (showMovieFps) {
			GUI.TextField (new Rect (0, 120, 180, 50), "movie load FPS: " + _fpsLoad.ToString("0.00"), style);
			GUI.TextField (new Rect (0, 170, 180, 50), "movie show FPS: " + _fpsShow.ToString("0.00"), style);
		}
	}

	void OnDestroy() {
		StopStreaming ();
	}

	private void ProcessFrameBuffer(byte[] data, int width, int height) {
		if (data == null) {
			return;
		}
		if (tex.width != width || tex.height != height) {
			tex = new Texture2D (width, height, TextureFormat.RGB24, false);
		}
		tex.LoadRawTextureData (data);
		tex.Apply ();
		GetComponent<Renderer>().material.mainTexture = tex;
	}

	private void TryUpdateImage() {
		if (frameDecoder != null) {
			frameDecoder.ComsumeFrame (ProcessFrameBuffer);
			frameDecoder.RequestDecode ();
		}
	}

	public void StopStreaming() {
		MJMedia.Logger.LogInfo ("StopStreaming() called.");

		if (framePusher != null) {
			framePusher.Stop ();
			framePusher = null;
		}
		if (frameDecoder != null) {
			frameDecoder.Stop ();
			frameDecoder = null;
		}
	}

	public void StartStreaming() {
		StartStreamingCommon (onDisconnect => {
			framePusher.StartAsync (serverUrl, frameQueue, onDisconnect);
		});
	}

	public void StartStreamingPOST(byte[] postBytes) {
		StartStreamingCommon (onDisconnect => {
			framePusher.StartAsync (serverUrl, frameQueue, postBytes, onDisconnect);
		});
	}

	private void StartStreamingCommon(StartConnectionFunc startConnection) {
		MJMedia.Logger.LogInfo ("StartStreaming() called.");
		StopStreaming ();
		if (!TurboJpegDecoder.TurboAvailable) {
			MJMedia.Logger.LogError ("Error! TurboJPEG is not available.");
			return;
		}
		if (framePusher != null) {
			framePusher.Stop ();
		}
		framePusher = new FramePusher();
		StartCoroutine (FramePushStartReconnectable (startConnection));
		frameDecoder = new DelaylessFrameDecoder(frameQueue);
		frameDecoder.Start ();
	}

	delegate void StartConnectionFunc(FramePusher.OnDisconnect onDisconnect);

	private IEnumerator FramePushStartReconnectable(StartConnectionFunc startConnection) {
		while (Application.isPlaying && autoReconnect) {
			if (frameQueue == null || framePusher == null) {
				yield break;
			}
			var disconnectedEvent = new AutoResetEvent (false);
			startConnection(pusher => {
				MJMedia.Logger.LogInfo ("OnDisconnect called.");
				disconnectedEvent.Set ();
			});
			// disconnectまたはStopされるまでループする。
			while (!disconnectedEvent.WaitOne(1)) {
				if (framePusher == null) {
					yield break;
				}
				yield return new WaitForSeconds (3.0f);
			}
			disconnectedEvent.Close ();
			if (framePusher != null) {
				MJMedia.Logger.LogInfo ("Disconnect Detected!");
				framePusher.Stop ();
			} else {
				yield break;
			}
			yield return new WaitForSeconds (8.0f);
		}
	}

	public bool Playing {
		get { return _playing; }
	}
	public bool Failed {
		get { return _failed; }
	}
	public float FpsLoad {
		get { return _fpsLoad; }
	}
	public float FpsShow {
		get { return _fpsShow; }
	}
	public float FpsSkip {
		get { return _fpsSkip; }
	}
}
