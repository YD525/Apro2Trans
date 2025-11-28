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

namespace Apro2Trans
{
    public class AproposHelper
    {

        public static SSELexApi TranslateApi = new SSELexApi();
        public static void TranslatePath(ProgressBar OneBar, string FilePath, string Suffix = ".txt")
        {
            new Thread(() => {

                int Sucess = 0;
                var GetFiles = DataHelper.GetAllFile(FilePath, new List<string>() { Suffix });

                OneBar.Dispatcher.Invoke(new Action(() => {
                    OneBar.Maximum = GetFiles.Count;
                }));


                foreach (var Get in GetFiles)
                {
                    string GetContent = DataHelper.ReadFileByStr(Get.FilePath, Encoding.UTF8);

                    GetContent = ProcessAproposCode(Get.FilePath, Get.FileName, GetContent);
                    DataHelper.WriteFile(Get.FilePath, Encoding.UTF8.GetBytes(GetContent));
                   
                    Sucess++;

                    OneBar.Dispatcher.Invoke(new Action(() => {
                        OneBar.Value = Sucess;
                    }));
                }

            }).Start();
        }

        public static string ProcessAproposCode(string FilePath,string FileName, string Content)
        {
            if (FileName == "Synonyms.txt")
            {
                SynonymsItem GetSynonyms = JsonSerializer.Deserialize<SynonymsItem>(Content);

                var Options = new JsonSerializerOptions
                {
                    WriteIndented = true 
                };

                for (int i = 0; i < GetSynonyms.ACCEPT.Length; i++)
                {
                    string GetOriginal = GetSynonyms.ACCEPT[i];      
                    TranslateApi.Enqueue("Synonyms.txt", FilePath + i, "ACCEPT", GetOriginal, string.Empty);
                }

                for (int i = 0; i < GetSynonyms.ACCEPTING.Length; i++)
                {
                    string GetOriginal = GetSynonyms.ACCEPTING[i]; 
                }

                for (int i = 0; i < GetSynonyms.ACCEPTS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.ACCEPTS[i];
                }

                for (int i = 0; i < GetSynonyms.ASS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.ASS[i];
                }

                for (int i = 0; i < GetSynonyms.BEAST.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BEAST[i];
                }

                for (int i = 0; i < GetSynonyms.BEASTCOCK.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BEASTCOCK[i];
                }

                for (int i = 0; i < GetSynonyms.BITCH.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BITCH[i];
                }
                for (int i = 0; i < GetSynonyms.BOOBS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BOOBS[i];
                }

                for (int i = 0; i < GetSynonyms.BREED.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BREED[i];
                }

                for (int i = 0; i < GetSynonyms.BUG.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BUG[i];
                }

                for (int i = 0; i < GetSynonyms.BUGCOCK.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BUGCOCK[i];
                }

                for (int i = 0; i < GetSynonyms.BUTTOCKS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.BUTTOCKS[i];
                }

                for (int i = 0; i < GetSynonyms.COCK.Length; i++)
                {
                    string GetOriginal = GetSynonyms.COCK[i];
                }

                for (int i = 0; i < GetSynonyms.CREAM.Length; i++)
                {
                    string GetOriginal = GetSynonyms.CREAM[i];                
                }

                for (int i = 0; i < GetSynonyms.CUM.Length; i++)
                {
                    string GetOriginal = GetSynonyms.CUM[i];    
                }

                for (int i = 0; i < GetSynonyms.CUMMING.Length; i++)
                {
                    string GetOriginal = GetSynonyms.CUMMING[i];           
                }

                for (int i = 0; i < GetSynonyms.CUMS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.CUMS[i];
                }

                for (int i = 0; i < GetSynonyms.DEAD.Length; i++)
                {
                    string GetOriginal = GetSynonyms.DEAD[i];
                }

                for (int i = 0; i < GetSynonyms.EXPLORE.Length; i++)
                {
                    string GetOriginal = GetSynonyms.EXPLORE[i];
                }

                for (int i = 0; i < GetSynonyms.EXPOSE.Length; i++)
                {
                    string GetOriginal = GetSynonyms.EXPOSE[i];
                }

                for (int i = 0; i < GetSynonyms.FEAR.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FEAR[i];
                }

                for (int i = 0; i < GetSynonyms.FFAMILY.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FFAMILY[i];
                }

                for (int i = 0; i < GetSynonyms.FOREIGN.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FOREIGN[i];
                }

                for (int i = 0; i < GetSynonyms.FUCK.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FUCK[i];
                }

                for (int i = 0; i < GetSynonyms.FUCKED.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FUCKED[i];
                }

                for (int i = 0; i < GetSynonyms.FUCKING.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FUCKING[i];
                }

                for (int i = 0; i < GetSynonyms.FUCKS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.FUCKS[i];
                }

                for (int i = 0; i < GetSynonyms.GENWT.Length; i++)
                {
                    string GetOriginal = GetSynonyms.GENWT[i];
                }

                for (int i = 0; i < GetSynonyms.GIRTH.Length; i++)
                {
                    string GetOriginal = GetSynonyms.GIRTH[i];
                }

                for (int i = 0; i < GetSynonyms.HEAVING.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HEAVING[i];
                }

                for (int i = 0; i < GetSynonyms.HOLE.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HOLE[i];
                }

                for (int i = 0; i < GetSynonyms.HOLES.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HOLES[i];
                }

                for (int i = 0; i < GetSynonyms.HORNY.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HORNY[i];
                }

                for (int i = 0; i < GetSynonyms.HUGE.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HUGE[i];
                }

                for (int i = 0; i < GetSynonyms.HUGELOAD.Length; i++)
                {
                    string GetOriginal = GetSynonyms.HUGELOAD[i];
                }

                for (int i = 0; i < GetSynonyms.INSERT.Length; i++)
                {
                    string GetOriginal = GetSynonyms.INSERT[i];
                }

                for (int i = 0; i < GetSynonyms.INSERTED.Length; i++)
                {
                    string GetOriginal = GetSynonyms.INSERTED[i];
                }

                for (int i = 0; i < GetSynonyms.INSERTING.Length; i++)
                {
                    string GetOriginal = GetSynonyms.INSERTING[i];
                }

                for (int i = 0; i < GetSynonyms.INSERTS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.INSERTS[i];
                }

                for (int i = 0; i < GetSynonyms.JIGGLE.Length; i++)
                {
                    string GetOriginal = GetSynonyms.JIGGLE[i];
                }

                for (int i = 0; i < GetSynonyms.JUICY.Length; i++)
                {
                    string GetOriginal = GetSynonyms.JUICY[i];
                }

                for (int i = 0; i < GetSynonyms.LARGELOAD.Length; i++)
                {
                    string GetOriginal = GetSynonyms.LARGELOAD[i];
                }

                for (int i = 0; i < GetSynonyms.LOUDLY.Length; i++)
                {
                    string GetOriginal = GetSynonyms.LOUDLY[i];
                }

                for (int i = 0; i < GetSynonyms.MACHINE.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MACHINE[i];
                }

                for (int i = 0; i < GetSynonyms.MACHINESLIME.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MACHINESLIME[i];
                }

                for (int i = 0; i < GetSynonyms.MACHINESLIMY.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MACHINESLIMY[i];
                }

                for (int i = 0; i < GetSynonyms.METAL.Length; i++)
                {
                    string GetOriginal = GetSynonyms.METAL[i];
                }

                for (int i = 0; i < GetSynonyms.MFAMILY.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MFAMILY[i];
                }

                for (int i = 0; i < GetSynonyms.MNONFAMILY.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MNONFAMILY[i];
                }

                for (int i = 0; i < GetSynonyms.MOAN.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MOAN[i];
                }

                for (int i = 0; i < GetSynonyms.MOANING.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MOANING[i];
                }

                for (int i = 0; i < GetSynonyms.MOANS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MOANS[i];
                }

                for (int i = 0; i < GetSynonyms.MOUTH.Length; i++)
                {
                    string GetOriginal = GetSynonyms.MOUTH[i];
                }

                for (int i = 0; i < GetSynonyms.OPENING.Length; i++)
                {
                    string GetOriginal = GetSynonyms.OPENING[i];
                }

                for (int i = 0; i < GetSynonyms.PAIN.Length; i++)
                {
                    string GetOriginal = GetSynonyms.PAIN[i];
                }

                for (int i = 0; i < GetSynonyms.PENIS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.PENIS[i];
                }

                for (int i = 0; i < GetSynonyms.PROBE.Length; i++)
                {
                    string GetOriginal = GetSynonyms.PROBE[i];
                }

                for (int i = 0; i < GetSynonyms.PUSSY.Length; i++)
                {
                    string GetOriginal = GetSynonyms.PUSSY[i];
                }

                for (int i = 0; i < GetSynonyms.QUIVERING.Length; i++)
                {
                    string GetOriginal = GetSynonyms.QUIVERING[i];
                }

                for (int i = 0; i < GetSynonyms.RAPE.Length; i++)
                {
                    string GetOriginal = GetSynonyms.RAPE[i];
                }

                for (int i = 0; i < GetSynonyms.RAPED.Length; i++)
                {
                    string GetOriginal = GetSynonyms.RAPED[i];
                }

                for (int i = 0; i < GetSynonyms.SALTY.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SALTY[i];
                }

                for (int i = 0; i < GetSynonyms.SCREAM.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SCREAM[i];
                }

                for (int i = 0; i < GetSynonyms.SCREAMS.Length; i++)
                {
                    string GetOriginal = GetSynonyms.SCREAMS[i];
                }

                for (int i = 0; i < GetSynonyms.SCUM.Length; i++)
                {
                    GetSynonyms.SCUM[i] = GetAproposTranslate(GetSynonyms.SCUM[i]);
                }

                for (int i = 0; i < GetSynonyms.SLIME.Length; i++)
                {
                    GetSynonyms.SLIME[i] = GetAproposTranslate(GetSynonyms.SLIME[i]);
                }

                for (int i = 0; i < GetSynonyms.SLIMY.Length; i++)
                {
                    GetSynonyms.SLIMY[i] = GetAproposTranslate(GetSynonyms.SLIMY[i]);
                }

                for (int i = 0; i < GetSynonyms.SLOPPY.Length; i++)
                {
                    GetSynonyms.SLOPPY[i] = GetAproposTranslate(GetSynonyms.SLOPPY[i]);
                }

                for (int i = 0; i < GetSynonyms.SLOWLY.Length; i++)
                {
                    GetSynonyms.SLOWLY[i] = GetAproposTranslate(GetSynonyms.SLOWLY[i]);
                }

                for (int i = 0; i < GetSynonyms.SLUTTY.Length; i++)
                {
                    GetSynonyms.SLUTTY[i] = GetAproposTranslate(GetSynonyms.SLUTTY[i]);
                }

                for (int i = 0; i < GetSynonyms.SODOMIZE.Length; i++)
                {
                    GetSynonyms.SODOMIZE[i] = GetAproposTranslate(GetSynonyms.SODOMIZE[i]);
                }

                for (int i = 0; i < GetSynonyms.SODOMIZED.Length; i++)
                {
                    GetSynonyms.SODOMIZED[i] = GetAproposTranslate(GetSynonyms.SODOMIZED[i]);
                }

                for (int i = 0; i < GetSynonyms.SODOMIZES.Length; i++)
                {
                    GetSynonyms.SODOMIZES[i] = GetAproposTranslate(GetSynonyms.SODOMIZES[i]);
                }

                for (int i = 0; i < GetSynonyms.SODOMIZING.Length; i++)
                {
                    GetSynonyms.SODOMIZING[i] = GetAproposTranslate(GetSynonyms.SODOMIZING[i]);
                }

                for (int i = 0; i < GetSynonyms.SODOMY.Length; i++)
                {
                    GetSynonyms.SODOMY[i] = GetAproposTranslate(GetSynonyms.SODOMY[i]);
                }

                for (int i = 0; i < GetSynonyms.SOLID.Length; i++)
                {
                    GetSynonyms.SOLID[i] = GetAproposTranslate(GetSynonyms.SOLID[i]);
                }

                for (int i = 0; i < GetSynonyms.STRAPON.Length; i++)
                {
                    GetSynonyms.STRAPON[i] = GetAproposTranslate(GetSynonyms.STRAPON[i]);
                }

                for (int i = 0; i < GetSynonyms.SUBMISSIVE.Length; i++)
                {
                    GetSynonyms.SUBMISSIVE[i] = GetAproposTranslate(GetSynonyms.SUBMISSIVE[i]);
                }

                for (int i = 0; i < GetSynonyms.SUBMIT.Length; i++)
                {
                    GetSynonyms.SUBMIT[i] = GetAproposTranslate(GetSynonyms.SUBMIT[i]);
                }

                for (int i = 0; i < GetSynonyms.SWEARING.Length; i++)
                {
                    GetSynonyms.SWEARING[i] = GetAproposTranslate(GetSynonyms.SWEARING[i]);
                }

                for (int i = 0; i < GetSynonyms.TASTY.Length; i++)
                {
                    GetSynonyms.TASTY[i] = GetAproposTranslate(GetSynonyms.TASTY[i]);
                }

                for (int i = 0; i < GetSynonyms.THICK.Length; i++)
                {
                    GetSynonyms.THICK[i] = GetAproposTranslate(GetSynonyms.THICK[i]);
                }

                for (int i = 0; i < GetSynonyms.TIGHTNESS.Length; i++)
                {
                    GetSynonyms.TIGHTNESS[i] = GetAproposTranslate(GetSynonyms.TIGHTNESS[i]);
                }

                for (int i = 0; i < GetSynonyms.UNTHINKING.Length; i++)
                {
                    GetSynonyms.UNTHINKING[i] = GetAproposTranslate(GetSynonyms.UNTHINKING[i]);
                }

                for (int i = 0; i < GetSynonyms.VILE.Length; i++)
                {
                    GetSynonyms.VILE[i] = GetAproposTranslate(GetSynonyms.VILE[i]);
                }

                for (int i = 0; i < GetSynonyms.WET.Length; i++)
                {
                    GetSynonyms.WET[i] = GetAproposTranslate(GetSynonyms.WET[i]);
                }

                for (int i = 0; i < GetSynonyms.WHORE.Length; i++)
                {
                    GetSynonyms.WHORE[i] = GetAproposTranslate(GetSynonyms.WHORE[i]);
                }

                string GetJson = JsonSerializer.Serialize(GetSynonyms, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                return GetJson;
            }
            else
            if (FileName == "WearAndTear_Descriptors.txt")
            {
                WearAndTearItem GetWearAndTear = JsonSerializer.Deserialize<WearAndTearItem>(Content);

                string GetJson = JsonSerializer.Serialize(GetWearAndTear, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                return GetJson;
            }
            else
            if (FileName == "Arousal_Descriptors.txt")
            {
                ArousalItem GetArousal = JsonSerializer.Deserialize<ArousalItem>(Content);

                //Process

                string GetJson = JsonSerializer.Serialize(GetArousal, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                return GetJson;
            }
            else
            {
                if (!Content.Contains("1st Person"))
                {
                    return Content;
                }
            }


            AproposItem GetApropos = JsonSerializer.Deserialize<AproposItem>(Content);

            if (GetApropos._1stPerson != null)
                for (int i = 0; i < GetApropos._1stPerson.Length; i++)
                {
                    string GetLine = GetApropos._1stPerson[i];

                    GetApropos._1stPerson[i] = GetAproposTranslate(GetLine);
                }

            if (GetApropos._2ndPerson != null)
                for (int i = 0; i < GetApropos._2ndPerson.Length; i++)
                {
                    string GetLine = GetApropos._2ndPerson[i];

                    GetApropos._2ndPerson[i] = GetAproposTranslate(GetLine);
                }

            if (GetApropos._3rdPerson != null)
                for (int i = 0; i < GetApropos._3rdPerson.Length; i++)
                {
                    string GetLine = GetApropos._3rdPerson[i];

                    GetApropos._3rdPerson[i] = GetAproposTranslate(GetLine);
                }


            string GetJsonA = JsonSerializer.Serialize(GetApropos, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return GetJsonA;
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
