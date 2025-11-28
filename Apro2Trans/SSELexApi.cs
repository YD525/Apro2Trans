using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;
using PhoenixEngine.TranslateManage;

namespace Apro2Trans
{
    public static class CrcHelper
    {
        private static readonly uint[] Table;

        static CrcHelper()
        {
            Table = new uint[256];
            const uint poly = 0xEDB88320;
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ poly;
                    else
                        crc >>= 1;
                }
                Table[i] = crc;
            }
        }

        public static uint ComputeCRC32(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            uint crc = 0xFFFFFFFF;
            foreach (byte b in bytes)
            {
                byte index = (byte)((crc & 0xFF) ^ b);
                crc = (crc >> 8) ^ Table[index];
            }
            return ~crc;
        }

        public static int ComputeCRC32Int(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            uint crc = 0xFFFFFFFF;
            foreach (byte b in bytes)
            {
                byte index = (byte)((crc & 0xFF) ^ b);
                crc = (crc >> 8) ^ Table[index];
            }
            return unchecked((int)~crc);
        }

        public static string ComputeCRC32Hex(string input)
        {
            return ComputeCRC32(input).ToString("X8");
        }
    }

    public class SSELexApi
    {
        public static string GetFullPath(string path)
        {
            string basePath = AppContext.BaseDirectory;
            return Path.Combine(basePath, path.TrimStart('\\', '/'));
        }
        public void Init()
        {
            Engine.Init();

            string SetCachePath = GetFullPath(@"\Cache");
            if (!Directory.Exists(SetCachePath))
            {
                Directory.CreateDirectory(SetCachePath);
            }

            EngineConfig.LMLocalAIEnable = true;
            EngineConfig.ContextEnable = true;
            EngineConfig.PreTranslateEnable = true;

            EngineConfig.Save();
        }
        public TranslationUnit? Dequeue(ref bool IsEnd)
        {
            return Engine.DequeueTranslated(ref IsEnd);
        }
        public int Enqueue(string FileName, string Key, string Type, string Original, string AIParam)
        {
            TranslationUnit Unit = new TranslationUnit(
            CrcHelper.ComputeCRC32Int(FileName),
            Key,
            Type,
            Original,
            "",
            AIParam,
            Engine.From,
            Engine.To,
            100
            );

            int GetEnqueueCount = Engine.AddTranslationUnit(Unit);

            return GetEnqueueCount;
        }
        public void SetThread(int ThreadCount)
        {
            EngineConfig.MaxThreadCount = ThreadCount;
            EngineConfig.AutoSetThreadLimit = false;

            EngineConfig.Save();
        }
        public int GetWorkingThreadCount()
        {
            return Engine.GetThreadCount();
        }
        public int SetLang(string From, string To)
        {
            try
            {
                Engine.From = LanguageHelper.FromLanguageCode(From);
                Engine.To = LanguageHelper.FromLanguageCode(To);

                return 1;

            }
            catch
            {
                return -1;
            }
        }
    }
}
