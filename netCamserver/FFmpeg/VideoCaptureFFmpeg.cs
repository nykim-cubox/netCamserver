﻿using FFmpeg.AutoGen;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
#if (TARGET_AMD64 || TARGET_X86)
    using System.Drawing;
    using System.Drawing.Imaging;
#endif

namespace API.Wrapper.FFmpeg
{
    public delegate void OnBrokenNotify();

    public class VideoCaptureFFmpeg : IDisposable
    {
        private Thread grabber_thread = null;
        private ThreadStart grabber_thread_start;
        private bool active_thread = false;

        private VideoStreamDecoder ffmpeg_decoder = null;
        private bool is_opened = false;

        private object current_frame_lock = new object();
        private Mat current_frame = null;
#if (TARGET_AMD64 || TARGET_X86)
		private object current_bitmap_lock = new object();
		private Bitmap current_bitmap = null;
#endif

        public event OnBrokenNotify OnBrokenNotify = null;
        public int FrameWidth { get { return ffmpeg_decoder.Width; } }
        public int FrameHeight { get { return ffmpeg_decoder.Height; } }

        public VideoCaptureFFmpeg()
        {
            FFmpegBinariesHelper.RegisterFFmpegBinaries();

            grabber_thread_start = new ThreadStart(decode_all_frames_to_image);

			set_up_logging();

			ffmpeg_decoder = new VideoStreamDecoder();
		}

        private unsafe void set_up_logging()
        {
            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_TRACE);// (ffmpeg.AV_LOG_SKIP_REPEATED);// (ffmpeg.AV_LOG_ERROR);

			// do not convert to local function
			av_log_set_callback_callback logCallback = (p0, level, format, vl) =>
            {
				if (level > ffmpeg.av_log_get_level()) return;

                var lineSize = 1024;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;
                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                var line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(line);
                Console.ResetColor();
            };

			ffmpeg.av_log_set_callback(logCallback);
        }

        public void Dispose()
        {
            stop_thread();

            if (current_frame != null)
                current_frame.Dispose();

            destroy_ffmpeg();
        }

        private void stop_thread()
        {
            active_thread = false;
            if (grabber_thread != null && grabber_thread.IsAlive)
                grabber_thread.Join();
            grabber_thread = null;

            ffmpeg_decoder.Disconnect();
        }

        private void destroy_ffmpeg()
        {
            if (ffmpeg_decoder != null)
                ffmpeg_decoder.Dispose();
            ffmpeg_decoder = null;
        }

        public bool IsOpened()
        {
            return is_opened;
        }

        public bool Open(string cameraName)
        {
            clear_temporary_images();

            is_opened = ffmpeg_decoder.Connect(cameraName, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE, cameraName.IndexOf("rtsp") == -1);
            if (is_opened)
            {
                active_thread = true;

                grabber_thread = new Thread(grabber_thread_start);
                grabber_thread.Start();

                if (grabber_thread.IsAlive)
                {
                    while (!wait_for_first_frame())
                        Thread.Sleep(10);
                }
                else
                {
                    active_thread = false;

                    ffmpeg_decoder.Disconnect();
                    is_opened = false;
                }
            }

            return is_opened;
        }

        private void clear_temporary_images()
        {
            lock (current_frame_lock)
            {
                if (current_frame != null)
                    current_frame.Dispose();
                current_frame = null;
            }
#if (TARGET_AMD64 || TARGET_X86)
            lock (current_frame_lock)
            {
				if (current_bitmap != null)
				current_bitmap.Dispose();
				current_bitmap = null;
			}
#endif
		}

		private bool wait_for_first_frame()
        {
            lock (current_frame_lock)
            {
                return current_frame != null;
            }
        }

        public void Close()
        {
            if (!is_opened)
                return;

            stop_thread();
        }

        private void decode_all_frames_to_image()
        {
            var info = ffmpeg_decoder.GetContextInfo();
            info.ToList().ForEach(x => Console.WriteLine($"{x.Key} = {x.Value}"));

            var sourceSize = ffmpeg_decoder.FrameSize;
            var sourcePixelFormat = ffmpeg_decoder.PixelFormat;
            var destinationSize = ffmpeg_decoder.Resolution;
            var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
            var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat);

