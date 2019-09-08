//
// MJStreamingPlayer for Unity
//
// Copyright (c) Makoto Hamanaka All Rights Reserved.
//

using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System;

public class TurboJpegDecoderBuffer {
	private byte[] tmpBuf;
	private int width;
	private int height;

	public bool GuaranteeBuf(TurboJpegDecoder decoder) {
		int bufsize = decoder.GetBufSize (); // actually decode header here.
		if (bufsize == 0) {
			Reset ();
			return false;
		}
		width = decoder.width;
		height = decoder.height;
		GuaranteeBufSize (bufsize);
		return true;
	}

	public void Reset() {
		width = 0;
		height = 0;
	}

	public void GuaranteeBufSize(int size) {
		if (tmpBuf == null || tmpBuf.Length < size) {
			tmpBuf = new byte[(int)(size * 1.2)];
		}
	}

	public byte[] Buffer {
		get { return tmpBuf; }
	}

	public int Width {
		get { return width; }
	}

	public int Height {
		get { return height; }
	}
}

public class TurboJpegDecoder {
	private IntPtr handle;
	private int _width = 0;
	private int _height = 0;
	private GCHandle pinnedArraySrc;
	private IntPtr srcPtr;
	private int srcLength;
	private bool initialized = false;
	private TurboJpegDecoderBuffer buffer;

	public TurboJpegDecoder(byte[] srcBytes, TurboJpegDecoderBuffer _buffer = null) {
		buffer = _buffer;
		if (buffer == null) {
			buffer = new TurboJpegDecoderBuffer ();
		}
		handle = LibJpegTurboBridge.tjInitDecompress ();
		pinnedArraySrc = GCHandle.Alloc(srcBytes, GCHandleType.Pinned);
		srcPtr = pinnedArraySrc.AddrOfPinnedObject();
		srcLength = srcBytes.Length;
		initialized = true;
	}
	
	~TurboJpegDecoder() {
		Close();
	}
	
	public int GetBufSize() {
		int ret = LibJpegTurboBridge.tjDecompressHeader(handle, srcPtr, (UInt32) srcLength, ref _width, ref _height);
		if (ret != 0) return 0;
		return EstimatedBufSize;
	}
	public bool Decode(byte[] destBuf) {
		if (width == 0 && height == 0) {
			GetBufSize();
		}
		if (destBuf.Length < EstimatedBufSize) {
			return false;
		}
		int ret = 0;
		GCHandle pinnedArrayDest = GCHandle.Alloc(destBuf, GCHandleType.Pinned);
		IntPtr destPtr = pinnedArrayDest.AddrOfPinnedObject ();
		ret = LibJpegTurboBridge.tjDecompress2 (handle, srcPtr, 
			(UInt32) srcLength, 
			destPtr, 0, 0, 0, 
			(int)LibJpegTurboBridge.TJPF.TJPF_RGB, 
			LibJpegTurboBridge.TJFLAG_FASTDCT + LibJpegTurboBridge.TJFLAG_BOTTOMUP + LibJpegTurboBridge.TJFLAG_NOREALLOC);
		pinnedArrayDest.Free();
		return (ret == 0);
	}
	public void Close() {
		if (initialized) {
			pinnedArraySrc.Free ();
			LibJpegTurboBridge.tjDestroy (handle);
		}
		_width = 0;
		_height = 0;
		initialized = false;
	}
	
	public int EstimatedBufSize {
		get { return (_width+4) * (_height+4) * 4; }
	}

	public int width {
		get { return _width; }
	}

	public int height {
		get { return _height; }
	}

	public static bool TurboAvailable {
		#if UNITY_ANDROID && !UNITY_EDITOR
		get { return true; }
		#else
		get { return true; }
		#endif
	}
}
