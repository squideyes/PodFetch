#region Copyright, Author Details and Related Context
//<notice lastUpdateOn="8/23/2013">
//  <assembly>PodFetch</assembly>
//  <description>A simple "Astronomy Picture Of The Day" (APOD) downloader that leverages TPL Dataflow</description>
//  <copyright>
//    Copyright (C) 2013 Louis S. Berman

//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.

//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.

//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see http://www.gnu.org/licenses/.
//  </copyright>
//  <author>
//    <fullName>Louis S. Berman</fullName>
//    <email>louis@squideyes.com</email>
//    <website>http://squideyes.com</website>
//  </author>
//</notice>
#endregion 

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
                switch (status)
                {
                    case Status.Scraping:
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        break;
                    case Status.Fetching:
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        break;
                    case Status.Scraped:
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        break;
                    case Status.Fetched:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case Status.NoImage:
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        break;
                    case Status.DupImage:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        break;
                    case Status.BadFetch:
                    case Status.BadScrape:
                    case Status.BadGetUrls:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }

                Console.WriteLine("{0,-10} {1}", status, string.Format(format, args));
            }
        }
    }
}
