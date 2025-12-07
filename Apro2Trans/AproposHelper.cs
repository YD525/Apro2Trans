using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using PhoenixEngine.TranslateManage;
using PhoenixEngine.EngineManagement;
using System.Security.RightsManagement;
using System.IO;
using System.Windows.Interop;
using System.Collections;
using PhoenixEngine.PlatformManagement.LocalAI;
using System.Runtime.CompilerServices;
using PhoenixEngine.DelegateManagement;

namespace Apro2Trans
{
    public class AproposHelper
    {
        public static Thread? UISyncTrd = null;
        public static Thread? TranslationSyncTrd = null;

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
                                DeFine.WorkWin.ThreadInFo.Content = string.Format("(Current:{0},Max:{1})", Engine.GetThreadCount(), EngineConfig.MaxThreadCount);
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

        public static string? ExtractContent(string AiResponseJson)
        {
            if (string.IsNullOrWhiteSpace(AiResponseJson))
                return null;

            try
            {
                using (JsonDocument Doc = JsonDocument.Parse(AiResponseJson))
                {

                    JsonElement Choices = Doc.RootElement.GetProperty("choices");
                    if (Choices.GetArrayLength() > 0)
                    {
                        JsonElement FirstChoice = Choices[0];
                        JsonElement Message = FirstChoice.GetProperty("message");
                        string Content = Message.GetProperty("content").GetString();
                        return Content;
                    }
                }
            }
            catch (JsonException ex)
            {

            }

            return null;
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
                    SynonymsItem? GetSynonyms = JsonSerializer.Deserialize<SynonymsItem>(Content);

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
                            GetSynonyms.ACCEPT[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.ACCEPTING?.Length; i++)
                    {
                        string Type = "ACCEPTING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.ACCEPTING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.ACCEPTS?.Length; i++)
                    {
                        string Type = "ACCEPTS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.ACCEPTS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.ASS?.Length; i++)
                    {
                        string Type = "ASS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.ASS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BEAST?.Length; i++)
                    {
                        string Type = "BEAST";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BEAST[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BEASTCOCK?.Length; i++)
                    {
                        string Type = "BEASTCOCK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BEASTCOCK[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BITCH?.Length; i++)
                    {
                        string Type = "BITCH";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BITCH[i] = Translateds[Key].TransText;
                        }
                    }
                    for (int i = 0; i < GetSynonyms.BOOBS?.Length; i++)
                    {
                        string Type = "BOOBS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BOOBS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BREED?.Length; i++)
                    {
                        string Type = "BREED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BREED[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BUG?.Length; i++)
                    {
                        string Type = "BUG";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BUG[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BUGCOCK?.Length; i++)
                    {
                        string Type = "BUGCOCK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BUGCOCK[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.BUTTOCKS?.Length; i++)
                    {
                        string Type = "BUTTOCKS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.BUTTOCKS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.COCK?.Length; i++)
                    {
                        string Type = "COCK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.COCK[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.CREAM?.Length; i++)
                    {
                        string Type = "CREAM";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.CREAM[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.CUM?.Length; i++)
                    {
                        string Type = "CUM";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.CUM[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.CUMMING?.Length; i++)
                    {
                        string Type = "CUMMING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.CUMMING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.CUMS?.Length; i++)
                    {
                        string Type = "CUMS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.CUMS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.DEAD?.Length; i++)
                    {
                        string Type = "DEAD";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.DEAD[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.EXPLORE?.Length; i++)
                    {
                        string Type = "EXPLORE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.EXPLORE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.EXPOSE?.Length; i++)
                    {
                        string Type = "EXPOSE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.EXPOSE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FEAR?.Length; i++)
                    {
                        string Type = "FEAR";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FEAR[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FFAMILY?.Length; i++)
                    {
                        string Type = "FFAMILY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FFAMILY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FOREIGN?.Length; i++)
                    {
                        string Type = "FOREIGN";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FOREIGN[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FUCK?.Length; i++)
                    {
                        string Type = "FUCK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FUCK[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FUCKED?.Length; i++)
                    {
                        string Type = "FUCKED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FUCKED[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FUCKING?.Length; i++)
                    {
                        string Type = "FUCKING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FUCKING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.FUCKS?.Length; i++)
                    {
                        string Type = "FUCKS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.FUCKS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.GENWT?.Length; i++)
                    {
                        string Type = "GENWT";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.GENWT[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.GIRTH?.Length; i++)
                    {
                        string Type = "GIRTH";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.GIRTH[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HEAVING?.Length; i++)
                    {
                        string Type = "HEAVING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HEAVING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HOLE?.Length; i++)
                    {
                        string Type = "HOLE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HOLE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HOLES?.Length; i++)
                    {
                        string Type = "HOLES";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HOLES[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HORNY?.Length; i++)
                    {
                        string Type = "HORNY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HORNY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HUGE?.Length; i++)
                    {
                        string Type = "HUGE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HUGE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.HUGELOAD?.Length; i++)
                    {
                        string Type = "HUGELOAD";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.HUGELOAD[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.INSERT?.Length; i++)
                    {
                        string Type = "INSERT";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.INSERT[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.INSERTED?.Length; i++)
                    {
                        string Type = "INSERTED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.INSERTED[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.INSERTING?.Length; i++)
                    {
                        string Type = "INSERTING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.INSERTING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.INSERTS?.Length; i++)
                    {
                        string Type = "INSERTS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.INSERTS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.JIGGLE?.Length; i++)
                    {
                        string Type = "JIGGLE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.JIGGLE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.JUICY?.Length; i++)
                    {
                        string Type = "JUICY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.JUICY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.LARGELOAD?.Length; i++)
                    {
                        string Type = "LARGELOAD";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.LARGELOAD[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.LOUDLY?.Length; i++)
                    {
                        string Type = "LOUDLY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.LOUDLY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MACHINE?.Length; i++)
                    {
                        string Type = "MACHINE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MACHINE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MACHINESLIME?.Length; i++)
                    {
                        string Type = "MACHINESLIME";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MACHINESLIME[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MACHINESLIMY?.Length; i++)
                    {
                        string Type = "MACHINESLIMY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MACHINESLIMY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.METAL?.Length; i++)
                    {
                        string Type = "METAL";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.METAL[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MFAMILY?.Length; i++)
                    {
                        string Type = "MFAMILY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MFAMILY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MNONFAMILY?.Length; i++)
                    {
                        string Type = "MNONFAMILY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MNONFAMILY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MOAN?.Length; i++)
                    {
                        string Type = "MOAN";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MOAN[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MOANING?.Length; i++)
                    {
                        string Type = "MOANING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MOANING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MOANS?.Length; i++)
                    {
                        string Type = "MOANS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MOANS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.MOUTH?.Length; i++)
                    {
                        string Type = "MOUTH";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.MOUTH[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.OPENING?.Length; i++)
                    {
                        string Type = "OPENING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.OPENING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.PAIN?.Length; i++)
                    {
                        string Type = "PAIN";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.PAIN[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.PENIS?.Length; i++)
                    {
                        string Type = "PENIS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.PENIS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.PROBE?.Length; i++)
                    {
                        string Type = "PROBE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.PROBE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.PUSSY?.Length; i++)
                    {
                        string Type = "PUSSY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.PUSSY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.QUIVERING?.Length; i++)
                    {
                        string Type = "QUIVERING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.QUIVERING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.RAPE?.Length; i++)
                    {
                        string Type = "RAPE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.RAPE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.RAPED?.Length; i++)
                    {
                        string Type = "RAPED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.RAPED[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SALTY?.Length; i++)
                    {
                        string Type = "SALTY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SALTY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SCREAM?.Length; i++)
                    {
                        string Type = "SCREAM";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SCREAM[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SCREAMS?.Length; i++)
                    {
                        string Type = "SCREAMS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SCREAMS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SCUM?.Length; i++)
                    {
                        string Type = "SCUM";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SCUM[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLIME?.Length; i++)
                    {
                        string Type = "SLIME";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLIME[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLIMY?.Length; i++)
                    {
                        string Type = "SLIMY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLIMY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLOPPY?.Length; i++)
                    {
                        string Type = "SLOPPY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLOPPY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLOWLY?.Length; i++)
                    {
                        string Type = "SLOWLY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLOWLY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SLUTTY?.Length; i++)
                    {
                        string Type = "SLUTTY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SLUTTY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMIZE?.Length; i++)
                    {
                        string Type = "SODOMIZE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMIZE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMIZED?.Length; i++)
                    {
                        string Type = "SODOMIZED";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMIZED[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMIZES?.Length; i++)
                    {
                        string Type = "SODOMIZES";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMIZES[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMIZING?.Length; i++)
                    {
                        string Type = "SODOMIZING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMIZING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SODOMY?.Length; i++)
                    {
                        string Type = "SODOMY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SODOMY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SOLID?.Length; i++)
                    {
                        string Type = "SOLID";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SOLID[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.STRAPON?.Length; i++)
                    {
                        string Type = "STRAPON";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.STRAPON[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SUBMISSIVE?.Length; i++)
                    {
                        string Type = "SUBMISSIVE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SUBMISSIVE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SUBMIT?.Length; i++)
                    {
                        string Type = "SUBMIT";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SUBMIT[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.SWEARING?.Length; i++)
                    {
                        string Type = "SWEARING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.SWEARING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.TASTY?.Length; i++)
                    {
                        string Type = "TASTY";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.TASTY[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.THICK?.Length; i++)
                    {
                        string Type = "THICK";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.THICK[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.TIGHTNESS?.Length; i++)
                    {
                        string Type = "TIGHTNESS";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.TIGHTNESS[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.UNTHINKING?.Length; i++)
                    {
                        string Type = "UNTHINKING";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.UNTHINKING[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.VILE?.Length; i++)
                    {
                        string Type = "VILE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.VILE[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.WET?.Length; i++)
                    {
                        string Type = "WET";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.WET[i] = Translateds[Key].TransText;
                        }
                    }

                    for (int i = 0; i < GetSynonyms.WHORE?.Length; i++)
                    {
                        string Type = "WHORE";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetSynonyms.WHORE[i] = Translateds[Key].TransText;
                        }
                    }

                    GetJson = JsonSerializer.Serialize(GetSynonyms, new JsonSerializerOptions
                    {
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true
                    });

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

                AproposItem GetApropos = JsonSerializer.Deserialize<AproposItem>(Content);

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
                            GetApropos._1stPerson[i] = Translateds[Key].TransText;
                        }
                    }

                if (GetApropos._2ndPerson != null)
                    for (int i = 0; i < GetApropos._2ndPerson?.Length; i++)
                    {
                        string Type = "2ndPerson";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetApropos._2ndPerson[i] = Translateds[Key].TransText;
                        }
                    }

                if (GetApropos._3rdPerson != null)
                    for (int i = 0; i < GetApropos._3rdPerson?.Length; i++)
                    {
                        string Type = "3rdPerson";
                        string Key = FilePath + "-" + Type + "[" + i + "]";

                        if (Translateds.ContainsKey(Key))
                        {
                            GetApropos._3rdPerson[i] = Translateds[Key].TransText;
                        }
                    }

                GetJson = JsonSerializer.Serialize(GetApropos, new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                });

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
                SynonymsItem? GetSynonyms = null;
                try
                {
                    GetSynonyms = JsonSerializer.Deserialize<SynonymsItem>(Content);
                }
                catch (Exception Ex)
                {
                //Automatic JSON syntax correction
                //[Apropos2 DB Update] - The JSON has an incorrect format... 
                //Since we're already here, let's just use AI to fix it without thinking.
                TryAgain:
                    LMStudio NLMStudio = new LMStudio();

                    string? RecvMsg = "";

                    NLMStudio.CallAI(Prompt, ref RecvMsg);

                    if (RecvMsg != null)
                    {
                        string? GetAIResult = ExtractContent(RecvMsg);
                        if (GetAIResult != null)
                        {
                            //Input the AI-repaired JSON
                            //If you're wrong, just go to.
                            try
                            {
                                GetSynonyms = JsonSerializer.Deserialize<SynonymsItem>(GetAIResult);

                                //Write the repaired JSON
                                var GetJson = JsonSerializer.Serialize(GetSynonyms, new JsonSerializerOptions
                                {
                                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                    WriteIndented = true
                                });

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

            AproposItem? GetApropos = null;

            try
            {
                GetApropos = JsonSerializer.Deserialize<AproposItem>(Content);
            }
            catch (Exception Ex)
            {
            //Automatic JSON syntax correction
            //[Apropos2 DB Update] - The JSON has an incorrect format... 
            //Since we're already here, let's just use AI to fix it without thinking.
            TryAgain:
                LMStudio NLMStudio = new LMStudio();
                string? RecvMsg = "";
                NLMStudio.CallAI(Prompt, ref RecvMsg);

                if (RecvMsg != null)
                {
                    string? GetAIResult = ExtractContent(RecvMsg);
                    if (GetAIResult != null)
                    {
                        //Input the AI-repaired JSON
                        //If you're wrong, just go to.
                        try
                        {
                            GetApropos = JsonSerializer.Deserialize<AproposItem>(GetAIResult);

                            //Write the repaired JSON
                            var GetJson = JsonSerializer.Serialize(GetApropos, new JsonSerializerOptions
                            {
                                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                WriteIndented = true
                            });

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
        [JsonPropertyName("1st Person")]
        public string[] _1stPerson { get; set; }

        [JsonPropertyName("2nd Person")]
        public string[] _2ndPerson { get; set; }

        [JsonPropertyName("3rd Person")]
        public string[] _3rdPerson { get; set; }
    }



    public class SynonymsItem
    {
        [JsonPropertyName("{ACCEPTS}")]
        public string[] ACCEPTS { get; set; }

        [JsonPropertyName("{ACCEPT}")]
        public string[] ACCEPT { get; set; }

        [JsonPropertyName("{ACCEPTING}")]
        public string[] ACCEPTING { get; set; }

        [JsonPropertyName("{ASS}")]
        public string[] ASS { get; set; }

        [JsonPropertyName("{BEASTCOCK}")]
        public string[] BEASTCOCK { get; set; }

        [JsonPropertyName("{BEAST}")]
        public string[] BEAST { get; set; }

        [JsonPropertyName("{BITCH}")]
        public string[] BITCH { get; set; }

        [JsonPropertyName("{BOOBS}")]
        public string[] BOOBS { get; set; }

        [JsonPropertyName("{BREED}")]
        public string[] BREED { get; set; }

        [JsonPropertyName("{BUGCOCK}")]
        public string[] BUGCOCK { get; set; }

        [JsonPropertyName("{BUG}")]
        public string[] BUG { get; set; }

        [JsonPropertyName("{BUTTOCKS}")]
        public string[] BUTTOCKS { get; set; }

        [JsonPropertyName("{COCK}")]
        public string[] COCK { get; set; }

        [JsonPropertyName("{CREAM}")]
        public string[] CREAM { get; set; }

        [JsonPropertyName("{CUMMING}")]
        public string[] CUMMING { get; set; }

        [JsonPropertyName("{CUMS}")]
        public string[] CUMS { get; set; }

        [JsonPropertyName("{CUM}")]
        public string[] CUM { get; set; }

        [JsonPropertyName("{DEAD}")]
        public string[] DEAD { get; set; }

        [JsonPropertyName("{EXPLORE}")]
        public string[] EXPLORE { get; set; }

        [JsonPropertyName("{EXPOSE}")]
        public string[] EXPOSE { get; set; }

        [JsonPropertyName("{FEAR}")]
        public string[] FEAR { get; set; }

        [JsonPropertyName("{FFAMILY}")]
        public string[] FFAMILY { get; set; }

        [JsonPropertyName("{FOREIGN}")]
        public string[] FOREIGN { get; set; }

        [JsonPropertyName("{FUCKED}")]
        public string[] FUCKED { get; set; }

        [JsonPropertyName("{FUCKING}")]
        public string[] FUCKING { get; set; }

        [JsonPropertyName("{FUCKS}")]
        public string[] FUCKS { get; set; }

        [JsonPropertyName("{FUCK}")]
        public string[] FUCK { get; set; }

        [JsonPropertyName("{GENWT}")]
        public string[] GENWT { get; set; }

        [JsonPropertyName("{GIRTH}")]
        public string[] GIRTH { get; set; }

        [JsonPropertyName("{HEAVING}")]
        public string[] HEAVING { get; set; }

        [JsonPropertyName("{HOLE}")]
        public string[] HOLE { get; set; }

        [JsonPropertyName("{HOLES}")]
        public string[] HOLES { get; set; }

        [JsonPropertyName("{HORNY}")]
        public string[] HORNY { get; set; }

        [JsonPropertyName("{HUGELOAD}")]
        public string[] HUGELOAD { get; set; }

        [JsonPropertyName("{HUGE}")]
        public string[] HUGE { get; set; }

        [JsonPropertyName("{INSERT}")]
        public string[] INSERT { get; set; }

        [JsonPropertyName("{INSERTS}")]
        public string[] INSERTS { get; set; }

        [JsonPropertyName("{INSERTED}")]
        public string[] INSERTED { get; set; }

        [JsonPropertyName("{INSERTING}")]
        public string[] INSERTING { get; set; }

        [JsonPropertyName("{JIGGLE}")]
        public string[] JIGGLE { get; set; }

        [JsonPropertyName("{JUICY}")]
        public string[] JUICY { get; set; }

        [JsonPropertyName("{LARGELOAD}")]
        public string[] LARGELOAD { get; set; }

        [JsonPropertyName("{LOUDLY}")]
        public string[] LOUDLY { get; set; }

        [JsonPropertyName("{MACHINESLIME}")]
        public string[] MACHINESLIME { get; set; }

        [JsonPropertyName("{MACHINESLIMY}")]
        public string[] MACHINESLIMY { get; set; }

        [JsonPropertyName("{MACHINE}")]
        public string[] MACHINE { get; set; }

        [JsonPropertyName("{METAL}")]
        public string[] METAL { get; set; }

        [JsonPropertyName("{MFAMILY}")]
        public string[] MFAMILY { get; set; }

        [JsonPropertyName("{MNONFAMILY}")]
        public string[] MNONFAMILY { get; set; }

        [JsonPropertyName("{MOANING}")]
        public string[] MOANING { get; set; }

        [JsonPropertyName("{MOANS}")]
        public string[] MOANS { get; set; }

        [JsonPropertyName("{MOAN}")]
        public string[] MOAN { get; set; }

        [JsonPropertyName("{MOUTH}")]
        public string[] MOUTH { get; set; }

        [JsonPropertyName("{OPENING}")]
        public string[] OPENING { get; set; }

        [JsonPropertyName("{PAIN}")]
        public string[] PAIN { get; set; }

        [JsonPropertyName("{PENIS}")]
        public string[] PENIS { get; set; }

        [JsonPropertyName("{PROBE}")]
        public string[] PROBE { get; set; }

        [JsonPropertyName("{PUSSY}")]
        public string[] PUSSY { get; set; }

        [JsonPropertyName("{QUIVERING}")]
        public string[] QUIVERING { get; set; }

        [JsonPropertyName("{RAPED}")]
        public string[] RAPED { get; set; }

        [JsonPropertyName("{RAPE}")]
        public string[] RAPE { get; set; }

        [JsonPropertyName("{SALTY}")]
        public string[] SALTY { get; set; }

        [JsonPropertyName("{SCREAM}")]
        public string[] SCREAM { get; set; }

        [JsonPropertyName("{SCREAMS}")]
        public string[] SCREAMS { get; set; }

        [JsonPropertyName("{SCUM}")]
        public string[] SCUM { get; set; }

        [JsonPropertyName("{SLIME}")]
        public string[] SLIME { get; set; }

        [JsonPropertyName("{SLIMY}")]
        public string[] SLIMY { get; set; }

        [JsonPropertyName("{SLOPPY}")]
        public string[] SLOPPY { get; set; }

        [JsonPropertyName("{SLOWLY}")]
        public string[] SLOWLY { get; set; }

        [JsonPropertyName("{SLUTTY}")]
        public string[] SLUTTY { get; set; }

        [JsonPropertyName("{SODOMIZED}")]
        public string[] SODOMIZED { get; set; }

        [JsonPropertyName("{SODOMIZES}")]
        public string[] SODOMIZES { get; set; }

        [JsonPropertyName("{SODOMIZE}")]
        public string[] SODOMIZE { get; set; }

        [JsonPropertyName("{SODOMIZING}")]
        public string[] SODOMIZING { get; set; }

        [JsonPropertyName("{SODOMY}")]
        public string[] SODOMY { get; set; }

        [JsonPropertyName("{SOLID}")]
        public string[] SOLID { get; set; }

        [JsonPropertyName("{STRAPON}")]
        public string[] STRAPON { get; set; }

        [JsonPropertyName("{SUBMISSIVE}")]
        public string[] SUBMISSIVE { get; set; }

        [JsonPropertyName("{SUBMIT}")]
        public string[] SUBMIT { get; set; }

        [JsonPropertyName("{SWEARING}")]
        public string[] SWEARING { get; set; }

        [JsonPropertyName("{TASTY}")]
        public string[] TASTY { get; set; }

        [JsonPropertyName("{THICK}")]
        public string[] THICK { get; set; }

        [JsonPropertyName("{TIGHTNESS}")]
        public string[] TIGHTNESS { get; set; }

        [JsonPropertyName("{UNTHINKING}")]
        public string[] UNTHINKING { get; set; }

        [JsonPropertyName("{VILE}")]
        public string[] VILE { get; set; }

        [JsonPropertyName("{WET}")]
        public string[] WET { get; set; }

        [JsonPropertyName("{WHORE}")]
        public string[] WHORE { get; set; }
    }


    public class WearAndTearItem
    {
        [JsonPropertyName("descriptors")]
        public WearAndTearDescriptors descriptors { get; set; }

        [JsonPropertyName("descriptors-mcm")]
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
        [JsonPropertyName("{READINESS}")]
        public READINESS READINESS { get; set; }

        [JsonPropertyName("{FAROUSAL}")]
        public FAROUSAL FAROUSAL { get; set; }

        [JsonPropertyName("{MAROUSAL}")]
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
