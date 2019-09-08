using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System;

public class LibJpegTurboBridge {
	
	public enum TJPF
	{
		/**
   * RGB pixel format.  The red, green, and blue components in the image are
   * stored in 3-byte pixels in the order R, G, B from lowest to highest byte
   * address within each pixel.
   */
		TJPF_RGB=0,
		/**
   * BGR pixel format.  The red, green, and blue components in the image are
   * stored in 3-byte pixels in the order B, G, R from lowest to highest byte
   * address within each pixel.
   */
		TJPF_BGR,
		/**
   * RGBX pixel format.  The red, green, and blue components in the image are
   * stored in 4-byte pixels in the order R, G, B from lowest to highest byte
   * address within each pixel.  The X component is ignored when compressing
   * and undefined when decompressing.
   */
		TJPF_RGBX,
		/**
   * BGRX pixel format.  The red, green, and blue components in the image are
   * stored in 4-byte pixels in the order B, G, R from lowest to highest byte
   * address within each pixel.  The X component is ignored when compressing
   * and undefined when decompressing.
   */
		TJPF_BGRX,
		/**
   * XBGR pixel format.  The red, green, and blue components in the image are
   * stored in 4-byte pixels in the order R, G, B from highest to lowest byte
   * address within each pixel.  The X component is ignored when compressing
   * and undefined when decompressing.
   */
		TJPF_XBGR,
		/**
   * XRGB pixel format.  The red, green, and blue components in the image are
   * stored in 4-byte pixels in the order B, G, R from highest to lowest byte
   * address within each pixel.  The X component is ignored when compressing
   * and undefined when decompressing.
   */
		TJPF_XRGB,
		/**
   * Grayscale pixel format.  Each 1-byte pixel represents a luminance
   * (brightness) level from 0 to 255.
   */
		TJPF_GRAY,
		/**
   * RGBA pixel format.  This is the same as @ref TJPF_RGBX, except that when
   * decompressing, the X component is guaranteed to be 0xFF, which can be
   * interpreted as an opaque alpha channel.
   */
		TJPF_RGBA,
		/**
   * BGRA pixel format.  This is the same as @ref TJPF_BGRX, except that when
   * decompressing, the X component is guaranteed to be 0xFF, which can be
   * interpreted as an opaque alpha channel.
   */
		TJPF_BGRA,
		/**
   * ABGR pixel format.  This is the same as @ref TJPF_XBGR, except that when
   * decompressing, the X component is guaranteed to be 0xFF, which can be
   * interpreted as an opaque alpha channel.
   */
		TJPF_ABGR,
		/**
   * ARGB pixel format.  This is the same as @ref TJPF_XRGB, except that when
   * decompressing, the X component is guaranteed to be 0xFF, which can be
   * interpreted as an opaque alpha channel.
   */
		TJPF_ARGB,
		/**
   * CMYK pixel format.  Unlike RGB, which is an additive color model used
   * primarily for display, CMYK (Cyan/Magenta/Yellow/Key) is a subtractive
   * color model used primarily for printing.  In the CMYK color model, the
   * value of each color component typically corresponds to an amount of cyan,
   * magenta, yellow, or black ink that is applied to a white background.  In
   * order to convert between CMYK and RGB, it is necessary to use a color
   * management system (CMS.)  A CMS will attempt to map colors within the
   * printer's gamut to perceptually similar colors in the display's gamut and
   * vice versa, but the mapping is typically not 1:1 or reversible, nor can it
   * be defined with a simple formula.  Thus, such a conversion is out of scope
   * for a codec library.  However, the TurboJPEG API allows for compressing
   * CMYK pixels into a YCCK JPEG image (see #TJCS_YCCK) and decompressing YCCK
   * JPEG images into CMYK pixels.
   */
		TJPF_CMYK
	};

	/**
 * The uncompressed source/destination image is stored in bottom-up (Windows,
 * OpenGL) order, not top-down (X11) order.
 */
	public const int TJFLAG_BOTTOMUP =        2;
	/**
 * When decompressing an image that was compressed using chrominance
 * subsampling, use the fastest chrominance upsampling algorithm available in
 * the underlying codec.  The default is to use smooth upsampling, which
 * creates a smooth transition between neighboring chrominance components in
 * order to reduce upsampling artifacts in the decompressed image.
 */
	public const int TJFLAG_FASTUPSAMPLE =  256;
	/**
 * Disable buffer (re)allocation.  If passed to #tjCompress2() or
 * #tjTransform(), this flag will cause those functions to generate an error if
 * the JPEG image buffer is invalid or too small rather than attempting to
 * allocate or reallocate that buffer.  This reproduces the behavior of earlier
 * versions of TurboJPEG.
 */
	public const int TJFLAG_NOREALLOC =     1024;
	/**
 * Use the fastest DCT/IDCT algorithm available in the underlying codec.  The
 * default if this flag is not specified is implementation-specific.  For
 * example, the implementation of TurboJPEG for libjpeg[-turbo] uses the fast
 * algorithm by default when compressing, because this has been shown to have
 * only a very slight effect on accuracy, but it uses the accurate algorithm
 * when decompressing, because this has been shown to have a larger effect.
 */
	public const int TJFLAG_FASTDCT =       2048;
	/**
 * Use the most accurate DCT/IDCT algorithm available in the underlying codec.
 * The default if this flag is not specified is implementation-specific.  For
 * example, the implementation of TurboJPEG for libjpeg[-turbo] uses the fast
 * algorithm by default when compressing, because this has been shown to have
 * only a very slight effect on accuracy, but it uses the accurate algorithm
 * when decompressing, because this has been shown to have a larger effect.
 */
	public const int TJFLAG_ACCURATEDCT =   4096;


	// 型の変換はここを参照 > https://msdn.microsoft.com/ja-jp/library/fzhhdwae.aspx 「プラットフォーム呼び出しによるデータのマーシャリング」

	// libjpeg-turboの関数はこちらを参照 >  https://github.com/openstf/android-libjpeg-turbo/blob/master/jni/vendor/libjpeg-turbo/libjpeg-turbo-1.4.1/turbojpeg.h

	[DllImport("jpeg-turbo")]
	public static extern IntPtr tjInitDecompress();
	[DllImport("jpeg-turbo")]
	public static extern void tjDestroy(IntPtr handle);

	// DLLEXPORT int DLLCALL tjDecompress2(tjhandle handle, unsigned char *jpegBuf, unsigned long jpegSize, 
	//   unsigned char *dstBuf, int width, int pitch, int height, int pixelFormat, int flags);
	[DllImport("jpeg-turbo")]
	public static extern int tjDecompress2(IntPtr handle, IntPtr jpegBuf, UInt32 jpegSize, IntPtr dstBuf, int width, int pitch, int height, int pixelFomat, int flags);

	//DLLEXPORT int DLLCALL tjDecompressHeader(tjhandle handle,
	//   unsigned char *jpegBuf, unsigned long jpegSize, int *width, int *height);
	[DllImport("jpeg-turbo")]
	public static extern int tjDecompressHeader(IntPtr HandleRef, IntPtr jpegBuf, UInt32 jpegSize, ref int width, ref int height);

	[DllImport("jpeg-turbo")]
	public static extern Int32 tjBufSizeYUV(int width, int height, int subsamp);
}
