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
            public string Title { get; set; }
        }

        static void Main(string[] args)
        {
            Console.SetWindowSize(120, 25);

            AsyncContext.Run(() => Fetch());
        }

        public static async void Fetch()
        {
            var startOn = DateTime.UtcNow;

            var imageRegex = new Regex("(?<=<IMG\\sSRC=\").*?(?=\"\\s.*?>)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

            var cts = new CancellationTokenSource();

            var scraper = new TransformBlock<Link, Link>(
                async link =>
                {
                    try
                    {
                        Status.Scraping.Log(link.PageUri.AbsoluteUri);

                        var html = await new HttpClient().GetStringAsync(link.PageUri);

                        var src = imageRegex.Match(html).Value;

                        if (string.IsNullOrWhiteSpace(src))
                            return null;

                        link.ImageUri = new Uri(Properties.Settings.Default.BaseUri, src);

                        Status.Scraped.Log(link.PageUri.AbsoluteUri);

                        return link;
                    }
                    catch (Exception error)
                    {
                        Status.BadFetch.Log("SCRAPE: " + error.Message);

                        return null;
                    }
                },
                new ExecutionDataflowBlockOptions()
                {
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = 1 //DataflowBlockOptions.Unbounded
                });

            var fetcher = new ActionBlock<Link>(
               async link =>
               {
                   try
                   {
                       var fileName = Path.Combine(Properties.Settings.Default.SaveToPath, 
                           Path.GetFileName(link.ImageUri.AbsoluteUri)).ToLower();

                       if (File.Exists(fileName))
                       {
                           Status.DupImage.Log(Path.GetFileName(fileName));

                           return;
                       }

                       var nameOnly = Path.GetFileName(fileName);

                       Status.Fetching.Log("{0:MM/dd/yyyy} - {1}", link.Date, nameOnly);

                       await link.ImageUri.Download(fileName);

                       Status.Fetched.Log("{0:MM/dd/yyyy} - {1}", link.Date, nameOnly);
                   }
                   catch (Exception error)
                   {
                       Status.BadFetch.Log(error.Message);
                   }
               },
               new ExecutionDataflowBlockOptions()
               {
                   CancellationToken = cts.Token,
                   MaxDegreeOfParallelism = 8 //DataflowBlockOptions.Unbounded
               });

            scraper.LinkTo(fetcher, uri => uri != null);
            scraper.LinkTo(DataflowBlock.NullTarget<Link>());

            scraper.HandleCompletion(fetcher);

            Status.Info.Log("Fetching APOD's archive list");

            try
            {
                var links = await GetLinks();

                Status.Info.Log("Parsed {0:N0} URIs", links.Count);

                links = links.Take(Properties.Settings.Default.MaxToFetch).ToList();

                Status.Info.Log("Queued {0:N0} images to be downloaded", links.Count);

                links.ForEach(link => scraper.Post(link));
            }
            catch (Exception error)
            {
                Status.BadFetch.Log("GETURLS:" + error.Message);
            }

            Console.ReadKey();
        }

        private static async Task<List<Link>> GetLinks()
        {
            const string PATTERN = "(?<DATE>\\d{4}\\s.*?\\d{2}):\\s*? <a\\shref=\"(?<PAGE>ap\\d{6}.html)\">(?<TITLE>.*?)</a>";

            var html = await new HttpClient().GetStringAsync(Properties.Settings.Default.ArchiveUri);

            var regex = new Regex(PATTERN, RegexOptions.CultureInvariant | RegexOptions.Compiled);

            var links = new List<Link>();

            foreach (Match match in regex.Matches(html))
            {
                links.Add(new Link()
                {
                    Date = DateTime.Parse(match.Groups[1].Value),
                    PageUri = new Uri(Properties.Settings.Default.BaseUri + match.Groups[2].Value),
                    Title = match.Groups[3].Value
                });
            }

            return links;
        }
    }
}