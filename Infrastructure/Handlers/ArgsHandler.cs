using System;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;

namespace Infrastructure.Handlers
{
    public class ArgsHandler
    {
        private static string[] timeOutArgs = { "-timeout", "--timeout", "-timeOut", "--timeOut", "-to", "--to" };
        private static string[] srcArgs = { "-src", "--src", "-source", "--source" };
        private static string[] destArgs = { "-dest", "--dest", "-destination", "--destination", "-dst", "--dst" };
        private static string[] removeDoublesArgs = { "-rd", "--rd", "-nd", "--nd", "-removeDoubles", "--removeDoubles", "-noDoubles", "--noDoubles" };
        private static string[] removeDoublesResponses = { "false", "False", "f", "0", "no" };

        private static Args _argsModel = new Args();

        public async static Task<Args> SetPathsFromArgs(string[] args)
        {
            try
            {
                var ar = args.ToArray();
                for (int i = 0; i < args.Length; i++)
                {
                    foreach (var arg in timeOutArgs)
                    {
                        if (args[i] == arg)
                        {
                            _argsModel.TimeOut = args[i + 1];
                            break;
                        }
                    }

                    foreach (var arg in srcArgs)
                    {
                        if (args[i] == arg)
                        {
                            _argsModel.SourcePath = await TransformIfIsLink(args[i + 1]);
                            break;
                        }
                    }

                    foreach (var arg in destArgs)
                    {
                        if (args[i] == arg)
                        {
                            _argsModel.ExportPath = args[i + 1];
                            break;
                        }
                    }
                    foreach (var arg in removeDoublesArgs)
                    {
                        if (args[i] == arg)
                        {
                            _argsModel.RemoveDoubles = !removeDoublesResponses.Contains(args[i + 1]);
                            break;
                        }
                    }
                }
                return _argsModel;
            }
            catch (Exception ex)
            {
                if (ex.GetType().ToString() == "System.IndexOutOfRangeException")
                {
                    throw new Exception($"You need to give an value to every argument!");
                }
                else
                    throw new Exception($"Occured an error while trying to handle the args. ");
            }
        }


        public async static Task<string> TransformIfIsLink(string path)
        {
            if (path.Contains("http"))
            {
                path = await FileDownloaderHandler.DownloadFile(path);
                _argsModel.IsLinkSourcePath = true;
            }

            return path;
        }

    }
}