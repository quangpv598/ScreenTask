using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace AppRealtime
{
    partial class AppRealtimeService : ServiceBase
    {
        private ScreenRecorder _screenRecorder;
        private ScreenTask _screenTask;

        public AppRealtimeService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Start(args);
        }

        protected override void OnStop()
        {
            //_screenRecorder.Stop();
        }

        public void Start(string[] args)
        {
            _screenTask = new ScreenTask();
            _screenTask.LoadSettings();

            Task.Run(() =>
            {
                _ = _screenTask.StartTaskAsync();
            });
            Task.Run(async () =>
            {
                _screenRecorder = new ScreenRecorder(_screenTask.CurrentSettings);
                await _screenRecorder.RunAsync();
            });
        }
    }
}
