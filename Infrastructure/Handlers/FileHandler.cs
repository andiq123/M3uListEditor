using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;
using Infrastructure.Handlers;

namespace Infrastructure
{
    public class FileHandler
    {
        public static void AddChannel(string path, Channel channel)
        {
            using (StreamWriter sw = new StreamWriter(path, true))
            {
                string channelToWrite = "";
                if (!string.IsNullOrEmpty(channel.GroupName))
                {
                    channelToWrite = $"{channel.Name}\n{channel.GroupName}\n{channel.Link}\n\n";
                }
                else
                {
                    channelToWrite = $"{channel.Name}\n{channel.Link}\n\n";
                }
                sw.Write(channelToWrite);
            }
        }

        public static void RemoveFileIfExits(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static void CreateFileIfDoesNotExists(string path)
        {
            if (!File.Exists(path))
            {
                File.Create(path);
            }
        }

        public static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path);
            }
        }

        public static void CreateDirectoryIfNotExits(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}