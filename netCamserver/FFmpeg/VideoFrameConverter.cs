using System;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using OpenCvSharp;

namespace API.Wrapper.FFmpeg
{
    public sealed unsafe class VideoFrameConverter : IDisposable
    {
        private readonly IntPtr _convertedFrameBufferPtr;
        private readonly Size _destinationSize;
        private readonly byte_ptrArray4 _dstData;
        private readonly int_array4 _dstLinesize;
        private readonly SwsContext* _pConvertContext;

        public VideoFrameConverter(Size sourceSize, AVPixelFormat sourcePixelFormat,
            Size destinationSize, AVPixelFormat destinationPixelFormat)
        {
            _destinationSize = destinationSize;

            _pConvertContext = ffmpeg.sws_getContext(sourceSize.Width,
                sourceSize.Height,
                sourcePixelFormat,
                destinationSize.Width,
                destinationSize.Height,
                destinationPixelFormat,
                ffmpeg.SWS_FAST_BILINEAR,
                null,
                null,
                null);
            if (_pConvertContext == null)
                throw new ApplicationException("Could not initialize the conversion context.");

            var convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(destinationPixelFormat,
                destinationSize.Width,
                destinationSize.Height,
                1);
            _convertedFrameBufferPtr = Marshal.AllocHGlobal(convertedFrameBufferSize);
            _dstData = new byte_ptrArray4();
            _dstLinesize = new int_array4();

            ffmpeg.av_image_fill_arrays(ref _dstData,
                ref _dstLinesize,
                (byte*)_convertedFrameBufferPtr,
                destinationPixelFormat,
                destinationSize.Width,
                destinationSize.Height,
                1);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(_convertedFrameBufferPtr);
            ffmpeg.sws_freeContext(_pConvertContext);
        }

        public AVFrame Convert(AVFrame sourceFrame)
        {
            ffmpeg.sws_scale(_pConvertContext,
                sourceFrame.data,
                sourceFrame.linesize,
                0,
                sourceFrame.height,
                _dstData,
                _dstLinesize);

            var data = new byte_ptrArray8();
            data.UpdateFrom(_dstData);
            var linesize = new int_array8();
            linesize.UpdateFrom(_dstLinesize);

            return new AVFrame
            {
                data = data,
                linesize = linesize,
                width = _destinationSize.Width,
                height = _destinationSize.Height
            };
        }

        public Mat ToCvMat(AVFrame aVFrame)
        {
            var result_mat = new Mat(aVFrame.height, aVFrame.width, MatType.CV_8UC3);
            var src_line_size = aVFrame.linesize[0];
            var dst_line_size = (int)result_mat.Step();
            ffmpeg.av_image_copy_plane((byte*)(void*)result_mat.Data, dst_line_size, (byte*)(void*)aVFrame.data[0], src_line_size, Math.Min(src_line_size, dst_line_size), result_mat.Height);

            return result_mat;

        }
    }
}