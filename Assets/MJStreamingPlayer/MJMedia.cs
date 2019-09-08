using UnityEngine;
using System.Net;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using System;

// - jpegデコードをメインスレッドで行うとGearVRなどでひどくブレるので良くない。
// - jpegデコードをネットワークストリームリードスレッドで行うと、スキップできないために遅延が大きくなるので微妙。
// このため、jpegデコードはスレッドを分けて、ネットワークからのフレームを遅延に応じてうまくスキップしながらデコードする。

// ネットワークスレッドは、jpeg１枚単位に分解しながら最大限ネットから情報を引っ張る。
// → ネットおよびサーバーのバッファに必要以上に情報がたまるのを防ぐ
// デコードスレッドは、前回フレームレンダリングを受けて動作し、最小限動作する。
// → 必要以上のフレームをデコードして、CPUを余分に消費しないようにする
// ネットワークとデコードをつなぐキューは、２フレームぐらいしか貯めないようにして、古いフレームは破棄する。
// → キューに余分に貯まることによる遅延を防ぐ。

namespace MJMedia {

	// Singleton Logger
	class Logger {
		public enum LogLevel {
			Verbose,
			Info,
			Warning,
			Error,
		}
		private static Logger instance;

		public LogLevel level = LogLevel.Warning;

		public static void LogError(string msg) {
			GetInstance ().Log (LogLevel.Error, msg);
		}
		public static void LogWarning(string msg) {
			GetInstance ().Log (LogLevel.Warning, msg);
		}
		public static void LogInfo(string msg) {
			GetInstance ().Log (LogLevel.Info, msg);
		}
		public static void LogVerbose(string msg) {
			GetInstance ().Log (LogLevel.Verbose, msg);
		}
		public void SetLogLevel(LogLevel level) {
			GetInstance ().level = level;
		}

		private static Logger GetInstance() {
			if (instance == null) {
				instance = new Logger ();
			}
			return instance;
		}

		private void Log(LogLevel level, string msg) {
			if (!IsLevelShown (level)) {
				return;
			}
			switch(level) {
			case LogLevel.Error:
				Debug.LogError (msg);
				break;
			case LogLevel.Warning:
				Debug.LogWarning (msg);
				break;
			case LogLevel.Info:
				Debug.Log(msg);
				break;
			case LogLevel.Verbose:
				Debug.Log (msg);
				break;
			}
		}

		private bool IsLevelShown(LogLevel level) {
			return (int)level >= (int)this.level;
		}
	}

	class MovieStats {
		const int maxSamples = 100;
		private Deque<float> frameLoadTimes = new Deque<float>(maxSamples+1);
		private Deque<float> frameShowTimes = new Deque<float>(maxSamples+1);
		private Deque<float> frameSkipTimes = new Deque<float>(maxSamples+1);
		System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch ();

		public MovieStats() {
			sw.Start ();
		}

		public void CountFrameLoad() {
			AddSampleToQueue (frameLoadTimes);
		}
		public void CountFrameSkip() {
			AddSampleToQueue (frameSkipTimes);
		}
		public void CountFrameShow() {
			AddSampleToQueue (frameShowTimes);
		}
		void AddSampleToQueue(Deque<float> queue) {
			lock (this) {
				queue.AddFront (sw.ElapsedMilliseconds*0.001f);
				if (queue.Count >= maxSamples) {
					queue.RemoveBack ();
				}
			}
		}
		public float fpsLoad() {
			return CalcFps (frameLoadTimes);
		}
		public float fpsSkip() {
			return CalcFps (frameSkipTimes);
		}
		public float fpsShow() {
			return CalcFps (frameShowTimes);
		}
		public float CalcFps(Deque<float> queue) {
			int count = 0;
			float firstTime = 0;
			float lastTime = 0;
			lock (this) {
				count = queue.Count;
				if (count >= 2) {
					firstTime = queue.Get (0);
					lastTime = queue.Get (count - 1);
				}
			}
			if (count <= 1) {
				return 0;
			}
			float fps = (count - 1) / (firstTime - lastTime);
			return fps;
		}
	}

	// 返却されたバッファをできるだけ再利用するバッファプール。
	// スレッドセーフ。
	class BufferPool {
		private Deque<byte[]> pool = new Deque<byte[]>();

