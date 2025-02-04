using System.Reflection;
using System.Runtime.InteropServices;
namespace CameraServer
{
	//기능추가1
	class Program
	{
		static void Main(string[] args)
		{
			init();

			if (args.Length == 1)
			{
				if (args[0] == "make")
					make_camera_info_json();
				else
				if (args[0] == "sample")
					make_sample_image();
				else
				{
#if !DEBUG
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    WindowUtils.HideConsole();
#endif
					int camera_index;
					if (Int32.TryParse(args[0], out camera_index))
						run_service(camera_index);
					else
					{
						usage();
					}
				}
			}

#if DEBUG
			Console.WriteLine("Press Any Key to quit                                           ");
			Console.ReadKey(false);
#endif
		}
		private static void init()
		{
			string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

			Console.WriteLine("################################################################");
			Console.WriteLine("                 Starting camera api server");
			Console.WriteLine("                  written by CUBOX Co.Ltd");
			Console.WriteLine("                       Ver " + version);
			Console.WriteLine("################################################################");

			WindowUtils.SetCancelOnConsole();
		}
		private static void make_camera_info_json()
		{
			CameraService.CreateCameraInfo();
		}

		private static void make_sample_image()
		{
			CameraService.MakeSampleImageAllDevices();
		}
		private static void usage()
		{
			Console.WriteLine("usage: CameraServer {device_index}");
			Console.WriteLine("    device_index: number-type   ex) 0");
			Console.WriteLine("");
			Console.WriteLine("*** if you want to make configuration file(JSON), you can use the below command");
			Console.WriteLine("     CameraServer make");
		}

		private static void run_service(int camIndex)
		{
			init_log(camIndex);
			 
			CameraService camera_service = new CameraService(camIndex);
			WebServer ws = new WebServer(camera_service, get_service_port(camIndex));

			if (ws.Start())
			{
				run_ok_message(camIndex);
#if TEST
                WindowUtils.WaitConsoleApplication();
#else
				if (camera_service.Start())
				{
					WindowUtils.WaitConsoleApplication();

					camera_service.Stop();
				}
				else
					LogControl.WriteLog(LogLevel.Error, string.Format("[ERROR] CAMERA NOT FOUND: camera_index={0}", camIndex));
#endif

				ws.Stop();
			}
			else
				LogControl.WriteLog(LogLevel.Error, string.Format("[ERROR] WEB SERVICE IS NOT ACTIVATED: camera_index={0}", camIndex));
		}

		private static void init_log(int camIndex)
		{
			var log_path = System.IO.Path.Combine(Environment.CurrentDirectory, "log");
			var prefix = string.Format("CameraServer_{0}", get_service_port(camIndex));

			LogControl.CreateLogControl(log_path, prefix, LogLevel.Information);
		}

		private static void run_ok_message(int camIndex)
		{
			int service_port = get_service_port(camIndex);

			Console.WriteLine();
			LogControl.WriteLog(LogLevel.Information, ">> Camera endpoints are /status and /camera and /takephoto                                    ");
			LogControl.WriteLog(LogLevel.Information, string.Format("  + GET http://127.0.0.1:{0}/status", service_port));
			LogControl.WriteLog(LogLevel.Information, string.Format("  + GET http://127.0.0.1:{0}/camera", service_port));
			LogControl.WriteLog(LogLevel.Information, string.Format("  + GET http://127.0.0.1:{0}/takephoto", service_port));
			LogControl.WriteLog(LogLevel.Information, "-----------------------------------------------------------------");
			LogControl.WriteLog(LogLevel.Information, ">> Camera API service is up and running.");
			LogControl.WriteLog(LogLevel.Information, "-----------------------------------------------------------------");
			Console.WriteLine("To close service, press Ctrl+C");
		}

		private static int get_service_port(int camIndex)
		{
			return 9000 + camIndex;
		}
	}
}
