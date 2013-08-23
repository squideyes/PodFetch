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

using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace PodFetch
{
    class Program
    {
        public class Link
        {
            public DateTime Date { get; set; }
            public Uri PageUri { get; set; }
            public Uri ImageUri { get; set; }
        }

        static void Main(string[] args)
        {
            AsyncContext.Run(() => Fetch());
        }

        public static async void Fetch()
        {
            long fetched = 0;
            long skipped = 0;
            long errored = 0;

            var startedOn = DateTime.UtcNow;

            var imageRegex = new Regex("(?<=<IMG\\sSRC=\").*?(?=\"\\s.*?>)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

            List<Link> links;

            try
            {
                links = await GetLinks();

                Status.Info.Log("Parsed {0:N0} URIs", links.Count);

                if (Properties.Settings.Default.MaxToFetch > 0)
                    links = links.Take(Properties.Settings.Default.MaxToFetch).ToList();

                Status.Info.Log("Queued {0:N0} images to be downloaded", links.Count);
            }
            catch (Exception error)
            {
                Status.BadGetUrls.Log(error.Message);

                PressAnyKeyToTerminate();

                return;
            }

            var scraper = new TransformBlock<Link, Link>(
                async link =>
                {
                    try
                    {
                        Status.Scraping.Log(link.PageUri.AbsoluteUri);

                        var html = await new HttpClient().GetStringAsync(link.PageUri);

                        var src = imageRegex.Match(html).Value;

                        if (string.IsNullOrWhiteSpace(src))
                        {
                            Interlocked.Increment(ref skipped);

                            Status.NoImage.Log(link.PageUri.AbsoluteUri);

                            return null;
                        }

                        link.ImageUri = new Uri(Properties.Settings.Default.BaseUri, src);

                        Status.Scraped.Log(link.PageUri.AbsoluteUri);

                        return link;
                    }
                    catch (Exception error)
                    {
                        Interlocked.Increment(ref errored);

                        Status.BadScrape.Log(error.Message);

                        return null;
                    }
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = 2
                });

            var fetcher = new ActionBlock<Link>(
               async link =>
               {
                   try
                   {
                       var fileName = Path.Combine(Properties.Settings.Default.SaveToPath,
                           Path.GetFileName(link.ImageUri.AbsoluteUri).ToLower());

                       var nameOnly = Path.GetFileName(fileName);

                       if (!Properties.Settings.Default.OverwriteAll)
                       {
                           if (File.Exists(fileName))
                           {
                               Interlocked.Increment(ref skipped);

                               Status.DupImage.Log("{0:MM/dd/yyyy} - {1}", link.Date, nameOnly);

                               return;
                           }
                       }

                       Status.Fetching.Log("{0:MM/dd/yyyy} - {1}", link.Date, nameOnly);

                       await link.ImageUri.Download(fileName);

                       Interlocked.Increment(ref fetched);

                       Status.Fetched.Log("{0:MM/dd/yyyy} - {1}", link.Date, nameOnly);
                   }
                   catch (Exception error)
                   {
                       Interlocked.Increment(ref errored);

                       Status.BadFetch.Log(error.Message);
                   }
               },
               new ExecutionDataflowBlockOptions()
               {
                   MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded
               });

            scraper.LinkTo(fetcher, link => link != null);
            scraper.LinkTo(DataflowBlock.NullTarget<Link>());

            scraper.HandleCompletion(fetcher);

            Status.Info.Log("Fetching APOD's archive list");

            links.ForEach(link => scraper.Post(link));

            scraper.Complete();

            try
            {
                await fetcher.Completion;

                Status.Finished.Log("Fetched: {0:N0}, Skipped: {1:N0}, Errors: {2:N0}, Seconds: {3:N2}",
                    fetched, skipped, errored, (DateTime.UtcNow - startedOn).TotalMilliseconds / 1000.0);
            }
            catch (AggregateException errors)
            {
                foreach (var error in errors.InnerExceptions)
                    Status.Failure.Log(error.Message);
            }
            catch (TaskCanceledException)
            {
                Status.Cancelled.Log("The process was manually cancelled!");
            }
            catch (Exception error)
            {
                Status.Failure.Log(error.Message);
            }

            PressAnyKeyToTerminate();
        }

        private static async Task<List<Link>> GetLinks()
        {
            var html = await new HttpClient().GetStringAsync(Properties.Settings.Default.ArchiveUri);

            var regex = new Regex("(?<DATE>\\d{4}\\s.*?\\d{2}):\\s*? <a\\shref=\"(?<PAGE>ap\\d{6}.html)\">.*?</a>",
                RegexOptions.CultureInvariant | RegexOptions.Compiled);

            var links = new List<Link>();

            foreach (Match match in regex.Matches(html))
            {
                links.Add(new Link()
                    {
                        Date = DateTime.Parse(match.Groups[1].Value),
                        PageUri = new Uri(Properties.Settings.Default.BaseUri + match.Groups[2].Value)
                    });
            }

            return links;
        }

        private static void PressAnyKeyToTerminate()
        {
            Console.WriteLine();
            Console.Write("Press any key to terminate the program...");

            Console.ReadKey();
        }
    }
}