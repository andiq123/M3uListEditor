using System.IO;
using System.Net;
using System.Threading.Tasks;
using Infrastructure.Handlers;

namespace Infrastructure
{
    public class FileDownloaderHandler
    {
        private static string _tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
        private static string _filePath = Path.Combine(_tempDirectory, $"{NamesGenerator.GenerateNameByDateAndHour()}.m3u");
        public async static Task<string> DownloadFile(string link)
        {
            FileHandler.CreateDirectoryIfNotExits(_tempDirectory);
            FileHandler.RemoveFileIfExits(_filePath);
            using (var client = new WebClient())
            {
                await client.DownloadFileTaskAsync(link, _filePath);
                return _filePath;
            }
        }
    }
}