		// リクエストされたサイズ以上のバッファを返す。
		byte[] GetDataBuffer(int size) {
			lock (this) {
				if (pool.Count > 0) {
					byte[] candidate = pool.RemoveFront ();
					if (candidate.Length > size) {
						return candidate;
					}
				}
			}
			return GetNewBuffer (size);
		}

		private byte[] GetNewBuffer(int neededSize) {
			return new byte[(int)(neededSize * 1.2)];
		}

		// バッファをプールから取り出し、さらにデータをコピーして渡す
		public byte[] GetDataBufferWithContents(byte[] src, int size) {
			byte[] dest = GetDataBuffer(size);
			System.Array.Copy(src, 0, dest, 0, size);
			return dest;
		}

		// 返却
		public void Push(byte[] buf) {
			lock (this) {
				pool.AddFront (buf);
			}
		}
	}


	// byte[] のキュー。
	// 指定サイズ以上Pushすると、指定サイズ以下になるよう末尾のデータから削除される。
	// スレッドセーフ。
	class FrameQueue {
		private Deque<byte[]> frames = new Deque<byte[]>();
		private BufferPool bufferPool = new BufferPool();
		private int maxQueueCount;
		MovieStats stats = new MovieStats();

		public FrameQueue(int _maxQueueCount) {
			maxQueueCount = _maxQueueCount;
		}

		public void Push(byte[] frame) {
			stats.CountFrameLoad ();
			byte[] trashBuf = null;
			lock (this) {
				frames.AddFront (frame);
				if (frames.Count >= maxQueueCount) {
					stats.CountFrameSkip ();
					trashBuf = frames.RemoveBack ();
				}
			}
			// lock内でPushしないのは、thisとbufferPoolの両方のlockを同時にとらないようにする配慮。
			if (trashBuf != null) {
				bufferPool.Push (trashBuf);
			}
		}

		public byte[] Pop() {
			lock (this) {
				if (frames.IsEmpty) {
					return null;
				}
				stats.CountFrameShow ();
				return frames.RemoveBack ();
			}
		}

		public byte[] GetDataBufferWithContents(byte[] src, int size) {
			return bufferPool.GetDataBufferWithContents (src, size);
		}

		public void Pool(byte[] buf) {
			bufferPool.Push (buf);
		}

		public int Count
		{
			get {
				lock (this) {
					return frames.Count;
				}
			}
		}

		public BufferPool BufferPool
		{
			get { return bufferPool; }
		}

		public MovieStats Stats {
			get { return stats; }
		}
	}

	/*
// BufferedStream　からMJPGストリームを読み込み、１フレームごとにonFrameCompleteを呼ぶ。
	// This is slower version. Use faster MjpegReader2.
	class MjpegReader {
		public delegate void OnFrameComplete(MemoryStream imageBytes);
		private BufferedStream bstream;
		private OnFrameComplete onFrameComplete;

		private byte[] data = new byte[2];
		private MemoryStream imageBytes = new MemoryStream();
		private bool isLoadStart = false;
		private bool isLooping = true;

		public MjpegReader(BufferedStream _bstream, OnFrameComplete _onFrameComplete) {
			bstream = _bstream;
			onFrameComplete = _onFrameComplete;
		}

		private byte ReadStreamByte() {
			int data = bstream.ReadByte ();
			if (data == -1) {
				throw new EndOfStreamException();
			}
			return (byte)data;
		}

		private void Read2Bytes() {
			data[0] = ReadStreamByte ();
			data[1] = ReadStreamByte ();
		}

		private void Write2Bytes() {
			imageBytes.WriteByte (data[0]);
			imageBytes.WriteByte (data[1]);
		}

		private bool checkJpegStart() {
			return (data[0] == 0xFF && data[1] == 0xD8);
		}

		private bool checkBoundaryStart() {
			return (data[0] == 0xFF && data[1] == 0xD9);
		}

		public void Run() {
			while (isLooping) {
				Read2Bytes();
				if (!isLoadStart) {
					if (checkJpegStart ()) {
						Write2Bytes();
						isLoadStart = true;
					}
				} else {
					Write2Bytes();
					if (checkBoundaryStart ()) {
						onFrameComplete (imageBytes);
						imageBytes.SetLength (0);
						isLoadStart = false;
					}
				}
			}
		}

		public void Stop() {
			isLooping = false;
		}

		public bool IsLooping {
			get { return isLooping; }
		}
	}
	*/

