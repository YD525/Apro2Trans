using System;
using System.Collections.Generic;
using System.Text;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.EngineManagement;
using System.IO;
using PhoenixEngine.PlatformManagement.LocalAI;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Apro2Trans
{
    public class AproposHelper
    {
        public static Thread UISyncTrd = null;
        public static Thread TranslationSyncTrd = null;

        public static Dictionary<string, TranslationUnit> Translateds = new Dictionary<string, TranslationUnit>();


        public static bool StartUISyncState = false;
        public static void StartUISyncService(bool Check)
        {
            if (Check)
            {
                if (!StartUISyncState)
                {
                    StartUISyncState = true;

                    UISyncTrd = new Thread(() =>
                    {
                        while (StartUISyncState)
                        {
                            Thread.Sleep(1000);
                            DeFine.WorkWin.Dispatcher.Invoke(new Action(() =>
                            {
                                DeFine.WorkWin.ThreadInFo.Content = string.Format("(Current:{0},Max:{1})", Engine.GetThreadCount(), EngineConfig.Config.MaxThreadCount);
                                DeFine.WorkWin.Progress.Content = string.Format("({0}/{1})", Translateds.Count, Total);
                                if (Working)
                                {
                                    DeFine.WorkWin.State.Content = "Working";
                                }
                                else
                                {
                                    DeFine.WorkWin.State.Content = "";
                                }
                            }));
                        }
                    });

                    UISyncTrd.Start();
                }
            }
        }

        public static bool IsValidJsonString(string Text)
        {
            if (Text == null)
                return true;

            try
            {
                string Json = JsonConvert.SerializeObject(Text);

                string Deserialized = JsonConvert.DeserializeObject<string>(Json);

                if (Deserialized == Text)
                {
                    return true;
                }

                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }
        public static int CleanInvalidJsonTexts()
        {
            var Removes = new List<string>();

            foreach (var Translated in Translateds)
            {
                var Unit = Translated.Value;
                if (!IsValidJsonString(Unit.TransText))
                {
                    Removes.Add(Translated.Key);
                }
            }

            foreach (var Key in Removes)
            {
                Translateds.Remove(Key);
            }

            return Removes.Count;
        }


        public static bool Working = false;

        public static bool StartTranslationSyncState = false;
        public static void StartTranslationSyncService(bool Check)
        {
            if (Check)
            {
                if (!StartTranslationSyncState)
                {
                    StartTranslationSyncState = true;

                    TranslationSyncTrd = new Thread(() =>
                    {
                        Working = true;

                        while (StartTranslationSyncState)
                        {
                            bool IsEnd = false;

                            try
                            {
                                var GetUnit = Engine.DequeueTranslated(ref IsEnd);
                                if (GetUnit != null)
                                {
                                    if (!Translateds.ContainsKey(GetUnit.Key))
                                    {
                                        GetUnit.TransText = Engine.AppendDollarWrappedReplacements(GetUnit.TransText);
                                        Translateds.Add(GetUnit.Key, GetUnit);
                                    }
                                    else
                                    {

                                    }
                                }
                                else
                                {
                                    Thread.Sleep(500);
                                }
                            }
                            catch
                            {
                            
                            }

                            if (IsEnd)
                            {
                                Log("Quality testing is underway.");

                                if (CleanInvalidJsonTexts() > 0)
                                {
                                    Log("Remove fields that might affect the JSON structure.");
                                }

                                Working = false;
                                StartTranslationSyncState = false;

                                Log("Write it into the translation record.");

                                WriteDB();

                                Thread.Sleep(100);

                                Log("All records have been translated; please check your local file.");
                            }
                        }
                    });

                    TranslationSyncTrd.Start();
                }
            }
            else
            {
                StartTranslationSyncState = false;
                Working = false;
            }
        }

        private static string LastReadFilePath = "";

        public static int Total = 0;
        public static SSELexApi TranslateApi = new SSELexApi();
        public static void ReadDB(string FilePath, string Suffix = ".txt")
        {
            LastReadFilePath = FilePath;
            Engine.InitTranslationCore(Engine.From, Engine.To);

            new Thread(() =>
            {
                Thread.Sleep(100);

                if (Working)
                {
                    return;
                }

                var GetFiles = DataHelper.GetAllFile(FilePath, new List<string>() { Suffix });

                Log(GetFiles.Count + " files have been read,Please wait........");

                foreach (var Get in GetFiles)
                {
                    string GetContent = DataHelper.ReadFileByStr(Get.FilePath, Encoding.UTF8);

                    ReadAproposRecords(Get.FilePath, Get.FileName, GetContent);
                }

                Total = RecordCount;
                Log(Total + " records have been added.");
                Engine.SkipWordAnalysis(true);
                Log("Disable word analysis");
                Engine.Start();

                StartTranslationSyncService(true);

            }).Start();
        }


        public static void Close()
        {
            Engine.End();
            StartTranslationSyncService(false);
        }

        public static string ExtractContent(string AiResponseJson)
        {
            if (string.IsNullOrWhiteSpace(AiResponseJson))
                return null;

            try
            {
                JObject Json = JObject.Parse(AiResponseJson);
                JArray Choices = (JArray)Json["choices"];

                if (Choices != null && Choices.Count > 0)
                {
                    JObject FirstChoice = (JObject)Choices[0];

                    JObject Message = (JObject)FirstChoice["message"];

                    if (Message != null)
                    {
                        string Content = (string)Message["content"];
                        return Content;
                    }
                }
            }
            catch (JsonReaderException ex)
            {
                
            }
            catch (Exception ex)
            {
                
            }

            return null;
        }

        public static string ReplaceDoubleDollarToBraces(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return Regex.Replace(input, @"\$\$(.*?)\$\$", "{$1}");
        }

        public static void WriteDB()
        {
            if (!Directory.Exists(LastReadFilePath) || LastReadFilePath.Trim().Length == 0)
            {
                return;
            }
            var GetFiles = DataHelper.GetAllFile(LastReadFilePath, new List<string>() { ".txt" });

            foreach (var Get in GetFiles)
            {
                string Content = DataHelper.ReadFileByStr(Get.FilePath, Encoding.UTF8);

                string FileName = Get.FileName;
                string FilePath = Get.FilePath;

                string GetJson = "";

                if (FileName == "Synonyms.txt")
                {
                    SynonymsItem GetSynonyms = JsonConvert.DeserializeObject<SynonymsItem>(Content);

                    if (GetSynonyms == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < GetSynonyms.ACCEPT?.Length; i++)
                    {
                        string Type = "ACCEPT";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.ACCEPT[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.ACCEPTING?.Length; i++)
                    {
                        string Type = "ACCEPTING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.ACCEPTING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.ACCEPTS?.Length; i++)
                    {
                        string Type = "ACCEPTS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.ACCEPTS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.ASS?.Length; i++)
                    {
                        string Type = "ASS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.ASS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BEAST?.Length; i++)
                    {
                        string Type = "BEAST";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BEAST[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BEASTCOCK?.Length; i++)
                    {
                        string Type = "BEASTCOCK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BEASTCOCK[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BITCH?.Length; i++)
                    {
                        string Type = "BITCH";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BITCH[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }
                    for (int i = 0; i < GetSynonyms.BOOBS?.Length; i++)
                    {
                        string Type = "BOOBS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BOOBS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BREED?.Length; i++)
                    {
                        string Type = "BREED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BREED[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BUG?.Length; i++)
                    {
                        string Type = "BUG";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BUG[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BUGCOCK?.Length; i++)
                    {
                        string Type = "BUGCOCK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BUGCOCK[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BUTTOCKS?.Length; i++)
                    {
                        string Type = "BUTTOCKS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BUTTOCKS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.COCK?.Length; i++)
                    {
                        string Type = "COCK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.COCK[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.CREAM?.Length; i++)
                    {
                        string Type = "CREAM";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.CREAM[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.CUM?.Length; i++)
                    {
                        string Type = "CUM";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.CUM[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.CUMMING?.Length; i++)
                    {
                        string Type = "CUMMING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.CUMMING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.CUMS?.Length; i++)
                    {
                        string Type = "CUMS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.CUMS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.DEAD?.Length; i++)
                    {
                        string Type = "DEAD";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.DEAD[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.EXPLORE?.Length; i++)
                    {
                        string Type = "EXPLORE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.EXPLORE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.EXPOSE?.Length; i++)
                    {
                        string Type = "EXPOSE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.EXPOSE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FEAR?.Length; i++)
                    {
                        string Type = "FEAR";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FEAR[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FFAMILY?.Length; i++)
                    {
                        string Type = "FFAMILY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FFAMILY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FOREIGN?.Length; i++)
                    {
                        string Type = "FOREIGN";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FOREIGN[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FUCK?.Length; i++)
                    {
                        string Type = "FUCK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FUCK[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FUCKED?.Length; i++)
                    {
                        string Type = "FUCKED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FUCKED[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FUCKING?.Length; i++)
                    {
                        string Type = "FUCKING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FUCKING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FUCKS?.Length; i++)
                    {
                        string Type = "FUCKS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FUCKS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.GENWT?.Length; i++)
                    {
                        string Type = "GENWT";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.GENWT[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.GIRTH?.Length; i++)
                    {
                        string Type = "GIRTH";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.GIRTH[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HEAVING?.Length; i++)
                    {
                        string Type = "HEAVING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HEAVING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HOLE?.Length; i++)
                    {
                        string Type = "HOLE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HOLE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HOLES?.Length; i++)
                    {
                        string Type = "HOLES";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HOLES[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HORNY?.Length; i++)
                    {
                        string Type = "HORNY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HORNY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HUGE?.Length; i++)
                    {
                        string Type = "HUGE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HUGE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HUGELOAD?.Length; i++)
                    {
                        string Type = "HUGELOAD";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HUGELOAD[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.INSERT?.Length; i++)
                    {
                        string Type = "INSERT";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.INSERT[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.INSERTED?.Length; i++)
                    {
                        string Type = "INSERTED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.INSERTED[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.INSERTING?.Length; i++)
                    {
                        string Type = "INSERTING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.INSERTING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.INSERTS?.Length; i++)
                    {
                        string Type = "INSERTS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.INSERTS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.JIGGLE?.Length; i++)
                    {
                        string Type = "JIGGLE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.JIGGLE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.JUICY?.Length; i++)
                    {
                        string Type = "JUICY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.JUICY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.LARGELOAD?.Length; i++)
                    {
                        string Type = "LARGELOAD";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.LARGELOAD[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.LOUDLY?.Length; i++)
                    {
                        string Type = "LOUDLY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.LOUDLY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MACHINE?.Length; i++)
                    {
                        string Type = "MACHINE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MACHINE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MACHINESLIME?.Length; i++)
                    {
                        string Type = "MACHINESLIME";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MACHINESLIME[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MACHINESLIMY?.Length; i++)
                    {
                        string Type = "MACHINESLIMY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MACHINESLIMY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.METAL?.Length; i++)
                    {
                        string Type = "METAL";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.METAL[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MFAMILY?.Length; i++)
                    {
                        string Type = "MFAMILY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MFAMILY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MNONFAMILY?.Length; i++)
                    {
                        string Type = "MNONFAMILY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MNONFAMILY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MOAN?.Length; i++)
                    {
                        string Type = "MOAN";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MOAN[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MOANING?.Length; i++)
                    {
                        string Type = "MOANING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MOANING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MOANS?.Length; i++)
                    {
                        string Type = "MOANS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MOANS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MOUTH?.Length; i++)
                    {
                        string Type = "MOUTH";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MOUTH[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.OPENING?.Length; i++)
                    {
                        string Type = "OPENING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.OPENING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.PAIN?.Length; i++)
                    {
                        string Type = "PAIN";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.PAIN[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.PENIS?.Length; i++)
                    {
                        string Type = "PENIS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.PENIS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.PROBE?.Length; i++)
                    {
                        string Type = "PROBE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.PROBE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.PUSSY?.Length; i++)
                    {
                        string Type = "PUSSY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.PUSSY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.QUIVERING?.Length; i++)
                    {
                        string Type = "QUIVERING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.QUIVERING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.RAPE?.Length; i++)
                    {
                        string Type = "RAPE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.RAPE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.RAPED?.Length; i++)
                    {
                        string Type = "RAPED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.RAPED[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SALTY?.Length; i++)
                    {
                        string Type = "SALTY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SALTY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SCREAM?.Length; i++)
                    {
                        string Type = "SCREAM";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SCREAM[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SCREAMS?.Length; i++)
                    {
                        string Type = "SCREAMS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SCREAMS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SCUM?.Length; i++)
                    {
                        string Type = "SCUM";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SCUM[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLIME?.Length; i++)
                    {
                        string Type = "SLIME";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLIME[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLIMY?.Length; i++)
                    {
                        string Type = "SLIMY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLIMY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLOPPY?.Length; i++)
                    {
                        string Type = "SLOPPY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLOPPY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLOWLY?.Length; i++)
                    {
                        string Type = "SLOWLY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLOWLY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLUTTY?.Length; i++)
                    {
                        string Type = "SLUTTY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLUTTY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMIZE?.Length; i++)
                    {
                        string Type = "SODOMIZE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMIZE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMIZED?.Length; i++)
                    {
                        string Type = "SODOMIZED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMIZED[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMIZES?.Length; i++)
                    {
                        string Type = "SODOMIZES";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMIZES[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMIZING?.Length; i++)
                    {
                        string Type = "SODOMIZING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMIZING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMY?.Length; i++)
                    {
                        string Type = "SODOMY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SOLID?.Length; i++)
                    {
                        string Type = "SOLID";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SOLID[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.STRAPON?.Length; i++)
                    {
                        string Type = "STRAPON";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.STRAPON[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SUBMISSIVE?.Length; i++)
                    {
                        string Type = "SUBMISSIVE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SUBMISSIVE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SUBMIT?.Length; i++)
                    {
                        string Type = "SUBMIT";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SUBMIT[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SWEARING?.Length; i++)
                    {
                        string Type = "SWEARING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SWEARING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.TASTY?.Length; i++)
                    {
                        string Type = "TASTY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.TASTY[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.THICK?.Length; i++)
                    {
                        string Type = "THICK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.THICK[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.TIGHTNESS?.Length; i++)
                    {
                        string Type = "TIGHTNESS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.TIGHTNESS[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.UNTHINKING?.Length; i++)
                    {
                        string Type = "UNTHINKING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.UNTHINKING[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.VILE?.Length; i++)
                    {
                        string Type = "VILE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.VILE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.WET?.Length; i++)
                    {
                        string Type = "WET";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.WET[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    for (int i = 0; i < GetSynonyms.WHORE?.Length; i++)
                    {
                        string Type = "WHORE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.WHORE[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                    GetJson = JsonConvert.SerializeObject(GetSynonyms,Formatting.Indented);

                    DataHelper.WriteFile(FilePath, Encoding.UTF8.GetBytes(GetJson));
                    continue;
                }
                else
                if (FileName == "WearAndTear_Descriptors.txt")
                {
                    //WearAndTearItem GetWearAndTear = JsonSerializer.Deserialize<WearAndTearItem>(Content);
                }
                else
                if (FileName == "Arousal_Descriptors.txt")
                {
                    //ArousalItem GetArousal = JsonSerializer.Deserialize<ArousalItem>(Content);
                }
                else
                {
                    if (!Content.Contains("1st Person"))
                    {
                        continue;
                    }
                }

                AproposItem GetApropos = JsonConvert.DeserializeObject<AproposItem>(Content);

                if (GetApropos == null)
                {
                    continue;
                }

                if (GetApropos._1stPerson != null)
                    for (int i = 0; i < GetApropos._1stPerson?.Length; i++)
                    {
                        string Type = "1stPerson";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetApropos._1stPerson[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                if (GetApropos._2ndPerson != null)
                    for (int i = 0; i < GetApropos._2ndPerson?.Length; i++)
                    {
                        string Type = "2ndPerson";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetApropos._2ndPerson[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                if (GetApropos._3rdPerson != null)
                    for (int i = 0; i < GetApropos._3rdPerson?.Length; i++)
                    {
                        string Type = "3rdPerson";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetApropos._3rdPerson[i] = ReplaceDoubleDollarToBraces(Translateds[Key].TransText);
                        }
                    }

                GetJson = JsonConvert.SerializeObject(GetApropos,Formatting.Indented);

                DataHelper.WriteFile(FilePath, Encoding.UTF8.GetBytes(GetJson));
                continue;
            }
        }

        public static void Log(string Msg)
        {
            DeFine.WorkWin.SetLog(Msg);
        }

        public static int RecordCount = 0;
        public static void ReadAproposRecords(string FilePath, string FileName, string Content)
        {
            string Prompt = $@"
You are a JSON fixer AI.

I will give you a piece of JSON that may be malformed or broken.

Your task:
1. Automatically fix all syntax errors.
2. Keep the original structure, field names, and content exactly as is.
3. Only fix formatting; do not change any text content.
4. Output ONLY the corrected JSON.
5. Do NOT add any Markdown, comments, explanations, or extra text.
6. Do NOT wrap the JSON in ```json``` or any code blocks.
7. Your response must be strictly valid JSON that can be parsed by a standard JSON parser.

[Original JSON]
{Content}

Return only the fixed JSON.
";

            if (FileName == "Synonyms.txt")
            {
                SynonymsItem GetSynonyms = null;
                try
                {
                    GetSynonyms = JsonConvert.DeserializeObject<SynonymsItem>(Content);
                }
                catch (Exception Ex)
                {
                //Automatic JSON syntax correction
                //[Apropos2 DB Update] - The JSON has an incorrect format... 
                //Since we're already here, let's just use AI to fix it without thinking.
                TryAgain:
                    LMStudio NLMStudio = new LMStudio();

                    string RecvMsg = "";

                    NLMStudio.CallAI(Prompt, ref RecvMsg);

                    if (RecvMsg != null)
                    {
                        string GetAIResult = ExtractContent(RecvMsg);
                        if (GetAIResult != null)
                        {
                            //Input the AI-repaired JSON
                            //If you're wrong, just go to.
                            try
                            {
                                GetSynonyms = JsonConvert.DeserializeObject<SynonymsItem>(GetAIResult);

                                //Write the repaired JSON
                                var GetJson = JsonConvert.SerializeObject(GetSynonyms,Formatting.Indented);
                                DataHelper.WriteFile(FilePath, Encoding.UTF8.GetBytes(GetJson));

                                Log("Automatically fix JSON syntax errors - " + FilePath);
                            }
                            catch
                            {
                                Thread.Sleep(100);
                                goto TryAgain;
                            }
                        }
                    }
                }

                if (GetSynonyms == null)
                {
                    return;
                }

                for (int i = 0; i < GetSynonyms.ACCEPT?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.ACCEPT[i];
                    string Type = "ACCEPT";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.ACCEPTING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.ACCEPTING[i];
                    string Type = "ACCEPTING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.ACCEPTS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.ACCEPTS[i];
                    string Type = "ACCEPTS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.ASS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.ASS[i];
                    string Type = "ASS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.BEAST?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BEAST[i];
                    string Type = "BEAST";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.BEASTCOCK?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BEASTCOCK[i];
                    string Type = "BEASTCOCK";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.BITCH?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BITCH[i];
                    string Type = "BITCH";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }
                for (int i = 0; i < GetSynonyms.BOOBS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BOOBS[i];
                    string Type = "BOOBS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.BREED?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BREED[i];
                    string Type = "BREED";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.BUG?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BUG[i];
                    string Type = "BUG";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.BUGCOCK?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BUGCOCK[i];
                    string Type = "BUGCOCK";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.BUTTOCKS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BUTTOCKS[i];
                    string Type = "BUTTOCKS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.COCK?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.COCK[i];
                    string Type = "COCK";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.CREAM?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.CREAM[i];
                    string Type = "CREAM";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.CUM?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.CUM[i];
                    string Type = "CUM";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.CUMMING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.CUMMING[i];
                    string Type = "CUMMING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.CUMS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.CUMS[i];
                    string Type = "CUMS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.DEAD?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.DEAD[i];
                    string Type = "DEAD";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.EXPLORE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.EXPLORE[i];
                    string Type = "EXPLORE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.EXPOSE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.EXPOSE[i];
                    string Type = "EXPOSE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.FEAR?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FEAR[i];
                    string Type = "FEAR";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.FFAMILY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FFAMILY[i];
                    string Type = "FFAMILY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.FOREIGN?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FOREIGN[i];
                    string Type = "FOREIGN";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.FUCK?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FUCK[i];
                    string Type = "FUCK";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.FUCKED?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FUCKED[i];
                    string Type = "FUCKED";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.FUCKING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FUCKING[i];
                    string Type = "FUCKING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.FUCKS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FUCKS[i];
                    string Type = "FUCKS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.GENWT?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.GENWT[i];
                    string Type = "GENWT";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.GIRTH?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.GIRTH[i];
                    string Type = "GIRTH";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.HEAVING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HEAVING[i];
                    string Type = "HEAVING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.HOLE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HOLE[i];
                    string Type = "HOLE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.HOLES?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HOLES[i];
                    string Type = "HOLES";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.HORNY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HORNY[i];
                    string Type = "HORNY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.HUGE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HUGE[i];
                    string Type = "HUGE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.HUGELOAD?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HUGELOAD[i];
                    string Type = "HUGELOAD";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.INSERT?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.INSERT[i];
                    string Type = "INSERT";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.INSERTED?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.INSERTED[i];
                    string Type = "INSERTED";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.INSERTING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.INSERTING[i];
                    string Type = "INSERTING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.INSERTS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.INSERTS[i];
                    string Type = "INSERTS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.JIGGLE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.JIGGLE[i];
                    string Type = "JIGGLE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.JUICY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.JUICY[i];
                    string Type = "JUICY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.LARGELOAD?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.LARGELOAD[i];
                    string Type = "LARGELOAD";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.LOUDLY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.LOUDLY[i];
                    string Type = "LOUDLY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.MACHINE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MACHINE[i];
                    string Type = "MACHINE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.MACHINESLIME?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MACHINESLIME[i];
                    string Type = "MACHINESLIME";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.MACHINESLIMY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MACHINESLIMY[i];
                    string Type = "MACHINESLIMY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.METAL?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.METAL[i];
                    string Type = "METAL";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.MFAMILY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MFAMILY[i];
                    string Type = "MFAMILY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.MNONFAMILY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MNONFAMILY[i];
                    string Type = "MNONFAMILY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.MOAN?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MOAN[i];
                    string Type = "MOAN";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.MOANING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MOANING[i];
                    string Type = "MOANING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.MOANS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MOANS[i];
                    string Type = "MOANS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.MOUTH?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MOUTH[i];
                    string Type = "MOUTH";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.OPENING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.OPENING[i];
                    string Type = "OPENING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.PAIN?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.PAIN[i];
                    string Type = "PAIN";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.PENIS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.PENIS[i];
                    string Type = "PENIS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.PROBE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.PROBE[i];
                    string Type = "PROBE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.PUSSY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.PUSSY[i];
                    string Type = "PUSSY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.QUIVERING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.QUIVERING[i];
                    string Type = "QUIVERING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.RAPE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.RAPE[i];
                    string Type = "RAPE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.RAPED?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.RAPED[i];
                    string Type = "RAPED";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SALTY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SALTY[i];
                    string Type = "SALTY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SCREAM?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SCREAM[i];
                    string Type = "SCREAM";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SCREAMS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SCREAMS[i];
                    string Type = "SCREAMS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SCUM?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SCUM[i];
                    string Type = "SCUM";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SLIME?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SLIME[i];
                    string Type = "SLIME";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SLIMY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SLIMY[i];
                    string Type = "SLIMY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SLOPPY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SLOPPY[i];
                    string Type = "SLOPPY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SLOWLY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SLOWLY[i];
                    string Type = "SLOWLY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SLUTTY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SLUTTY[i];
                    string Type = "SLUTTY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SODOMIZE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SODOMIZE[i];
                    string Type = "SODOMIZE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SODOMIZED?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SODOMIZED[i];
                    string Type = "SODOMIZED";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SODOMIZES?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SODOMIZES[i];
                    string Type = "SODOMIZES";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SODOMIZING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SODOMIZING[i];
                    string Type = "SODOMIZING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SODOMY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SODOMY[i];
                    string Type = "SODOMY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SOLID?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SOLID[i];
                    string Type = "SOLID";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.STRAPON?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.STRAPON[i];
                    string Type = "STRAPON";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SUBMISSIVE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SUBMISSIVE[i];
                    string Type = "SUBMISSIVE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SUBMIT?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SUBMIT[i];
                    string Type = "SUBMIT";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.SWEARING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SWEARING[i];
                    string Type = "SWEARING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.TASTY?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.TASTY[i];
                    string Type = "TASTY";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.THICK?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.THICK[i];
                    string Type = "THICK";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.TIGHTNESS?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.TIGHTNESS[i];
                    string Type = "TIGHTNESS";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.UNTHINKING?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.UNTHINKING[i];
                    string Type = "UNTHINKING";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.VILE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.VILE[i];
                    string Type = "VILE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.WET?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.WET[i];
                    string Type = "WET";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.WHORE?.Length; i++)
                {
                    string GetOriginal = GetSynonyms.WHORE[i];
                    string Type = "WHORE";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

                //string GetJson = JsonSerializer.Serialize(GetSynonyms, new JsonSerializerOptions
                //{
                //    WriteIndented = true
                //});

                //return GetJson;
            }
            else
            if (FileName == "WearAndTear_Descriptors.txt")
            {
                //WearAndTearItem GetWearAndTear = JsonSerializer.Deserialize<WearAndTearItem>(Content);

                //string GetJson = JsonSerializer.Serialize(GetWearAndTear, new JsonSerializerOptions
                //{
                //    WriteIndented = true
                //});

                //return GetJson;
            }
            else
            if (FileName == "Arousal_Descriptors.txt")
            {
                //ArousalItem GetArousal = JsonSerializer.Deserialize<ArousalItem>(Content);

                //Process

                //string GetJson = JsonSerializer.Serialize(GetArousal, new JsonSerializerOptions
                //{
                //    WriteIndented = true
                //});

                //return GetJson;
            }
            else
            {
                if (!Content.Contains("1st Person"))
                {
                    return;
                }
            }

            AproposItem GetApropos = null;

            try
            {
                GetApropos = JsonConvert.DeserializeObject<AproposItem>(Content);
            }
            catch (Exception Ex)
            {
            //Automatic JSON syntax correction
            //[Apropos2 DB Update] - The JSON has an incorrect format... 
            //Since we're already here, let's just use AI to fix it without thinking.
                TryAgain:
                LMStudio NLMStudio = new LMStudio();
                string RecvMsg = "";
                NLMStudio.CallAI(Prompt, ref RecvMsg);

                if (RecvMsg != null)
                {
                    string GetAIResult = ExtractContent(RecvMsg);
                    if (GetAIResult != null)
                    {
                        //Input the AI-repaired JSON
                        //If you're wrong, just go to.
                        try
                        {
                            GetApropos = JsonConvert.DeserializeObject<AproposItem>(GetAIResult);

                            //Write the repaired JSON
                            var GetJson = JsonConvert.SerializeObject(GetApropos,Formatting.Indented);

                            DataHelper.WriteFile(FilePath, Encoding.UTF8.GetBytes(GetJson));

                            Log("Automatically fix JSON syntax errors - " + FilePath);
                        }
                        catch (Exception E)
                        {
                            Thread.Sleep(100);
                            goto TryAgain;
                        }
                    }
                }
            }

            if (GetApropos == null)
            {
                return;
            }

            if (GetApropos._1stPerson != null)
                for (int i = 0; i < GetApropos._1stPerson?.Length; i++)
                {
                    string GetOriginal = GetApropos._1stPerson[i];

                    string Type = "1stPerson";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

            if (GetApropos._2ndPerson != null)
                for (int i = 0; i < GetApropos._2ndPerson?.Length; i++)
                {
                    string GetOriginal = GetApropos._2ndPerson[i];

                    string Type = "2ndPerson";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }

            if (GetApropos._3rdPerson != null)
                for (int i = 0; i < GetApropos._3rdPerson?.Length; i++)
                {
                    string GetOriginal = GetApropos._3rdPerson[i];

                    string Type = "3rdPerson";
                    string Key = FilePath + "-" + Type + "[" + i + "]";

                    RecordCount = TranslateApi.Enqueue(FileName, Key, Type, GetOriginal, string.Empty);
                }


            //string GetJsonA = JsonSerializer.Serialize(GetApropos, new JsonSerializerOptions
            //{
            //    WriteIndented = true
            //});

            //return GetJsonA;
        }
    }


    public class AproposItem
    {
        [JsonProperty("1st Person")]
        public string[] _1stPerson { get; set; }

        [JsonProperty("2nd Person")]
        public string[] _2ndPerson { get; set; }

        [JsonProperty("3rd Person")]
        public string[] _3rdPerson { get; set; }
    }



    public class SynonymsItem
    {
        [JsonProperty("{ACCEPTS}")]
        public string[] ACCEPTS { get; set; }

        [JsonProperty("{ACCEPT}")]
        public string[] ACCEPT { get; set; }

        [JsonProperty("{ACCEPTING}")]
        public string[] ACCEPTING { get; set; }

        [JsonProperty("{ASS}")]
        public string[] ASS { get; set; }

        [JsonProperty("{BEASTCOCK}")]
        public string[] BEASTCOCK { get; set; }

        [JsonProperty("{BEAST}")]
        public string[] BEAST { get; set; }

        [JsonProperty("{BITCH}")]
        public string[] BITCH { get; set; }

        [JsonProperty("{BOOBS}")]
        public string[] BOOBS { get; set; }

        [JsonProperty("{BREED}")]
        public string[] BREED { get; set; }

        [JsonProperty("{BUGCOCK}")]
        public string[] BUGCOCK { get; set; }

        [JsonProperty("{BUG}")]
        public string[] BUG { get; set; }

        [JsonProperty("{BUTTOCKS}")]
        public string[] BUTTOCKS { get; set; }

        [JsonProperty("{COCK}")]
        public string[] COCK { get; set; }

        [JsonProperty("{CREAM}")]
        public string[] CREAM { get; set; }

        [JsonProperty("{CUMMING}")]
        public string[] CUMMING { get; set; }

        [JsonProperty("{CUMS}")]
        public string[] CUMS { get; set; }

        [JsonProperty("{CUM}")]
        public string[] CUM { get; set; }

        [JsonProperty("{DEAD}")]
        public string[] DEAD { get; set; }

        [JsonProperty("{EXPLORE}")]
        public string[] EXPLORE { get; set; }

        [JsonProperty("{EXPOSE}")]
        public string[] EXPOSE { get; set; }

        [JsonProperty("{FEAR}")]
        public string[] FEAR { get; set; }

        [JsonProperty("{FFAMILY}")]
        public string[] FFAMILY { get; set; }

        [JsonProperty("{FOREIGN}")]
        public string[] FOREIGN { get; set; }

        [JsonProperty("{FUCKED}")]
        public string[] FUCKED { get; set; }

        [JsonProperty("{FUCKING}")]
        public string[] FUCKING { get; set; }

        [JsonProperty("{FUCKS}")]
        public string[] FUCKS { get; set; }

        [JsonProperty("{FUCK}")]
        public string[] FUCK { get; set; }

        [JsonProperty("{GENWT}")]
        public string[] GENWT { get; set; }

        [JsonProperty("{GIRTH}")]
        public string[] GIRTH { get; set; }

        [JsonProperty("{HEAVING}")]
        public string[] HEAVING { get; set; }

        [JsonProperty("{HOLE}")]
        public string[] HOLE { get; set; }

        [JsonProperty("{HOLES}")]
        public string[] HOLES { get; set; }

        [JsonProperty("{HORNY}")]
        public string[] HORNY { get; set; }

        [JsonProperty("{HUGELOAD}")]
        public string[] HUGELOAD { get; set; }

        [JsonProperty("{HUGE}")]
        public string[] HUGE { get; set; }

        [JsonProperty("{INSERT}")]
        public string[] INSERT { get; set; }

        [JsonProperty("{INSERTS}")]
        public string[] INSERTS { get; set; }

        [JsonProperty("{INSERTED}")]
        public string[] INSERTED { get; set; }

        [JsonProperty("{INSERTING}")]
        public string[] INSERTING { get; set; }

        [JsonProperty("{JIGGLE}")]
        public string[] JIGGLE { get; set; }

        [JsonProperty("{JUICY}")]
        public string[] JUICY { get; set; }

        [JsonProperty("{LARGELOAD}")]
        public string[] LARGELOAD { get; set; }

        [JsonProperty("{LOUDLY}")]
        public string[] LOUDLY { get; set; }

        [JsonProperty("{MACHINESLIME}")]
        public string[] MACHINESLIME { get; set; }

        [JsonProperty("{MACHINESLIMY}")]
        public string[] MACHINESLIMY { get; set; }

        [JsonProperty("{MACHINE}")]
        public string[] MACHINE { get; set; }

        [JsonProperty("{METAL}")]
        public string[] METAL { get; set; }

        [JsonProperty("{MFAMILY}")]
        public string[] MFAMILY { get; set; }

        [JsonProperty("{MNONFAMILY}")]
        public string[] MNONFAMILY { get; set; }

        [JsonProperty("{MOANING}")]
        public string[] MOANING { get; set; }

        [JsonProperty("{MOANS}")]
        public string[] MOANS { get; set; }

        [JsonProperty("{MOAN}")]
        public string[] MOAN { get; set; }

        [JsonProperty("{MOUTH}")]
        public string[] MOUTH { get; set; }

        [JsonProperty("{OPENING}")]
        public string[] OPENING { get; set; }

        [JsonProperty("{PAIN}")]
        public string[] PAIN { get; set; }

        [JsonProperty("{PENIS}")]
        public string[] PENIS { get; set; }

        [JsonProperty("{PROBE}")]
        public string[] PROBE { get; set; }

        [JsonProperty("{PUSSY}")]
        public string[] PUSSY { get; set; }

        [JsonProperty("{QUIVERING}")]
        public string[] QUIVERING { get; set; }

        [JsonProperty("{RAPED}")]
        public string[] RAPED { get; set; }

        [JsonProperty("{RAPE}")]
        public string[] RAPE { get; set; }

        [JsonProperty("{SALTY}")]
        public string[] SALTY { get; set; }

        [JsonProperty("{SCREAM}")]
        public string[] SCREAM { get; set; }

        [JsonProperty("{SCREAMS}")]
        public string[] SCREAMS { get; set; }

        [JsonProperty("{SCUM}")]
        public string[] SCUM { get; set; }

        [JsonProperty("{SLIME}")]
        public string[] SLIME { get; set; }

        [JsonProperty("{SLIMY}")]
        public string[] SLIMY { get; set; }

        [JsonProperty("{SLOPPY}")]
        public string[] SLOPPY { get; set; }

        [JsonProperty("{SLOWLY}")]
        public string[] SLOWLY { get; set; }

        [JsonProperty("{SLUTTY}")]
        public string[] SLUTTY { get; set; }

        [JsonProperty("{SODOMIZED}")]
        public string[] SODOMIZED { get; set; }

        [JsonProperty("{SODOMIZES}")]
        public string[] SODOMIZES { get; set; }

        [JsonProperty("{SODOMIZE}")]
        public string[] SODOMIZE { get; set; }

        [JsonProperty("{SODOMIZING}")]
        public string[] SODOMIZING { get; set; }

        [JsonProperty("{SODOMY}")]
        public string[] SODOMY { get; set; }

        [JsonProperty("{SOLID}")]
        public string[] SOLID { get; set; }

        [JsonProperty("{STRAPON}")]
        public string[] STRAPON { get; set; }

        [JsonProperty("{SUBMISSIVE}")]
        public string[] SUBMISSIVE { get; set; }

        [JsonProperty("{SUBMIT}")]
        public string[] SUBMIT { get; set; }

        [JsonProperty("{SWEARING}")]
        public string[] SWEARING { get; set; }

        [JsonProperty("{TASTY}")]
        public string[] TASTY { get; set; }

        [JsonProperty("{THICK}")]
        public string[] THICK { get; set; }

        [JsonProperty("{TIGHTNESS}")]
        public string[] TIGHTNESS { get; set; }

        [JsonProperty("{UNTHINKING}")]
        public string[] UNTHINKING { get; set; }

        [JsonProperty("{VILE}")]
        public string[] VILE { get; set; }

        [JsonProperty("{WET}")]
        public string[] WET { get; set; }

        [JsonProperty("{WHORE}")]
        public string[] WHORE { get; set; }
    }


    public class WearAndTearItem
    {
        [JsonProperty("descriptors")]
        public WearAndTearDescriptors descriptors { get; set; }

        [JsonProperty("descriptors-mcm")]
        public string[] descriptorsmcm { get; set; }
    }

    public class WearAndTearDescriptors
    {
        public string[] level0 { get; set; }
        public string[] level1 { get; set; }
        public string[] level2 { get; set; }
        public string[] level3 { get; set; }
        public string[] level4 { get; set; }
        public string[] level5 { get; set; }
        public string[] level6 { get; set; }
        public string[] level7 { get; set; }
        public string[] level8 { get; set; }
        public string[] level9 { get; set; }
    }




    public class ArousalItem
    {
        [JsonProperty("{READINESS}")]
        public READINESS READINESS { get; set; }

        [JsonProperty("{FAROUSAL}")]
        public FAROUSAL FAROUSAL { get; set; }

        [JsonProperty("{MAROUSAL}")]
        public MAROUSAL MAROUSAL { get; set; }
    }

    public class READINESS
    {
        public string[] level0 { get; set; }
        public string[] level1 { get; set; }
        public string[] level2 { get; set; }
        public string[] level3 { get; set; }
        public string[] level4 { get; set; }
    }

    public class FAROUSAL
    {
        public string[] level0 { get; set; }
        public string[] level1 { get; set; }
        public string[] level2 { get; set; }
        public string[] level3 { get; set; }
        public string[] level4 { get; set; }
    }

    public class MAROUSAL
    {
        public string[] level0 { get; set; }
        public string[] level1 { get; set; }
        public string[] level2 { get; set; }
        public string[] level3 { get; set; }
        public string[] level4 { get; set; }
    }
}
