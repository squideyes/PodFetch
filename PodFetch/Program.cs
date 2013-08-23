using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
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
        private static ConcurrentDictionary<string, bool> urls =
            new ConcurrentDictionary<string, bool>();

        static void Main(string[] args)
        {
            Console.SetWindowSize(120, 25);

            AsyncContext.Run(() => Fetch());
        }

        public static async void Fetch()
        {
            var imageRegex = new Regex("(?<=<IMG\\sSRC=\").*?(?=\"\\s.*?>)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline |
                RegexOptions.CultureInvariant | RegexOptions.Compiled);

            var cts = new CancellationTokenSource();

            var scraper = new TransformBlock<Uri, Uri>(
                async uri =>
                {
                    try
                    {
                        var html = await new HttpClient().GetStringAsync(uri);

                        return new Uri(Properties.Settings.Default.BaseUri,
                            imageRegex.Match(html).Value);
                    }
                    catch (Exception error)
                    {
                        Status.Error.Log("SCRAPE: " + error.Message);

                        return null;
                    }
                },
                new ExecutionDataflowBlockOptions()
                {
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded
                });

            var fetcher = new ActionBlock<Uri>(
               async uri =>
               {
                   try
                   {
                       var fileName = Path.Combine(
                           Properties.Settings.Default.SaveToPath,
                           Path.GetFileName(uri.AbsoluteUri));

                       var nameOnly = Path.GetFileName(fileName);

                       if (File.Exists(fileName))
                       {
                           Status.DupImage.Log(nameOnly);

                           return;
                       }

                       await uri.Download(fileName);

                       Status.Fetch.Log(nameOnly);
                   }
                   catch (Exception error)
                   {
                       Status.Error.Log(error.Message);
                   }
               },
               new ExecutionDataflowBlockOptions()
               {
                   CancellationToken = cts.Token,
                   MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded
               });

            scraper.LinkTo(fetcher, uri => uri != null);
            scraper.LinkTo(DataflowBlock.NullTarget<Uri>());

            scraper.HandleCompletion(fetcher);

            Status.Info.Log("Fetching APOD's archive list");

            try
            {
                var uris = await GetUris();

                Status.Info.Log("Parsed {0:N0} URIs", uris.Count);

                uris = uris.Take(Properties.Settings.Default.MaxToFetch).ToList();

                Status.Info.Log("Queued {0:N0} images to be downloaded", uris.Count);
            }
            catch (Exception error)
            {
                Status.Info.Log("Fetching APOD's archive list");
            }


























            //scraper.Post(new Link(Properties.Settings.Default.BaseUri.AbsoluteUri,
            //    LinkKind.Html, Properties.Settings.Default.BaseUri));

            //Status.Info.Log("Crawling...");

            //Console.ReadKey();

            //cts.Cancel();

            //Status.Info.Log("Stopping...");

            //try
            //{
            //    await Task.WhenAll(
            //        scraper.Completion,
            //        parser.Completion,
            //        fetcher.Completion);
            //}
            //catch (OperationCanceledException)
            //{
            //    Status.Info.Log("Stopped!");
            //}
            //catch (Exception error)
            //{
            //    Status.Error.Log(error.Message);
            //}

            Console.WriteLine();
            Console.Write("Press any key to continue...");

            Console.ReadKey();
        }

        private static async Task<List<Uri>> GetUris()
        {
            const string PATTERN =
                "(?<DATE>\\d{4}\\s.*?\\d{2}):\\s*? <a\\shref=\"(?<PAGE>ap\\d{6}.html)\">(?<TITLE>.*?)</a>";

            var html = await new HttpClient().GetStringAsync(Properties.Settings.Default.ArchiveUri);

            var regex = new Regex(PATTERN,
                RegexOptions.CultureInvariant | RegexOptions.Compiled);

            var uris = new List<Uri>();

            foreach (Match match in regex.Matches(html))
                uris.Add(new Uri(Properties.Settings.Default.BaseUri + match.Groups[2].Value));

            return uris;
        }
    }
}
