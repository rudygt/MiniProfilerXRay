using System;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;
using StackExchange.Profiling;

namespace MiniProfilerXRay.ConsoleSample
{    
    public class Program
    {
        private static void Main(string[] args)
        {
            var prewarm = JsonConvert.SerializeObject((object)10);

            MiniProfiler.DefaultOptions.Storage = args.Length == 2
                ? new XRayMiniprofilerStorage(args[1])
                : new XRayMiniprofilerStorage("192.168.99.100:2000", "ConsoleApp");

            var mp = MiniProfiler.StartNew("TestApp");

            using (mp.Step("Level 1"))
            {
                using (mp.Step("Level 1.1"))
                {
                    Thread.Sleep(50);
                }

                using (mp.Step("Level 1.2"))
                {
                    using (var xrayAnnotation = mp.StartXRayAnnotations())
                    {
                        Thread.Sleep(50);

                        xrayAnnotation.AddXRayAnnotation("data", 10);
                    }
                }
            }

            using (mp.Step("Level 2"))
            {
                using (mp.Step("Level 2.1"))
                {
                    Thread.Sleep(50);
                }

                using (mp.Step("Level 2.2"))
                {
                    Thread.Sleep(50);
                }
            }

            mp.Stop();

            Console.WriteLine(mp.RenderPlainText());

            if (Debugger.IsAttached)
                Console.ReadLine();

            Console.WriteLine("done");
        }
    }
}