	class OffsetBuffer {

		public struct Position
		{
			public int pos;

			public Position(int _pos = 0) {
				pos = _pos;
			}
		}
		private int bufsize;
		private int bufOffset = 0;
		private int nextOffset = 0;
		private byte[] data;
		private Position _notFoundPos = new Position(-1);

		public OffsetBuffer(int _bufsize) {
			bufsize = _bufsize;
			data = new byte[bufsize];
		}

		public void Reset() {
			bufOffset = 0;
			nextOffset = 0;
		}

		// 続く２文字を見つけるために、最終バイトは残すバージョン。
		public void ResetUsingLastByte() {
			if (AvailableDataCount == 0) {
				Reset ();
				return;
			}
			byte lastByte = data [nextOffset - 1];
			Reset ();
			data [0] = lastByte;
			AdvanceWrittenData (1);
		}

		public void AdvanceWrittenData(int count) {
			if (nextOffset + count > bufsize) {
				Debug.LogError ("nextOffset("+nextOffset+") + count("+count+") > bufsize("+bufsize+")");
				nextOffset = bufsize;
			}
			nextOffset += count;
		}

		public void Move(MemoryStream stream, int size) {
			if (bufOffset + size > nextOffset) {
				Debug.LogError ("Move: bufOffset("+bufOffset+") + size("+size+") > nextOffset("+nextOffset+")");
				size = AvailableDataCount;
			}
			stream.Write (data, bufOffset, size);
			bufOffset += size;
		}

		public void MoveDataUntil(MemoryStream stream, Position pos) {
			int size = pos.pos - bufOffset;
			Move (stream, size);
		}

		public void Skip(int size) {
			bufOffset += size;
		}

		public void SkipTo(Position pos) {
			if (pos.pos < bufOffset) {
				Debug.LogError ("SkipTo: pos.pos("+pos.pos+") < bufOffset("+bufOffset+")");
			}
			bufOffset = pos.pos;
		}

		public void MoveAllTo(MemoryStream stream) {
			stream.Write (data, bufOffset, AvailableDataCount);
			Reset ();
		}

		// 続く２文字を見つけるために、最終バイトは残すバージョン。
		public void MoveAllWithoutLastByteTo(MemoryStream stream) {
			if (AvailableDataCount == 0) {
				return;
			}
			stream.Write (data, bufOffset, AvailableDataCount-1);
			ResetUsingLastByte ();
		}

		public Position SearchForPattern(byte b1, byte b2) {
			// Array.IndexOfで書き換えたくなると思いますが、Array.IndexOfはforループより遥かに遅いです。
			for (int i=bufOffset ; i<nextOffset-1 ; i++) {
				if (data[i] == b1 && data[i+1] == b2) {
					return new Position(i);
				}
			}
			return NotFoundPos;
		}

		public Position EndPos {
			get {
				Position pos;
				pos.pos = nextOffset;
				return pos;
			}
		}

		public Position NotFoundPos {
			get { return _notFoundPos; }
		}

		public int AvailableDataCount {
			get { return nextOffset - bufOffset; }
		}

		public int WritableDataCount {
			get { return bufsize - nextOffset; }
		}

		public int NextWriteOffset {
			get { return nextOffset; }
		}

		public byte[] Buffer {
			get { return data; }
		}
	}

	class Error {
		public Exception e;
		public Error(Exception _e = null) {
			e = _e;
		}
	}

	// BufferedStream　からMJPGストリームを読み込み、１フレームごとにonFrameCompleteを呼ぶ。
	class MjpegReader2 {
		public delegate void OnFrameComplete(MemoryStream imageBytes);
		public delegate void OnError(MJMedia.Error e);
		private BufferedStream bstream;
		private OnFrameComplete onFrameComplete;
		private OnError onError;

		private const int bufsize = 30000;
		private OffsetBuffer buffer = new OffsetBuffer (bufsize);

		private MemoryStream imageBytes = new MemoryStream();
		private bool isLoadStart = false;
		private bool isLooping = true;

		private System.AsyncCallback readCallback;

		private Stream commandStream;
		private AutoResetEvent readyToRead = new AutoResetEvent (false);