            bool valid_frame = true;
            bool back_valid = true;
            while (active_thread)
            {
                if (ffmpeg_decoder.TryDecodeNextFrame(out var frame))
                {
                    // noize reduction
                    if (frame.decode_error_flags != 0 && frame.flags == 2)
                        valid_frame = false;
                    else
                    if (frame.decode_error_flags == 0 && frame.flags == 2)
                        valid_frame = true;
                    else
                    if (frame.decode_error_flags == 12 && frame.flags == 0)
                    {
                        back_valid = valid_frame;
                        valid_frame = false;
                    }

                    if (valid_frame)
                    {
                        var convertedFrame = vfc.Convert(frame);

                        update_current_frame(vfc.ToCvMat(convertedFrame));
                        update_current_frame(convertedFrame);
                    }
                    //else
                    {
                        Console.WriteLine(
                            $"number: {frame.coded_picture_number}, timestamp: {frame.best_effort_timestamp}, decode_error: {frame.decode_error_flags}, flags: {frame.flags}"
						);
						try
						{
                            Console.SetCursorPosition(0, Console.CursorTop - 1);
                        }
                        catch
                        {
                        }
                    }

                    if (frame.decode_error_flags == 12 && frame.flags == 0)
                        valid_frame = back_valid;
                }
                else
                {
                    active_thread = false;

                    Task.Run(() =>
                    {
                        OnBrokenNotify?.Invoke();
                    });
                }

                Thread.Sleep(1);
            }
        }

        private void update_current_frame(Mat mat)
        {
            lock (current_frame_lock)
            {
                if (current_frame != null)
                    current_frame.Dispose();

                current_frame = mat;
            }
        }

		private unsafe void update_current_frame(AVFrame frame)
		{
#if (TARGET_AMD64 || TARGET_X86)
            lock (current_bitmap_lock)
            {
                if (current_bitmap != null)
                    current_bitmap.Dispose();

                current_bitmap = new Bitmap
                                    (
                                        frame.width,
                                        frame.height,
                                        frame.linesize[0],
                                        PixelFormat.Format24bppRgb,
                                        (IntPtr)frame.data[0]
                                    );
            }
#endif
				}

		private static AVPixelFormat get_hw_pixel_format(AVHWDeviceType hWDevice)
        {
            AVPixelFormat ret_val = AVPixelFormat.AV_PIX_FMT_NONE;
            switch (hWDevice)
            {
                case AVHWDeviceType.AV_HWDEVICE_TYPE_NONE: ret_val = AVPixelFormat.AV_PIX_FMT_NONE; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU: ret_val = AVPixelFormat.AV_PIX_FMT_VDPAU; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA: ret_val = AVPixelFormat.AV_PIX_FMT_CUDA; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI: ret_val = AVPixelFormat.AV_PIX_FMT_VAAPI; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2: ret_val = AVPixelFormat.AV_PIX_FMT_NV12; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_QSV: ret_val = AVPixelFormat.AV_PIX_FMT_QSV; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX: ret_val = AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA: ret_val = AVPixelFormat.AV_PIX_FMT_NV12; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_DRM: ret_val = AVPixelFormat.AV_PIX_FMT_DRM_PRIME; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL: ret_val = AVPixelFormat.AV_PIX_FMT_OPENCL; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC: ret_val = AVPixelFormat.AV_PIX_FMT_MEDIACODEC; break;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN: throw new NotImplementedException();
            }

            return ret_val;
        }

		public bool Read(Mat frame)
		{
			bool ret = true;

			lock (current_frame_lock)
			{
				if (current_frame == null)
					ret = false;
				else
					current_frame.CopyTo(frame);
			}

			return ret;
		}

#if (TARGET_AMD64 || TARGET_X86)
        public Bitmap Read()
        {
            lock (current_bitmap_lock)
            {
                return new Bitmap(current_bitmap);
            }
        }
#endif

		public void Release()
        {
            Dispose();
        }

        public IReadOnlyDictionary<string, string> GetUSBCameraList()
        {
            if (ffmpeg_decoder == null)
                return new Dictionary<string, string>();

            return ffmpeg_decoder.GetUSBCameraList();
        }

        public void SetResolution(int width, int height)
        {
            ffmpeg_decoder.SetResolution(width, height);
        }
    }
}
