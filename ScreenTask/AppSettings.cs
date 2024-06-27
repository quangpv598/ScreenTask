﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeBroker
{
    [Serializable]
    public class AppSettings
    {
        public string CreateComputerHost { get; set; }
        public string VideoHost { get; set; }
        public string ImageHost { get; set; }
        public int FrameHeight { get; set; }
        public int FrameWidth { get; set; }
        public int VideoDuration { get; set; } // seconds
        public int ScreenshotsSpeed { get; set; }
        public int ImageQuality { get; set; }
        public int Port { get; set; }
        public string IP { get; set; }
        public bool AllowPublicAccess { get; set; }
        public string ComputerName { get; set; }
        public string EmployeeName { get; set; }
    }
}
