using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Wrapper.FFmpeg;
using CameraServer.model;
using FFmpeg.AutoGen;
using Newtonsoft.Json;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace CameraServer
{
    public class CameraService
    {
        private static string CONFIG_PATH = @"Config";
        private static string CONFIG_FILENAME = @"CameraInfo.json";
        private VideoCaptureFFmpeg video_capture = null;
        private int camera_index;
        private string camera_name;
        private int camera_width = 0;
        private int camera_height = 0;
        private int camera_rotate = 0;
        private bool camera_flip = false;

        public bool is_active { get; private set; }

        public CameraService(int camIndex)
        {
            camera_index = camIndex;
            camera_name = get_camera_name_from_config(camIndex);
#if !TEST
            video_capture = new VideoCaptureFFmpeg();
            video_capture.OnBrokenNotify += Video_capture_OnBrokenNotify;
#endif
        }

        private void Video_capture_OnBrokenNotify()
        {
            Console.WriteLine();
            LogControl.WriteLog(LogLevel.Error, string.Format(">> Broken camera"));
            LogControl.WriteLog(LogLevel.Error, string.Format("  + Forcely cleansing camera instance."));
            Stop();
            LogControl.WriteLog(LogLevel.Error, string.Format("  + Starting camera recovery mode."));
            do_open_camera(1, false);
        }

        private string get_camera_name_from_config(int camIndex)
        {
            var config = load_from_json_file();

            if (config == null)
                return String.Empty;
#if IndexName
            var camera_item = config.cameras.FirstOrDefault(c => c.camera_index == camIndex);//이름순
#else
			var camera_item = config.cameras.ElementAt(camIndex);//하드웨어순
#endif
            camera_width = camera_item.width;
            camera_height = camera_item.height;
            camera_rotate = camera_item.rotate;
            camera_flip = camera_item.flip;

            return camera_item.camera_name;
        }

        private static CameraConfig load_from_json_file()
        {
            var camera_json = read_from_json_file();

            return camera_json != String.Empty ?
                JsonConvert.DeserializeObject<CameraConfig>(camera_json) :
                null;
        }

        private static string read_from_json_file()
        {
            string config_filename = System.IO.Path.Combine(
                Environment.CurrentDirectory, CONFIG_PATH, CONFIG_FILENAME);

            LogControl.WriteLog(LogLevel.Information, string.Format(">> config: {0}", config_filename));

            if (!System.IO.File.Exists(config_filename))
            {
                LogControl.WriteLog(LogLevel.Error, string.Format("[ERROR] FILE NOT FOUND: {0}", config_filename));

                return String.Empty;
            }

            StreamReader sr = new StreamReader(config_filename);

            string json = string.Empty;

            if(!sr.EndOfStream)
                json = sr.ReadToEnd();

            sr.Close();

            return json;
        }

        public bool Start()
        {
            if (is_active )
                return true;

            if (camera_name == string.Empty)
                return false;

            video_capture.SetResolution(camera_width, camera_height);

            do_open_camera(5);

            return is_active;
        }

        private void do_open_camera(int interval, bool is_wait_done=true)
        {
            var init_camera_thread = new Thread(() =>
            {
                LogControl.WriteLog(LogLevel.Information, ">> Starting camera                                                   ");
                LogControl.WriteLog(LogLevel.Information, string.Format("  + camera_index : {0}", camera_index));
                LogControl.WriteLog(LogLevel.Information, string.Format("  + camera_name  : {0}", camera_name));
                LogControl.WriteLog(LogLevel.Information, string.Format("  + resolution   : {0}x{1}", camera_width, camera_height));

                bool can_message = true;
                while (!is_active)
                {
                    is_active = video_capture.Open(adjust_camera_name(camera_name));

                    if (!is_active)
                    {
                        if (can_message)
                        {
                            LogControl.WriteLog(LogLevel.Warning, string.Format(">>>> Camera is not attached to system. waiting for {0} seconds to attach its camera.", interval));
                            can_message = false;
                        }
                        Thread.Sleep(interval * 1000);
                    }
                    else
                    {
                        Console.WriteLine();
                        LogControl.WriteLog(LogLevel.Information, string.Format("<< Starting camera : succeeded"));
                        LogControl.WriteLog(LogLevel.Information, "-----------------------------------------------------------------");
                    }

                    if (WindowUtils.IsCancellationRequested)
                        break;
                }
            });

            init_camera_thread.Start();

            if (is_wait_done)
                init_camera_thread.Join();
        }

        private static string adjust_camera_name(string name)
        {
            return name.Contains("rtsp") ? name :
				RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "@device_pnp_\\\\?\\" + name + "\\global":
                                                                       name;
		}

		public void Stop()
        {
            is_active = false;

            video_capture.Close();
        }

        internal static void CreateCameraInfo()
        {
            var video_capture = new VideoCaptureFFmpeg();
            var cameras = video_capture.GetUSBCameraList();
			int nextIndex = 2; // 나머지 카메라의 시작 index

			Console.WriteLine("-------------------------------------------------------------");
            Console.WriteLine("  >>>>> make config file for cameras information on system.");

            CameraConfig config = new CameraConfig();
            config.cameras = new List<CameraItem>();

			foreach (var cam in cameras)
            {
                CameraItem item = new CameraItem();
                item.camera_index = config.cameras.Count;
                item.camera_name = cam.Value.Replace("@device_pnp_\\\\?\\", "").Replace("\\global", "");
				item.width = 1280;
                item.height = 720;
                item.rotate = 0;

#if IndexName
                if (cam.Key.Contains("USB Camera0"))//(item.camera_name.Contains("vid_32e4") && item.camera_name.Contains("pid_9101"))//rgb
				{
					item.camera_index = 0;
				}
				else if (cam.Key.Contains("USB Camera1")) //(item.camera_name.Contains("vid_32e4") && item.camera_name.Contains("pid_9102"))//ir
				{
					item.camera_index = 1;
				}
				else
				{
					item.camera_index = nextIndex;
					nextIndex++;
				}
#endif

				Console.WriteLine(string.Format("    ++++++++ camera[{0}]: {1}: {2}", item.camera_index, cam.Key, item.camera_name));
				config.cameras.Add(item);
            }

            string config_path = System.IO.Path.Combine(Environment.CurrentDirectory, CONFIG_PATH);

            if (!System.IO.Directory.Exists(config_path))
                System.IO.Directory.CreateDirectory(config_path);

            write_to_json_file(JsonConvert.SerializeObject(config, Formatting.Indented));

            Console.WriteLine(string.Format("  <<<<< DONE: {0} cameras", config.cameras.Count));
            Console.WriteLine("-------------------------------------------------------------");
        }

        private static void write_to_json_file(string json)
        {
            string config_filename = System.IO.Path.Combine(
                Environment.CurrentDirectory, CONFIG_PATH, CONFIG_FILENAME);

            StreamWriter sw = new StreamWriter(config_filename);
            sw.Write(json);
            sw.Flush();
            sw.Close();
        }

        internal static void MakeSampleImageAllDevices()
        {
            Console.WriteLine("-------------------------------------------------------------");
            Console.WriteLine("  >>>>> save sample image from all cameras on system.");

            string config_filename = System.IO.Path.Combine(
                Environment.CurrentDirectory, CONFIG_PATH, CONFIG_FILENAME);

            if (!System.IO.File.Exists(config_filename))
                CreateCameraInfo();

            var config = load_from_json_file();

            string saved_path = @"saved";
            if(!System.IO.Directory.Exists(saved_path))
                System.IO.Directory.CreateDirectory(saved_path);

            var v_capture = new VideoCaptureFFmpeg();
            foreach (var item in config.cameras)
            {
                var camera_index = item.camera_index;
                var camera_name = item.camera_name;

                if (v_capture.Open(adjust_camera_name(camera_name)))
                {
                    Mat frame = new Mat();
                    v_capture.Read(frame);
                    var filename = string.Format(@"{0}\{1}_sample.jpg", saved_path, camera_index);
                    if (Cv2.ImWrite(filename, frame))
                        Console.WriteLine(string.Format("    ++++++++ camera[{0}]: {1} saved", item.camera_index, filename));

                    v_capture.Close();
                }
            }

            Console.WriteLine(string.Format("  <<<<< DONE: {0} cameras", config.cameras.Count));
            Console.WriteLine("-------------------------------------------------------------");
        }

        public Mat GetImage(int rotate)
        {
            using (var src = new Mat())
            {
                if (video_capture.Read(src))
                {
                    var dst = new Mat();

                    if (camera_flip)
                        Cv2.Flip(src, dst, 0);
                    else
                        dst = src.Clone();

                    if (camera_rotate == 90 || camera_rotate == 180 || camera_rotate == 270)
                        dst = do_rotate_image(camera_rotate, dst);

                    if (rotate == 90 || rotate == 180 || rotate == 270)
                        dst = do_rotate_image(rotate, dst);

                    return dst;
                }
            }

            return null;
        }

        private Mat do_rotate_image(int rotate, Mat image)
        {
            if (rotate == 0)
                return image.Clone();

            RotateFlags rotate_flag =
                rotate == 90 ? RotateFlags.Rotate90Clockwise :
                rotate == 180 ? RotateFlags.Rotate180 : RotateFlags.Rotate90Counterclockwise;

            var tmp = new Mat();
            Cv2.Rotate(image, tmp, rotate_flag);

            return tmp.Clone();
        }
    }
}
