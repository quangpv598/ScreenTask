using System;
using System.ServiceProcess;

namespace WindowsSecurityHealthService
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if (!DEBUG)
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new SecurityHealthService()
            };
            ServiceBase.Run(ServicesToRun);
#else
            SecurityHealthService service = new SecurityHealthService();
            service.OnStartManually(args);
            Console.ReadLine();
#endif
        }
    }
}