		public MjpegReader2(BufferedStream _bstream, OnFrameComplete _onFrameComplete, OnError _onError = null) {
			bstream = _bstream;
			onFrameComplete = _onFrameComplete;
			onError = _onError;
		}

		private void ReadBytes() {
			readCallback = new System.AsyncCallback (ReadBufComplete);
			if (buffer.AvailableDataCount <= 1) {
				buffer.ResetUsingLastByte ();
			}
			bstream.BeginRead(buffer.Buffer, buffer.NextWriteOffset, buffer.WritableDataCount, readCallback, null);
		}

		private void ReadBufComplete(System.IAsyncResult result) {
			int readCount;
			try {
				readCount = bstream.EndRead (result);
			} catch (Exception e) {
				// not reached here, as I observed.
				Logger.LogError("Async EndRead Exception. error:"+e);
				onError (new Error(e));
				return;
			}
			if (readCount == 0) {
				//Stop ();
				return;
			}
			buffer.AdvanceWrittenData (readCount);
			while (buffer.AvailableDataCount > 1) {
				if (!isLoadStart) {
					OffsetBuffer.Position pos = searchJpegStart ();
					if (pos.pos >= 0) {
						// 開始マークを発見。直前までスキップして開始マークは追加。
						buffer.SkipTo (pos);
						pos.pos += 2;
						buffer.MoveDataUntil (imageBytes, pos);
						isLoadStart = true;
					} else {
						// バッファ内に見つからないので最後の1バイト以外全部すてる
						buffer.ResetUsingLastByte();
					}
				} else {
					OffsetBuffer.Position pos = searchBoundaryStart();
					if (pos.pos < 0) {
						// 終端が見つからない → バッファ内全部入れる
						buffer.MoveAllWithoutLastByteTo (imageBytes);
					} else {
						// 終端があった → そこまで入れる。
						pos.pos += 2;
						buffer.MoveDataUntil (imageBytes, pos);
						onFrameComplete (imageBytes);
						imageBytes.SetLength (0);
						isLoadStart = false;
					}
				}
			}
			if (isLooping) {
				// let Run() loop to call ReadBytes again
				readyToRead.Set ();
			}
		}

		private OffsetBuffer.Position searchJpegStart() {
			return buffer.SearchForPattern(0xff, 0xd8);
		}

		private OffsetBuffer.Position searchBoundaryStart() {
			return buffer.SearchForPattern(0xff, 0xd9);
		}

		public void Run() {
			while (isLooping) {
				ReadBytes();
				Thread.Sleep (1);
				readyToRead.WaitOne ();
			}
			Logger.LogInfo ("mjpgReader2.Run() exiting...");
			bstream.Close ();
		}

		public void Stop() {
			Logger.LogVerbose ("MjpgReader2.Stop() called.");
			isLooping = false;
			readyToRead.Set ();
		}

		public bool IsLooping {
			get { return isLooping; }
		}
	}

	// ネットからMJPEGを読んでframeQueueにPushするところまでをスレッドで実行する。
	class FramePusher {
		//private bool enableFrameLog = false;//true;
		private Thread thread;
		private FrameQueue frameQueue;
		private BufferedStream bstream;
		private const int bufsize = 30000;
		private string url;
		private bool failed = false;
		private bool eof = false;
		MjpegReader2 mjpgReader;
		private WebRequest request;
		private HttpWebResponse response;
		public delegate void OnDisconnect(FramePusher framePusher);
		private OnDisconnect onDisconnect;

		~FramePusher() {
			Logger.LogInfo ("~FramePusher() called.");
			Stop ();
		}

		public void Start (string _url, FrameQueue _frameQueue, OnDisconnect _onDisconnect = null) {
			Stop ();
			url = _url;
			frameQueue = _frameQueue;
			onDisconnect = _onDisconnect;
			eof = false;
			failed = false;
			thread = new Thread (ReadThread);
			thread.Priority = System.Threading.ThreadPriority.AboveNormal;
			thread.Start ();
		}

		private void StartConnection() {
			var request = HttpWebRequest.Create (url);
			if (request == null) {
				Logger.LogError ("error! url<"+url+"> is invalid!");
				return;
			}
			request.Method = "GET";
			request.Timeout = 10000;
			Logger.LogInfo ("sync connection start. url: <" + url + ">");
			this.request = request;
			response = (HttpWebResponse)request.GetResponse ();
			Logger.LogInfo ("response: " + response);
			Stream stream = response.GetResponseStream ();
			bstream = new BufferedStream (stream, bufsize);
			mjpgReader = new MjpegReader2(bstream, OnFrameComplete);
		}

