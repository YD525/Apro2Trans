using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhoenixEngine.DelegateManagement;
using System.Windows;
using PhoenixEngine.TranslateManage;
using static PhoenixEngine.EngineManagement.DataTransmission;

namespace Apro2Trans
{
    public class RecvListener
    {
        public string Key = "";
        public List<int> ActiveIDs = new List<int>();
        public Action<int, object> Method = null;

        public RecvListener(string Key, List<int> ActiveIDs, Action<int, object> Func)
        {
            this.Key = Key;
            this.ActiveIDs = ActiveIDs;
            this.Method = Func;
        }
    }
    public class LogHelper
    {

        private static ReaderWriterLockSlim ListenersLock = new ReaderWriterLockSlim();
        public static List<RecvListener> RecvListeners = new List<RecvListener>();

        public static void Init()
        {
            DelegateHelper.SetDataCall += Recv;
            DelegateHelper.SetTranslationUnitCallBack += TranslationUnitStartWorkCall;

            RegListener("InputOutputLog", new List<int>() { 3, 5 }, new Action<int, object>((Sign, Any) =>
            {
                if (Sign == 5 || Sign == 3)
                {
                    if (Any is AICall)
                    {
                        AICall GetCall = (AICall)Any;

                        if (GetCall.SendString.Length > 0)
                        {
                            DeFine.WorkWin.Input.Dispatcher.Invoke(new Action(() => {
                                DeFine.WorkWin.Input.Text = GetCall.SendString;
                            }));
                        }
                    }
                    if (Any is PlatformCall)
                    {
                        PlatformCall GetCall = (PlatformCall)Any;
                    }
                }
            }));
        }

        public static void Recv(int Sign, object Any)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    for (int i = 0; i < RecvListeners.Count; i++)
                    {
                        if (RecvListeners[i].ActiveIDs.Contains(Sign))
                        {
                            RecvListeners[i].Method.Invoke(Sign, Any);
                        }
                    }
                }
                catch { }
            });
        }

        public static bool TranslationUnitStartWorkCall(TranslationUnit Item)
        {
            return true;
        }

        public static void RegListener(string Key, List<int> ActiveIDs, Action<int, object> Action)
        {
            ListenersLock.EnterWriteLock();
            try
            {
                foreach (var Get in RecvListeners)
                {
                    if (Get.Key.Equals(Key))
                    {
                        return;
                    }
                }

                RecvListeners.Add(new RecvListener(Key, ActiveIDs, Action));
            }
            finally
            {
                ListenersLock.ExitWriteLock();
            }
        }
    }
}
