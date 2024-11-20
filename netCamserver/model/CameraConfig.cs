using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CameraServer.model
{
    public class CameraConfig
    {
        public List<CameraItem>? cameras;
    }

    public class CameraItem
    {
        public int camera_index;
        public string? camera_name;
        public int width;
        public int height;
        public int rotate;
        public bool flip;
    }
}
