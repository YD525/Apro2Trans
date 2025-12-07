using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.DelegateManagement;
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

            EngineConfig.ContextLimit = 150;
            EngineConfig.PreTranslateEnable = true;

            EngineConfig.Save();

            Engine.From = Languages.English;
            Engine.To = Languages.English;

            DelegateHelper.SetTranslationUnitCallBack += TranslationUnitEndWorkCall;
        }

        public static List<string> ErrorKeys = new List<string>();
        public static bool TranslationUnitEndWorkCall(TranslationUnit Item, int State)
        {
            string Action =
   "[Strict Translation Instruction]\r\n" +
   "IMPORTANT: The translation will be directly shown in the game JSON.\r\n" +
   "Only output the pure translated text.\r\n" +
   "Do NOT add any extra characters, explanations, labels, context, control characters, or emoji.\r\n" +
   "Strictly follow the placeholder format $$Word$$.\r\n\r\n";

            if (Item.AIParam.Length == 0)
            {
                Item.AIParam = Action;
            }

            if (State == 2)
            {
                //Quality inspection of the translated content
                int A = Item.SourceText?.Count(L => L == '$') ?? 0;
                int B = Item.TransText?.Count(L => L == '$') ?? 0;

                if (A != B)
                {
                    int ErrorCount = 0;

                    if (ErrorKeys.Contains(Item.Key))
                    {
                        ErrorCount++;
                    }
                    else
                    {
                        ErrorKeys.Add(Item.Key);
                    }

                    string AutoStr = "";

                    if (ErrorCount > 0)
                    {
                        AutoStr = "Repeat: do NOT translate or alter anything inside the $$Word$$ placeholders.\r\n";
                    }

                    //Dynamically modify prompt words
                    Item.AIParam = Action +
                    "[Translation Error Report]\r\n" +
                    $"Translation error: The \"$\" placeholder symbols were handled incorrectly.\r\n" +
                    "All \"$\" characters must be preserved without any modification.\r\n" +
                    "Please strictly follow the placeholder format $$Word$$ and do NOT translate or modify it.\r\n" +
                     AutoStr +
                    $"Source: {Item.SourceText}\r\n" +
                    $"Invalid Translation: {Item.TransText}\r\n";
                    return false;
                }
                else
                {
                    return true;
                }
            }

            return true;
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
            "",
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
