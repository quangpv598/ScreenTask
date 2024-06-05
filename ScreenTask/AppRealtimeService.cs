using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
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

            Trace.Listeners.Clear();
            string appLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
            TextWriterTraceListener twtl = new TextWriterTraceListener(appLogPath);
            twtl.Name = "TextLogger";
            twtl.TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime;

            Trace.Listeners.Add(twtl);

            Trace.AutoFlush = true;

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
                _ = _screenTask.StartCaptureScreenAsync();
            });

            //Task.Run(async () =>
            //{
            //    _screenRecorder = new ScreenRecorder(_screenTask.CurrentSettings);
            //    await _screenRecorder.RunAsync();
            //});
        }
    }
}
