using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenCvSharp;
using FFmpeg.AutoGen;
using System.Linq;
//using System.Runtime.Remoting.Messaging;

namespace API.Wrapper.FFmpeg
{
    public sealed unsafe class VideoStreamDecoder : IDisposable
    {
        private AVCodecContext* _pCodecContext;
        private AVFormatContext* _pFormatContext;
        private AVFrame* _pFrame;
        private AVPacket* _pPacket;
        private AVFrame* _receivedFrame;
        private int _streamIndex;
        private bool is_opened = false;
        private string _codec_name;
        private Size _frame_size;
        private Size _resolution;
        private AVPixelFormat _pixel_format;
        private Dictionary<string, string> usb_camera_list = new Dictionary<string, string>();

        public VideoStreamDecoder()
        {
            ffmpeg.avdevice_register_all();
        }

        private void init()
        {
            _frame_size = new Size(0, 0);

            _pFormatContext = ffmpeg.avformat_alloc_context();
            _receivedFrame = ffmpeg.av_frame_alloc();
        }

        private void shutdown()
        {
            Dispose();
            is_opened = false;
        }

        public bool Connect(string camera_name, AVHWDeviceType hw_device_type, bool is_usb)
        {
            is_opened = false;

            if (_pFormatContext == null)
                init();

            var ret_val = is_usb ?
                init_usb_web_cam(camera_name, hw_device_type) :
                init_network_cam(camera_name, hw_device_type);

            if (ret_val < 0)
            {
                Dispose();
                
                return false;
            }

            if (ffmpeg.avformat_find_stream_info(_pFormatContext, null) < 0)
                return false;

            AVCodec* codec = null;
            _streamIndex = ffmpeg.av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0);

            if (_streamIndex < 0)
                return false;

            _pCodecContext = ffmpeg.avcodec_alloc_context3(codec);

            if (hw_device_type != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                ffmpeg.av_hwdevice_ctx_create(&_pCodecContext->hw_device_ctx, hw_device_type, null, null, 0)
                    .ThrowExceptionIfError();
            }

            if (ffmpeg.avcodec_parameters_to_context(_pCodecContext, _pFormatContext->streams[_streamIndex]->codecpar) >= 0)
            {
                ffmpeg.avcodec_open2(_pCodecContext, codec, null).ThrowExceptionIfError();

                _codec_name = ffmpeg.avcodec_get_name(codec->id);
                _frame_size = new Size(_pCodecContext->width, _pCodecContext->height);
                _pixel_format = _pCodecContext->pix_fmt;

                if (_resolution.Width == 0)
                    _resolution.Width = _frame_size.Width;
                if (_resolution.Height == 0)
                    _resolution.Height = _frame_size.Height;

                _pPacket = ffmpeg.av_packet_alloc();
                _pFrame = ffmpeg.av_frame_alloc();

                is_opened = ret_val == 0;
            }

            return is_opened;
        }

        private string get_mapped_camera_name(string camera_name)
        {
#if WINDOWS
            return camera_name.Contains("@device_pnp") ?
                    camera_name :
                    usb_camera_list.TryGetValue(camera_name, out var mapped_camera_name) ?
                        mapped_camera_name : camera_name;
#else
            return camera_name;
#endif
        }

        public void Disconnect()
        {
            if (!is_opened)
                return;

            shutdown();
        }

        private int init_usb_web_cam(string camera_name, AVHWDeviceType HWDeviceType)
        {
            if (camera_name.IndexOf("video") == -1)
                camera_name = "video=" + camera_name;

            return open_usb_camera(camera_name);
        }

        private int open_usb_camera(string camera_name)
        {
            var pFormatContext = _pFormatContext;

            // for webcam
            AVInputFormat* iformat = ffmpeg.av_find_input_format(FFmpeg.input_format_string);
            return ffmpeg.avformat_open_input(&pFormatContext, camera_name, iformat, null);
        }

        private int init_network_cam(string camera_name, AVHWDeviceType HWDeviceType)
        {
            AVDictionary* opts;
            ffmpeg.av_dict_set(&opts, "probesize", "4096", 0);
            ffmpeg.av_dict_set(&opts, "max_probe_packets", "64", 0);
            ffmpeg.av_dict_set(&opts, "rtsp_transport", "tcp", 0);
            ffmpeg.av_dict_set(&opts, "flush_packets", "1", 0);
            ffmpeg.av_dict_set(&opts, "fflags", "nobuffer", 0);
            ffmpeg.av_dict_set(&opts, "analyzeduration", "250000", 0);
            ffmpeg.av_dict_set(&opts, "flags", "low_delay", 0);
            ffmpeg.av_dict_set(&opts, "max_delay", "5000000", 0);
            ffmpeg.av_dict_set(&opts, "buffer_size", "400000000", 0);
            ffmpeg.av_dict_set(&opts, "timeout", "5000000", 0);    // microseconds. 5 seconds

            var pFormatContext = _pFormatContext;
            return ffmpeg.avformat_open_input(&pFormatContext, camera_name, null, &opts).ThrowExceptionIfError();
        }