		// for GET
		public void StartAsync (string _url, FrameQueue _frameQueue, OnDisconnect _onDisconnect = null) {
			HttpWebRequest request = CreateRequestForGET(_url);
			StartAsync(_frameQueue, request, _onDisconnect);
		}

		// for POST
		public void StartAsync (string _url, FrameQueue _frameQueue, byte[] postBytes, OnDisconnect _onDisconnect = null) {
			HttpWebRequest request = CreateRequestForPOST(_url, postBytes);
			StartAsync(_frameQueue, request, _onDisconnect);
		}

		private void StartAsync (FrameQueue _frameQueue, HttpWebRequest request, OnDisconnect _onDisconnect = null) {
			if (mjpgReader != null) {
				Debug.LogError ("previous play not stopped.");
				return;
			}
			frameQueue = _frameQueue;
			onDisconnect = _onDisconnect;
			eof = false;
			failed = false;
			StartConnectionAsync (request);
		}

		private HttpWebRequest CreateRequestForGET(string url) {
			var request = (HttpWebRequest) HttpWebRequest.Create (url);
			if (request == null) {
				Logger.LogError ("error! url<"+url+"> is invalid!");
				return null;
			}
			request.Method = "GET";
			request.Timeout = 10000;
			Logger.LogInfo ("CreateRequestForGET() url: " + url);
			return request;
		}

		private HttpWebRequest CreateRequestForPOST(string url, byte[] postBytes) {
			var request = (HttpWebRequest)HttpWebRequest.Create (url);
			if (request == null) {
				Logger.LogError ("error! url<"+url+"> is invalid!");
				return null;
			}
			request.Method = "POST";
			request.Timeout = 10000;
			request.ContentType = "application/json;charset=utf-8";
			request.ContentLength = postBytes.Length;

			using (Stream requestStream = request.GetRequestStream ()) {
				requestStream.Write (postBytes, 0, postBytes.Length);
			}
			Logger.LogInfo ("CreateRequestForPOST() url: " + url + " postBytes length:"+postBytes.Length);
			return request;
		}

		private void StartConnectionAsync(HttpWebRequest request) {
			this.request = request;

			IAsyncResult result = (IAsyncResult) request.BeginGetResponse(asynchronousResult => 
				{
					FramePusher pusher = (FramePusher)asynchronousResult.AsyncState;
					pusher.AsyncResponseRetrieved(asynchronousResult);
				},
				this);

			ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, (state, timeout) =>
				{
					FramePusher pusher = (FramePusher)state;
					if (timeout)
					{
						Logger.LogWarning("TIMEOUT!!");
						if (pusher.request != null) pusher.request.Abort();
						pusher.Stop();
					}
				},
				request, timeout: TimeSpan.FromSeconds(4), executeOnlyOnce: true);
		}

		private void AsyncResponseRetrieved(IAsyncResult asynchronousResult) {
			Logger.LogInfo("AsyncResponseRetrieved() called.");
			try {
				response = (HttpWebResponse) request.EndGetResponse(asynchronousResult);
			} catch (Exception e) {
				Logger.LogError ("Exception in AsyncResponseRetrieved: "+e);
				// better to clean up (on the main thread).
				return;
			}
			Logger.LogInfo ("response status: [" + response.StatusCode + "]" + response.StatusDescription);
			Stream stream = response.GetResponseStream ();
			bstream = new BufferedStream (stream, bufsize);
			mjpgReader = new MjpegReader2(bstream, OnFrameComplete);

			thread = new Thread (ReadThread);
			thread.Priority = System.Threading.ThreadPriority.AboveNormal;
			thread.Start ();
		}

		private void OnFrameComplete(MemoryStream imageBytes) {
			Logger.LogVerbose ("PushFrame imageBytes length: " + (int)imageBytes.Length);
			lock (this) {
				if (frameQueue != null) {
					byte[] buf = frameQueue.GetDataBufferWithContents (imageBytes.GetBuffer (), (int)imageBytes.Length);
					frameQueue.Push (buf);
					Logger.LogVerbose ("added frame. count = " + frameQueue.Count);
				}
			}
			Thread.Sleep (2); // １フレーム完成後少し休む（てきとう）
		}

