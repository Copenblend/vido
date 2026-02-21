using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using Vido.Core.Playback;

namespace Vido.Services.Video;

/// <summary>
/// Converts decoded FFmpeg AVFrames to BGRA32 pixel data for WPF rendering.
/// Uses swscale for color space conversion.
/// </summary>
internal sealed unsafe class FrameConverter : IDisposable
{
    private SwsContext* _swsContext;
    private byte_ptr4 _dstData;
    private int4 _dstLineSize;
    private int _srcWidth;
    private int _srcHeight;
    private AVPixelFormat _srcFormat;
    private int _dstWidth;
    private int _dstHeight;
    private byte[]? _buffer;
    private bool _disposed;

    /// <summary>
    /// Configures the converter for the specified source format and dimensions.
    /// Called once when a video is loaded or when dimensions change.
    /// </summary>
    public void Configure(int srcWidth, int srcHeight, AVPixelFormat srcFormat)
    {
        // Normalize deprecated YUVJ* formats to their non-deprecated equivalents.
        // These legacy formats embed full-range (JPEG) in the pixel format enum;
        // modern FFmpeg expects the base format + color_range metadata instead.
        // Without this, sws_getContext emits: "deprecated pixel format used, make sure you did set range correctly"
        srcFormat = NormalizePixelFormat(srcFormat);

        if (_srcWidth == srcWidth && _srcHeight == srcHeight && _srcFormat == srcFormat)
            return;

        Cleanup();

        _srcWidth = srcWidth;
        _srcHeight = srcHeight;
        _srcFormat = srcFormat;
        _dstWidth = srcWidth;
        _dstHeight = srcHeight;

        _swsContext = ffmpeg.sws_getContext(
            srcWidth, srcHeight, srcFormat,
            _dstWidth, _dstHeight, AVPixelFormat.AV_PIX_FMT_BGRA,
            2 /* SWS_BILINEAR */, null, null, null);

        if (_swsContext == null)
            throw new InvalidOperationException("Failed to create swscale context.");

        // Allocate destination buffer
        var bufferSize = ffmpeg.av_image_get_buffer_size(
            AVPixelFormat.AV_PIX_FMT_BGRA, _dstWidth, _dstHeight, 1);

        _buffer = new byte[bufferSize];

        fixed (byte* pBuffer = _buffer)
        {
            ffmpeg.av_image_fill_arrays(
                ref _dstData, ref _dstLineSize,
                pBuffer,
                AVPixelFormat.AV_PIX_FMT_BGRA,
                _dstWidth, _dstHeight, 1);
        }
    }

    /// <summary>
    /// Converts an AVFrame to BGRA32 FrameData.
    /// </summary>
    public FrameData? Convert(AVFrame* frame, TimeSpan pts)
    {
        if (_swsContext == null || _buffer == null)
            return null;

        fixed (byte* pBuffer = _buffer)
        {
            // Refresh destination pointers (buffer may have moved if GC compacted)
            ffmpeg.av_image_fill_arrays(
                ref _dstData, ref _dstLineSize,
                pBuffer,
                AVPixelFormat.AV_PIX_FMT_BGRA,
                _dstWidth, _dstHeight, 1);

            // Convert frame data/linesize to arrays for sws_scale
            var srcData = new byte*[] { frame->data[0], frame->data[1], frame->data[2], frame->data[3] };
            var srcLineSize = new int[] { frame->linesize[0], frame->linesize[1], frame->linesize[2], frame->linesize[3] };
            var dstData = new byte*[] { _dstData[0], _dstData[1], _dstData[2], _dstData[3] };
            var dstLineSize = new int[] { _dstLineSize[0], _dstLineSize[1], _dstLineSize[2], _dstLineSize[3] };

            ffmpeg.sws_scale(
                _swsContext,
                srcData, srcLineSize,
                0, _srcHeight,
                dstData, dstLineSize);
        }

        // Copy pixel data to a new array for the frame
        var stride = _dstLineSize[0];
        var pixelData = new byte[stride * _dstHeight];
        Buffer.BlockCopy(_buffer, 0, pixelData, 0, pixelData.Length);

        return new FrameData
        {
            PixelData = pixelData,
            Width = _dstWidth,
            Height = _dstHeight,
            Stride = stride,
            Pts = pts
        };
    }

    private void Cleanup()
    {
        if (_swsContext != null)
        {
            ffmpeg.sws_freeContext(_swsContext);
            _swsContext = null;
        }

        _buffer = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            _disposed = true;
        }
    }

    /// <summary>
    /// Maps deprecated YUVJ* pixel formats to their modern equivalents.
    /// The color range information is preserved by the codec context's color_range field.
    /// </summary>
    private static AVPixelFormat NormalizePixelFormat(AVPixelFormat format)
    {
        return format switch
        {
            AVPixelFormat.AV_PIX_FMT_YUVJ420P => AVPixelFormat.AV_PIX_FMT_YUV420P,
            AVPixelFormat.AV_PIX_FMT_YUVJ422P => AVPixelFormat.AV_PIX_FMT_YUV422P,
            AVPixelFormat.AV_PIX_FMT_YUVJ444P => AVPixelFormat.AV_PIX_FMT_YUV444P,
            AVPixelFormat.AV_PIX_FMT_YUVJ440P => AVPixelFormat.AV_PIX_FMT_YUV440P,
            _ => format
        };
    }
}