        public string CodecName { get { return _codec_name; } }
        public Size FrameSize { get { return _frame_size; } }
        public Size Resolution { get { return _resolution; } }
        public AVPixelFormat PixelFormat { get { return _pixel_format; } }
        public bool IsOpened()
        {
            return is_opened;
        }
        public void Dispose()
        {
            if (_pFrame != null)
            {
                var pFrame = _pFrame;
                ffmpeg.av_frame_free(&pFrame);

                _pFrame = null;
            }

            if (_pPacket != null)
            {
                var pPacket = _pPacket;
                ffmpeg.av_packet_free(&pPacket);

                _pPacket = null;
            }

            if (_pCodecContext != null)
            {
                var pCodecContext = _pCodecContext;
                ffmpeg.avcodec_close(_pCodecContext);
                ffmpeg.avcodec_free_context(&pCodecContext);

                _pCodecContext = null;
            }

            if (_pFormatContext != null)
            {
                var pFormatContext = _pFormatContext;
                if (is_opened)
                {
                    ffmpeg.avformat_close_input(&pFormatContext);
                    ffmpeg.avformat_free_context(pFormatContext);
                }

                _pFormatContext = null;
            }
        }

        public bool TryDecodeNextFrame(out AVFrame frame)
        {
            ffmpeg.av_frame_unref(_pFrame);
            ffmpeg.av_frame_unref(_receivedFrame);
            int error;

            do
            {
                try
                {
                    do
                    {
                        ffmpeg.av_packet_unref(_pPacket);
                        error = ffmpeg.av_read_frame(_pFormatContext, _pPacket);

                        if (error != 0)
                        {
                            frame = *_pFrame;
                            return false;
                        }
                    } while (_pPacket->stream_index != _streamIndex);

                    if(ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket) != 0)
                    {
                        ffmpeg.av_packet_unref(_pPacket);

                        frame = *_pFrame;
                        return false;
                    }
                }
                finally
                {
                    ffmpeg.av_packet_unref(_pPacket);
                }

                error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
            }
            while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            if (error < 0)
            {
                ffmpeg.av_packet_unref(_pPacket);
                frame = *_pFrame;
                return false;
            }

            if (_pCodecContext->hw_device_ctx != null)
            {
                ffmpeg.av_hwframe_transfer_data(_receivedFrame, _pFrame, 0);
                frame = *_receivedFrame;
            }
            else
                frame = *_pFrame;

            return true;
        }

        public IReadOnlyDictionary<string, string> GetContextInfo()
        {
            AVDictionaryEntry* tag = null;
            var result = new Dictionary<string, string>();

            while ((tag = ffmpeg.av_dict_get(_pFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                result.Add(key, value);
            }

            return result;
        }

        public Dictionary<string, string> GetUSBCameraList()
        {
            refresh_usb_cameras();

            return usb_camera_list;
        }

        private void refresh_usb_cameras()
        {
            usb_camera_list.Clear();

            AVInputFormat* iformat = ffmpeg.av_find_input_format(FFmpeg.input_format_string);
            AVDeviceInfoList* device_list = null;
            if (ffmpeg.avdevice_list_input_sources(iformat, null, null, &device_list) >= 0)
            {
                Console.WriteLine("sources: {0}, default: {1}", device_list->nb_devices, device_list->default_device);

                for (int idx = 0; idx < device_list->nb_devices; idx++)
                {
                    var device_name = Marshal.PtrToStringAnsi((IntPtr)device_list->devices[idx]->device_name);
                    var device_desc = Marshal.PtrToStringAnsi((IntPtr)device_list->devices[idx]->device_description);

                    Console.WriteLine("name={0}, desc={1}", device_name, device_desc);

                    AVMediaType device_type =
                        check_device_type(
                            device_list->devices[idx]->nb_media_types,
                            device_list->devices[idx]->media_types,
                            device_name,
                            device_desc);

                    if (device_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                        add_to_list(device_desc, device_name);
                }
            }
            ffmpeg.avdevice_free_list_devices(&device_list);
        }

        private AVMediaType check_device_type(int nb_media_types, AVMediaType* media_types, string? device_name, string? device_desc)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                for (int i = 0; i < nb_media_types; i++)
                        {
                    if (media_types[i] == AVMediaType.AVMEDIA_TYPE_VIDEO)
                        return AVMediaType.AVMEDIA_TYPE_VIDEO;
                        }
                    }
            else
            {
                var ret_val = init_usb_web_cam(device_name, AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
                var pFormatContext = _pFormatContext;
                ffmpeg.avformat_close_input(&pFormatContext);

                if (ret_val == 0)
                    return AVMediaType.AVMEDIA_TYPE_VIDEO;
                }

            return AVMediaType.AVMEDIA_TYPE_UNKNOWN; ;
        }

        private void add_to_list(string device_desc, string device_name)
        {
            int device_cnt = 0;
            foreach (var item in usb_camera_list)
            {
                if (item.Key.IndexOf(device_desc) != -1)
                    device_cnt++;
            }

            if (device_cnt > 0)
                device_desc = string.Format("{0}#{1}", device_desc, device_cnt);
            usb_camera_list.Add(device_desc, device_name);
        }

        internal void SetResolution(int width, int height)
        {
            if (_resolution.Width != width || _resolution.Height != height)
                _resolution = new Size(width, height);
        }
    }
}