		private void ReadThread() {
			var onDisconnect = this.onDisconnect; // スレッド開始時のonDisconnectをスレッド終了まで使う。
			try {
				if (response == null) {
					// syncならばStartConnectionする
					StartConnection();
				}
				if (mjpgReader != null) {
					mjpgReader.Run();
				} else {
					Logger.LogError("mjpg Reader was NULL!");
				}
			} catch (System.Exception e) {
				if (e is EndOfStreamException) {
					eof = true;
				} else {
					failed = true;
				}
				Debug.LogError ("Exception in ReadThread ("+url+"): "+e);
				if (e.Message.Contains("Aborted")) {
					Logger.LogInfo("Let's sleep a bit more for connection abort.");
					Thread.Sleep (5000);
				}
			} finally {
				Stop ();
				if (onDisconnect != null) {
					onDisconnect (this);
				}
			}
		}

		public void Stop() {
			if (request != null) {
				//request.Abort ();
				request = null;
			}
			if (mjpgReader != null) {
				mjpgReader.Stop ();
				mjpgReader = null;
			}
			if (response != null) {
				response.Close ();
				response = null;
			}
			if (thread != null) {
				//thread.Abort ();
				thread = null;
			}
			lock (this) {
				frameQueue = null;
			}
		}

		public bool Failed {
			get {
				return failed;
			}
		}

		public bool IsEOF {
			get {
				return eof;
			}
		}

		public bool Playing {
			get {
				return mjpgReader != null && mjpgReader.IsLooping && !failed && !eof;
			}
		}
	}

	class DelaylessFrameDecoder {
		private TurboJpegDecoderBuffer workingDecoderBuffer = new TurboJpegDecoderBuffer();
		private TurboJpegDecoderBuffer completedDecoderBuffer = new TurboJpegDecoderBuffer();
		private Thread thread;
		private bool requestTermination = false;
		private AutoResetEvent eventToDecode = new AutoResetEvent(false);
		private FrameQueue frameQueue;

		public DelaylessFrameDecoder(FrameQueue _queue) {
			frameQueue = _queue;
		}

		public void Start() {
			Logger.LogInfo ("DelaylessFrameDecoder.Start() called.");
			thread = new Thread (Run);
			thread.Start ();
		}

		public void Stop() {
			Logger.LogInfo  ("DelaylessFrameDecoder.Stop() called.");
			frameQueue = null;
			requestTermination = true;
		}

		private void Run() {
			while (!requestTermination) {
				eventToDecode.WaitOne (1000);
				TryDecodeFrame();
			}
		}

		public void RequestDecode() {
			eventToDecode.Set();
		}

		private void TryDecodeFrame() {
			if (frameQueue != null) {
				var frameBuf = frameQueue.Pop ();
				Logger.LogVerbose((frameBuf == null ? "no frame to consume." : "frame consumed.") + "framesCount : " + frameQueue.Count);
				if (frameBuf != null) {
					Decode (frameBuf);
					frameQueue.Pool (frameBuf);
				}
			}
		}

		private bool Decode(byte[] frameBuf) {
			TurboJpegDecoder decoder = new TurboJpegDecoder (frameBuf, workingDecoderBuffer);
			if (!workingDecoderBuffer.GuaranteeBuf (decoder)) {
				decoder.Close ();
				Logger.LogWarning  ("TurboJpegDecoder GuaranteeBuf failed.");
				return false;
			}
			decoder.Decode (workingDecoderBuffer.Buffer);
			decoder.Close ();
			swapBuffers ();
			return true;
		}

		private void swapBuffers() {
			lock (this) {
				var buf = workingDecoderBuffer;
				workingDecoderBuffer = completedDecoderBuffer;
				completedDecoderBuffer = buf;
			}
		}

		public delegate void ProcessFrameBufferFunc(byte[] data, int width, int height);

		public void ComsumeFrame(ProcessFrameBufferFunc func) {
			lock (this) {
				if (completedDecoderBuffer.Width == 0) {
					func (null, 0, 0);
				} else {
					func (completedDecoderBuffer.Buffer, completedDecoderBuffer.Width, completedDecoderBuffer.Height);
					completedDecoderBuffer.Reset ();
				}
			}
		}
	}
}