using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SearchHelper
{
    /// <summary>
    /// TranslationWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TranslationWindow : Window
    {
        Queue TranslationQueue = Application.Current.Properties["TranslationQueue"] as Queue;
        long CurrentIndex = 0;
        public TranslationWindow()
        {
            InitializeComponent();
            Task.Factory.StartNew(TranslationWorker);
        }

        void TranslationWorker()
        {
            while (true)
            {
                HelperObject ho;
                if (GetKeyWordFromQueue(out ho))
                {
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        TranslationBrowser.Navigate("http://dict.youdao.com/s.brief?q=" + WebUtility.UrlEncode(ho.KeyWord) + "&keyfrom=ie8.activity.brief");
                    }));
                    //GetBodyStringFromKeyWord(ho.KeyWord).ContinueWith(task =>
                    //    {
                    //        string result = task.Result;
                    //        if (result != null)
                    //        {
                    //            if (ho.Index >= CurrentIndex)
                    //            {
                    //                CurrentIndex = ho.Index;
                    //                Dispatcher.BeginInvoke(new Action(() =>
                    //                {
                    //                    TranslationBrowser.NavigateToString(result);
                    //                }));

                    //            }
                    //        }
                    //    }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                }

                Thread.Sleep(200);
            }
        }

        bool GetKeyWordFromQueue(out HelperObject ho)
        {
            ho = null;
            if (TranslationQueue != null)
            {
                lock (TranslationQueue.SyncRoot)
                {
                    while (TranslationQueue.Count > 0)
                    {
                        ho = TranslationQueue.Dequeue() as HelperObject;
                    }
                }
            }
            if (ho != null)
            {
                Debug.WriteLineIf(ho != null, "SH trans get--Index=" + ho.Index.ToString() + "KeyWord=" + ho.KeyWord);
            }
            return ho != null;
        }

        Task<string> GetBodyStringFromKeyWord(string keyWord)
        {
            return Task<string>.Factory.StartNew(() =>
                {
                    WebClient webClient = new WebClient();
                    webClient.Encoding = Encoding.UTF8;
                    try
                    {
                        string dataString = webClient.DownloadString("http://dict.youdao.com/s.brief?q=" + keyWord + "&keyfrom=ie8.activity.brief");
                        return TransFormString(dataString);
                    }
                    catch
                    {
                        return null;
                    }
                });
        }

        string TransFormString(string data)
        {
            if (data == null)
            {
                return null;
            }
            int start = data.IndexOf("<table cellpadding=0 cellspacing=0 border=0 width=\"320px\" >");
            int end = data.IndexOf("table", start + 6);
            Debug.Assert(start != -1 && end != -1, "去掉有道标头失败");
            return data.Remove(start, end - start + 6);
        }

        private void WebBrowser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            string script = "document.body.style.overflow ='hidden'";
            WebBrowser wb = (WebBrowser)sender;
            wb.InvokeScript("execScript", new Object[] { script, "JavaScript" });
        }
    }
}
