using System.IO;
using System.Windows;
using System.Windows.Controls;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.EngineManagement;
using PhoenixEngine.TranslateCore;

namespace Apro2Trans
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LogHelper.Init();
            DeFine.WorkWin = this;
            AproposHelper.StartUISyncService(true);
        }

        public void SetLog(string Msg)
        {
            Log.Dispatcher.Invoke(new Action(() => 
            {
                Log.Items.Add(Msg);
                Log.ScrollIntoView(Log.Items[Log.Items.Count - 1]);
            }));
        }

        public List<Languages> GetSupportedLanguages()
        {
            List<Languages> LanguageList = new List<Languages>();

            foreach (var Language in Enum.GetValues(typeof(Languages)))
            {
                LanguageList.Add((Languages)Language);
            }

            return LanguageList.OrderBy(lang => lang == Languages.Auto ? 0 : 1).ToList();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AproposHelper.TranslateApi.Init();

            EngineConfig.AutoSetThreadLimit = false;

            foreach (var GetLang in GetSupportedLanguages())
            {
                From.Items.Add(GetLang.ToString());
                To.Items.Add(GetLang.ToString());
            }

            EngineVersion.Content = Engine.Version;
        }

        private void StartTrans(object sender, RoutedEventArgs e)
        {
            EngineConfig.MaxThreadCount = ConvertHelper.ObjToInt(ThreadLimit.Text);

            if (DBPath.Text.Length > 0)
            {
                if (Directory.Exists(DBPath.Text))
                {
                    string GetPath = DBPath.Text;
                    
                    AproposHelper.ReadDB(GetPath);

                    Log.Items.Clear();
                }
            }
        }

        private void From_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string GetValue = ConvertHelper.ObjToStr(From.SelectedValue);
            if (GetValue.Length > 0)
            {
                Languages Lang = Enum.Parse<Languages>(GetValue);
                Engine.From = Lang;
            }
        }

        private void To_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string GetValue = ConvertHelper.ObjToStr(To.SelectedValue);
            if (GetValue.Length > 0)
            {
                Languages Lang = Enum.Parse<Languages>(GetValue);
                Engine.To = Lang;
            }
        }

        private void ThreadLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            EngineConfig.MaxThreadCount = ConvertHelper.ObjToInt(ThreadLimit.Text);
        }

        private void LMPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            EngineConfig.LMPort = ConvertHelper.ObjToInt(LMPort.Text);
        }
    }
}