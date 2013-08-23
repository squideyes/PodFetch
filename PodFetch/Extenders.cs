using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace PodFetch
{
    public static class Extenders
    {
        private static ConsoleColor[] colors =
            new ConsoleColor[] {ConsoleColor.White,  ConsoleColor.Cyan, 
                ConsoleColor.Yellow, ConsoleColor.Green, 
                ConsoleColor.Magenta, ConsoleColor.Magenta, ConsoleColor.Red};

        private static object logLock = new object();

        public static void HandleCompletion(
            this IDataflowBlock source, params IDataflowBlock[] targets)
        {
            source.Completion.ContinueWith(
                task =>
                {
                    foreach (var target in targets)
                    {
                        if (task.IsFaulted)
                            target.Fault(task.Exception);
                        else
                            target.Complete();
                    }
                });
        }

        public static void EnsurePathExists(this string fileName)
        {
            var path = Path.GetDirectoryName(fileName);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public static async Task Download(this Uri uri, string fileName)
        {
            fileName.EnsurePathExists();

            var webStream = await new HttpClient().
                GetStreamAsync(uri);

            using (var fileStream = File.OpenWrite(fileName))
                await webStream.CopyToAsync(fileStream);
        }

        public static void Log(this Status status, string format, params object[] args)
        {
            lock (logLock)
            {
                Console.ForegroundColor = colors[(int)status];

                Console.WriteLine("{0,-10} {1}",status, string.Format(format, args));
            }
        }
    }
}
