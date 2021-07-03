using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace Mdl.Web.Scraper.Ui.ConsoleApp
{
    [Command(Name = "scraper - wget", Description = "A simple scraper")]
    [HelpOption("-h|--help")]
    public class Program
    {
        [Option("-u|--url <URL>", "The Url to query", CommandOptionType.MultipleValue)]
        public string[] Urls { get; } = new string[0];

        [Option("-i|--input-file <PATH>", "The path to a file with Urls", CommandOptionType.SingleValue)]
        public string FileUrls { get; }

        [Option("-w|--wait <TIME>", "The delay between two query (in milliseconds). Default: 0", CommandOptionType.SingleValue)]
        public int Delay { get; } = 0;

        [Option("-o|--output <PATH>", "The output directory. Default: .", CommandOptionType.SingleValue)]
        public string OutputPath { get; } = ".";


        static Task<int> Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args);

        private async Task<int> OnExecuteAsync(CommandLineApplication app,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(FileUrls) && !Urls.Any())
            {
                await Console.Out.WriteLineAsync("No work, either url or file was provided.");

                return 0;
            }

            var workId = Guid.NewGuid();

            var urls = new List<string>();

            urls.AddRange(
                Urls.Any()
                    ? Urls.Where(ValidateUrl)
                    : (await LoadUrlsFromFileAsync(FileUrls)).Where(ValidateUrl)
            );

            var max = urls.Count;


            using var client = new HttpClient();

            string logPath = Path.Combine(OutputPath, $"{workId.ToString()}.log");
            await using StreamWriter log = File.CreateText(logPath);
            await log.WriteLineAsync($"Start work: {workId.ToString()}");

            int i = 0;

            foreach (string url in urls)
            {
                i++;
                await Console.Out.WriteLineAsync($"process: {i}/{max} {url}");
                await log.WriteLineAsync($"process: {i}/{max} {url}");

                await Task.Delay(TimeSpan.FromMilliseconds(Delay), cancellationToken);

                var response = await client.GetAsync(url, cancellationToken);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string readAsStringAsync = await response.Content.ReadAsStringAsync(cancellationToken);
                    await using var handle = File.CreateText(Path.Combine(OutputPath, $"{workId.ToString()}.{i}.html"));
                    await handle.WriteAsync(readAsStringAsync);
                }
                else
                {
                    await log.WriteLineAsync($"error: {i}/{max} {response.StatusCode}");
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            return 0;
        }

        private async Task<string[]> LoadUrlsFromFileAsync(string path)
        {
            if (!File.Exists(path))
            {
                throw new ApplicationException("File not found.");
            }

            return await File.ReadAllLinesAsync(path);
        }

        private bool ValidateUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri))
            {
                return validatedUri.Scheme == Uri.UriSchemeHttp || validatedUri.Scheme == Uri.UriSchemeHttps;
            }

            return false;
        }
    }
}