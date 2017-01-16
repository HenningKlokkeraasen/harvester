using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Harvester
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var result = Run().Result;
        }

        private static async Task<bool> Run()
        {
            var selected = 0;
            while (selected != 6)
            {
                PrintMenu();
                var line = Console.ReadLine();
                int.TryParse(line, out selected);

                switch (selected)
                {
                    case 1:
                        await DownloadResource();
                        break;
                    case 2:
                        await ExtractData();
                        break;
                    case 3:
                        await DownloadMultipleResources();
                        break;
                    case 4:
                        await ExtractDataFromMultipleFiles();
                        break;
                    case 5:
                        RenameFiles();
                        break;
                    case 6:
                        Console.WriteLine("Kbai");
                        break;
                }
            }
            return true;
        }

        private static void PrintMenu()
        {
            Console.WriteLine("Menu");
            Console.WriteLine("1) Download resource");
            Console.WriteLine("2) Extract data");
            Console.WriteLine("3) Download multiple resources");
            Console.WriteLine("4) Extract data from multiple files");
            Console.WriteLine("5) Rename files");
            Console.WriteLine("6) Exit");
        }

        private static async Task DownloadResource()
        {
            var uri = AskForSomethingAndGetIt("URI (e.g. http://google.com): ");
            var path = AskForSomethingAndGetIt("Path (e.g. C:\\temp\\file.html): ");

            try
            {
                await Harvester.Harvest(path, uri);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.WriteLine("Done");
        }

        private static async Task ExtractData()
        {
            var readPath = AskForSomethingAndGetIt("Path to read from (e.g. C:\\temp\\file.html): ");
            var savePath = AskForSomethingAndGetIt("Path to save to (e.g. C:\\temp\\extract.html): ");
            var selector = AskForSelector;
            var attr = AskForSomethingAndGetIt("Attribute (e.g. href): ");
            var prefix = AskForSomethingAndGetIt("Prefix for each line: ");

            try
            {
                await Extractor.Extract(readPath, savePath, selector, attr, prefix);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.WriteLine("Done");
        }

        private static async Task DownloadMultipleResources()
        {
            var readPath = AskForSomethingAndGetIt("Path to read from - one URI per line (e.g. C:\\temp\\resources.txt): ");
            var savePath = AskForSomethingAndGetIt("Folder to save to (e.g. C:\\temp\\files\\): ");
            var interval = AskForInterval;

            var readAllLines = File.ReadAllLines(readPath);
            foreach (var line in readAllLines)
            {
                Console.WriteLine(line);
                try
                {
                    Thread.Sleep(interval * 1000);
                    await Harvester.Harvest(savePath + FileSaver.MakeValidFileName(line), line);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            Console.WriteLine("Done");
        }

        private static async Task ExtractDataFromMultipleFiles()
        {
            var readPath = AskForFolder;
            var savePath = AskForSomethingAndGetIt("Folder to save to (e.g. C:\\temp\\files\\output\\): ");
            var selector = AskForSelector;
            var extractions = AskForExtractions;

            var files = Directory.EnumerateFiles(readPath);
            foreach (var file in files)
            {
                Console.WriteLine(file);
                var fileName = file.Split('\\').LastOrDefault();
                try
                {
                    await Extractor.Extract(readPath + fileName, savePath + fileName, selector, extractions, ",");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            Console.WriteLine("Done");
        }

        private static void RenameFiles()
        {
            var readPath = AskForFolder;
            var from = AskForSomethingAndGetIt("Replace from: ");
            var to = AskForSomethingAndGetIt("Replace to: ");
            var files = Directory.EnumerateFiles(readPath);
            foreach (var file in files)
            {
                var newName = file.Replace(from, to);
                File.Move(file, newName);
            }
            Console.WriteLine("Done");
        }

        private static List<ChildExtraction> AskForExtractions
        {
            get
            {
                var extractionsText = AskForSomethingAndGetIt(
                    "Child-extraction: ChildNumberZeroBased|AttributesToAppendCommaSeparated;... (e.g. 0|;1|href,alt;): ");
                var extractions = new List<ChildExtraction>();
                try
                {
                    var attrs = extractionsText.Split(';');
                    foreach (var attr in attrs)
                    {
                        var choices = attr.Split('|');
                        var childNumberText = choices[0];
                        int childNumber;
                        int.TryParse(childNumberText, out childNumber);
                        var attributesToAppend = choices[1];
                        extractions.Add(new ChildExtraction
                        {
                            ChildNumberZeroBased = childNumber,
                            AttributesToAppend = attributesToAppend.Split(',')
                        });
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                return extractions;
            }
        }

        private static int AskForInterval
        {
            get
            {
                int interval;
                var intervalInput = AskForSomethingAndGetIt("Interval in seconds (e.g. 1): ");
                int.TryParse(intervalInput, out interval);
                return interval;
            }
        }

        private static string AskForSelector => AskForSomethingAndGetIt("Html Agility Pack selector (e.g. //div[@id='div1']): ");

        private static string AskForFolder => AskForSomethingAndGetIt("Folder to read from (e.g. C:\\temp\\files\\): ");

        private static string AskForSomethingAndGetIt(string something)
        {
            Console.Write(something);
            return Console.ReadLine();
        }
    }

    internal class ChildExtraction
    {
        internal int ChildNumberZeroBased { get; set; }
        internal string[] AttributesToAppend { get; set; }
    }

    internal static class Extractor
    {
        internal static async Task Extract(string readPath, string savePath, string selector, string attr, string prefix)
        {
            var doc = new HtmlDocument();
            doc.Load(readPath);
            using (var file = File.Create(savePath))
            {
                foreach (var node in doc.DocumentNode.SelectNodes(selector))
                {
                    var text = node.Attributes[attr].Value;
                    var value = prefix + text + "\n";
                    await file.WriteAsync(Encoding.ASCII.GetBytes(value), 0, value.Length);
                }
            }
        }

        internal static async Task Extract(string readPath, string savePath, string selector, IList<ChildExtraction> extractions, string separator)
        {
            var doc = new HtmlDocument();
            doc.Load(readPath);
            using (var file = File.Create(savePath))
            {
                foreach (var node in doc.DocumentNode.SelectNodes(selector))
                {
                    var sb = new StringBuilder();
                    var subNodes = node.ChildNodes.Where(n => !(n is HtmlTextNode)).ToList();
                    foreach (var extraction in extractions)
                    {
                        var subNode = subNodes[extraction.ChildNumberZeroBased];
                        var text = subNode.InnerText;
                        if (extraction.AttributesToAppend != null)
                        {
                            foreach (var s in extraction.AttributesToAppend.Where(s => !string.IsNullOrEmpty(s)))
                            {
                                var attribute = subNode.Attributes[s];
                                if (attribute != null)
                                    text += "\\" + attribute.Value;
                            }
                        }

                        sb.Append(text + separator);
                    }
                    var value = sb + "\n";
                    await file.WriteAsync(Encoding.ASCII.GetBytes(value), 0, value.Length);
                }
            }
        }
    }

    internal static class Harvester
    {
        internal static async Task Harvest(string path, string uri)
        {
            var stream = await new WebClient().OpenReadTaskAsync(uri);
            await FileSaver.Save(path, stream);
        }
    }
    
    internal static class FileSaver
    {
        internal static async Task Save(string path, Stream sourceStream)
        {
            using (var destinationStream = File.Create(path))
                await sourceStream.CopyToAsync(destinationStream);
        }

        /// <summary>
        /// http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
        /// </summary>
        internal static string MakeValidFileName(string name)
        {
            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(name, invalidRegStr, "_");
        }
    }
}
