using Nancy;
using Nancy.Hosting.Self;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CameraServer
{
    public class SupportedService
    {
        public delegate string service_func(DynamicDictionary query);

        public string name;
        public service_func? service;
    }

    public class WebServer
    {
        private const int MAX_WIDTH = 720;
        private const int MAX_HEIGHT = 960;
        private static string filenameFaceCascade = @"data\haarcascades\haarcascade_frontalface_alt.xml";
        private HostConfiguration host_config;
        private Uri uri;
        private NancyHost host;
        private static CameraService _camera_service;
        private static CascadeClassifier _faceCascade = new CascadeClassifier(filenameFaceCascade);
        private Dictionary<string, SupportedService> supported_services = new Dictionary<string, SupportedService>()
        {
            { "status", new SupportedService() { name = "status", service = do_status } },
            { "camera", new SupportedService() { name = "camera", service = do_camera_service } },
            { "takephoto", new SupportedService() { name = "takephoto", service = do_takephoto_service } },
        };

        public WebServer(CameraService camera_service, int service_port)
        {
            _camera_service = camera_service;

            host_config = new HostConfiguration { UrlReservations = new UrlReservations() { CreateAutomatically = true } };
            uri = new Uri(string.Format("http://127.0.0.1:{0}", service_port));

            WebServiceModule.OnGetRequest += WebServiceModule_OnGetRequest;
        }

        private string WebServiceModule_OnGetRequest(string uri, DynamicDictionary query)
        {
            return has_uri(uri) ? run_service(uri, query) : string.Format("PAGE-NOT-FOUND: {0}", uri);
        }

        private bool has_uri(string uri)
        {
            return supported_services.ContainsKey(uri);
        }

        private string run_service(string uri, DynamicDictionary query)
        {
            var service = supported_services[uri];

            return service.service(query);
        }

        private static string do_status(DynamicDictionary query)
        {
            var ret =
                string.Format(
                    @"{{""status"":{0}}}",
                    _camera_service.is_active ? "true" : "false"
                );

            return ret;
        }

        private static string do_camera_service(DynamicDictionary query)
        {
            int rotate = query.ContainsKey("rotate") ? query["rotate"] : 0;
            string encoded_base64 = string.Empty;

            using (var frame = _camera_service.GetImage(rotate))
            {
                encoded_base64 = image_to_base64string(frame);
                LogControl.WriteLog(LogLevel.Information, string.Format("REQ: camera, RESP: length={0}                      ", encoded_base64.Length));
            };

            return encoded_base64;
        }

        private static string image_to_base64string(Mat frame)
        {
            if(frame == null)
                return string.Empty;

            byte[] buf;
            Cv2.ImEncode(".jpg", frame, out buf);

            return Convert.ToBase64String(buf);
        }

        private static string do_takephoto_service(DynamicDictionary query)
        {
            int rotate = query.ContainsKey("rotate") ? query["rotate"] : 0;
            Mat? dst = null;

            using (var frame = _camera_service.GetImage(rotate))
            {
                if (frame != null)
                {
                    dst = new Mat();

                    var faces = _faceCascade.DetectMultiScale(frame);
                    if (faces.Length > 0)
                    {
                        int idx = 0;
                        for (int i = 0; i < faces.Length; i++)
                        {
                            if (faces[idx].Width <= faces[i].Width)
                                idx = i;
                        }

                        // 얼굴인식 영영보다 margin만큼 크게 잡아 가로 세로 크기를 구한다음 3:4 비율로 crop 하기 위해 센터를 구하고 rect를 다시 계산한다.
                        int margin = 200;
                        double w = faces[idx].Width + margin > MAX_WIDTH ? MAX_WIDTH : faces[idx].Width + margin;
                        double h = faces[idx].Height + margin > MAX_HEIGHT ? MAX_HEIGHT : faces[idx].Height + margin;

                        if (w / h < 0.75)
                            h = w * 1.33333;
                        else
                            w = h * 0.75;

                        int cx = faces[idx].X + faces[idx].Width / 2;
                        int cy = faces[idx].Y + faces[idx].Height / 2;
                        int x = cx - (int)(w / 2);
                        int y = cy - (int)(h / 2);

                        if (w > 0 && h > 0)
                        {
                            int dx = (x + (int)w) > frame.Width ? (x + (int)w) - frame.Width : 0;
                            int dy = (y + (int)h) > frame.Height ? (y + (int)h) - frame.Height : 0;

                            dst = frame[new Rect(x-dx, y-dy, (int)(w)-dx, (int)(h)-dy)];
                        }
                        else
                            dst = frame[new OpenCvSharp.Rect(0, 0, frame.Width, frame.Height)];
                    }
                    else
                        dst = frame[new OpenCvSharp.Rect(0, 0, frame.Width, frame.Height)];

                    Cv2.Resize(dst, dst, new Size(MAX_WIDTH, MAX_HEIGHT), 0, 0);
                }
            };

            var encoded_base64 = image_to_base64string(dst);

            LogControl.WriteLog(LogLevel.Information, string.Format("REQ: takephoto, RESP: length={0}                  ", encoded_base64.Length));

            return encoded_base64;
        }

        public bool Start()
        {
            try
            {
                host = new NancyHost(host_config, uri);
                host.Start();
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        public void Stop()
        {
            host.Stop();
            host.Dispose();
        }
    }
}
