using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Core.Models;
using Infrastructure;
using Infrastructure.Handlers;

namespace UI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Engine engine = new Engine();
                Args argsModel = new Args();
                if (args.Length > 0)
                {
                    argsModel = await ArgsHandler.SetPathsFromArgs(args);
                }
                argsModel = await GetArgs(argsModel);
                engine.SetArgs(argsModel);


                engine.reportProgressEvent += progressChanged;
                FinalChannelReport report = await engine.Start();

                var msg = createFinalMessage(ref report);
                Console.Clear();
                Console.WriteLine(msg);

                if (args.Length == 0)
                    Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} \n{ex.StackTrace}");
                Console.ReadLine();
            }

        }

        private static string createFinalMessage(ref FinalChannelReport report)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Finished! { report.WorkingChannelsCount} channels are working out of { report.TotalChannelsCount}");
            if (report.DoublesRemovedCount == 0)
            {
                sb.AppendLine($"No duplicates found to remove");
            }
            else
            {
                sb.AppendLine($"And were removed { report.DoublesRemovedCount} channels becaused were duplicates.");
                sb.AppendLine($"There were { report.TotalChannelsCount + report.DoublesRemovedCount } channels.");
                sb.AppendLine($"Now there are { report.WorkingChannelsCount} channels.");
            }
            return sb.ToString();
        }

        private static void progressChanged(object sender, ProgressReportModel e)
        {
            Console.Clear();
            Console.WriteLine($"Working channels: {e.WorkingChannels.Count}, Not Working: {e.NotWorkingChannelsCount}");
            Console.WriteLine($"Current Nr: {e.NotWorkingChannelsCount + e.WorkingChannels.Count}/{e.ChannelsCountTotal}");
        }

        private static async Task<Args> GetArgs(Args args)
        {
            if (string.IsNullOrEmpty(args.SourcePath))
            {
                Console.WriteLine($"Drang and drop the file Here... or paste the link");

                string srcPath;
                do
                {
                    srcPath = Console.ReadLine();
                } while (String.IsNullOrEmpty(srcPath));
                srcPath = await ArgsHandler.TransformIfIsLink(srcPath);

                args.SourcePath = $@"{srcPath}";
            }

            if (string.IsNullOrEmpty(args.ExportPath))
            {
                var fileName = Path.GetFileName(args.SourcePath).Replace(".m3u", "");
                args.ExportPath = Path.Combine(args.SourcePath.Split(fileName)[0], $"{fileName}-Cleaned.m3u");
            }
            return args;
        }
    }
}
