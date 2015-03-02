using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Web;
using System.Runtime.Serialization;
using System.Runtime;
using System.Web.Script.Serialization;
using System.Threading;
using System.Collections;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Net.NetworkInformation;


namespace SearchHelper
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        SearchEngine SearchEngine = SearchEngine.BaiDu;
        long CurrentIndex = 0;
        Queue<HelperObject> SuggestionQueue = new Queue<HelperObject>();
        Queue TranslationQueue = Application.Current.Properties["TranslationQueue"] as Queue;
        TranslationWindow TranslationWindow = new TranslationWindow();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string keyWord = (sender as TextBox).Text;
            if (keyWord != string.Empty)
            {
                HelperObject ho = new HelperObject(keyWord);
                SuggestionQueue.Enqueue(ho);
                EnqueueToTranslation(ho);
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        Search(SearchInput.Text);
                        e.Handled = true;
                        break;
                    }
                case Key.Up:
                    {
                        if (SuggestListView.HasItems)
                        {
                            SuggestListView.SelectedItem = SuggestListView.Items[SuggestListView.Items.Count - 1];
                            (SuggestListView.ItemContainerGenerator.ContainerFromIndex(SuggestListView.Items.Count - 1) as ListBoxItem).Focus();
                            e.Handled = true;
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        void Search(string keyWord)
        {
            ShowORHideWindow(false);
            string url;
            switch (SearchEngine)
            {
                case SearchEngine.BaiDu:
                    url = @"http://www.baidu.com/s?wd=" + WebUtility.UrlEncode(keyWord) + @"&ie=utf-8";
                    break;
                case SearchEngine.Google:
                    url = @"http://www.google.com/search?q=" + WebUtility.UrlEncode(keyWord) + "&ie=utf-8&oe=utf-8";
                    break;
                default:
                    url = @"http://www.baidu.com/s?wd=" + WebUtility.UrlEncode(keyWord) + @"&ie=utf-8";
                    break;
            }
            System.Diagnostics.Process.Start(url);
            Clean();
        }

        void Clean()
        {
            SearchInput.Text = null;
            SuggestionQueue.Clear();
            SuggestListView.Items.Clear();
        }

        Task<List<string>> GetSuggestion(string keyword)
        {
            return Task.Factory.StartNew(() =>
                {
                    string urlString = @"http://suggestion.baidu.com/su?wd=" + WebUtility.UrlEncode(keyword) + "&action=opensearch&ie=utf-8&from=ie8";
                    WebClient suggestionClient = new WebClient();
                    byte[] bytes = suggestionClient.DownloadData(urlString);
                    string jsonString = UTF8Encoding.UTF8.GetString(bytes);

                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    object[] data = serializer.Deserialize<object[]>(jsonString);
                    if (data != null && data.Length == 2)
                    {
                        object[] datas = data[1] as object[];
                        if (datas != null && datas.Length > 0)
                        {
                            List<string> rcl = new List<string>();
                            foreach (var dob in datas)
                            {
                                rcl.Add(dob as string);
                            }

                            return rcl;
                        }
                    }
                    return new List<string>();
                });
        }

        void SuggestionWorker()
        {
            while (true)
            {
                HelperObject ho;
                if (GetHelperObjectFromSearchQueue(out ho))
                {
                    GetSuggestion(ho.KeyWord).ContinueWith(task =>
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                List<string> rc = task.Result;
                                if (rc.Count > 0 && CurrentIndex <= ho.Index)
                                {
                                    rc.Reverse();
                                    SuggestListView.Items.Clear();
                                    foreach (var item in rc)
                                    {
                                        SuggestListView.Items.Add(item);
                                    }
                                    CurrentIndex = ho.Index;
                                }
                            }), null);
                        });
                }

                Thread.Sleep(200);
            }
        }

        bool GetHelperObjectFromSearchQueue(out HelperObject ho)
        {
            ho = null;
            while (SuggestionQueue.Count > 0)
            {
                ho = SuggestionQueue.Dequeue();
            }
            return ho != null;
        }

        private void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(SuggestionWorker);
        }

        private void SearchHelper_Loaded(object sender, RoutedEventArgs e)
        {
            new HotKey(this, HotKey.KeyFlags.MOD_WIN, System.Windows.Forms.Keys.Oem3).OnHotKey += new HotKey.OnHotKeyEventHandler(() =>
            {
                ShowORHideWindow(true);
                SearchHelper.Activate();
                SearchInput.Focus();
            });

            this.Top = 750;
            this.Left = 50;
            ShowORHideWindow(false);
            //this.Hide();
            SwitchSearchEngine();
        }

        void SwitchSearchEngine()
        {
            Ping pin = new Ping();
            pin.SendPingAsync("www.google.com").ContinueWith(task =>
                {
                    if (task.Result.Status == IPStatus.Success)
                    {
                        SearchEngine = SearchEngine.Google;
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void ShowORHideWindow(bool isShow)
        {
            SearchHelper.Visibility = isShow ? Visibility.Visible : Visibility.Hidden;
            //SearchHelper.WindowState = isShow ? System.Windows.WindowState.Normal : System.Windows.WindowState.Minimized;
            //SearchHelper.Opacity = isShow ? 1 : 0;
            if (isShow)
            {
                Point vp = new Point(SearchHelper.ActualWidth - 323, 0);
                var np = SearchHelper.PointToScreen(vp);
                TranslationWindow.Left = np.X;
                TranslationWindow.Top = np.Y;
                TranslationWindow.Show();
            }
            else
            {
                TranslationWindow.Hide();
            }
            
        }

        void EnqueueToTranslation(HelperObject ho)
        {
            if (TranslationQueue != null)
            {
                Task.Factory.StartNew(() =>
                    {
                        lock (TranslationQueue.SyncRoot)
                        {
                            TranslationQueue.Enqueue(ho);
                        }
                    });
            }
        }

        private void SearchHelper_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    {
                        ShowORHideWindow(false);
                        e.Handled = true;
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        private void SuggestListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestListView.SelectedItem != null)
            {
                EnqueueToTranslation(new HelperObject(SuggestListView.SelectedItem as string));
            }
        }

        private void SuggestListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        if (SuggestListView.SelectedItem != null)
                        {
                            Search(SuggestListView.SelectedItem as string);
                            e.Handled = true;
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
    }

    public class HelperObject
    {
        static long TOTALINDEX = 0;

        public HelperObject(string keyWord)
        {
            KeyWord = keyWord;
            Index = TOTALINDEX++;
        }

        public  long Index { get; private set; }
        public string KeyWord { get; private set; }
    }

    public enum SearchEngine
    {
        BaiDu,
        Google
    }

    public class HotKey
    {
        #region Member

        int KeyId;         //热键编号
        IntPtr Handle;     //窗体句柄
        Window Window;     //热键所在窗体
        uint ControlKey;   //热键控制键
        uint Key;          //热键主键

        public delegate void OnHotKeyEventHandler();     //热键事件委托
        public event OnHotKeyEventHandler OnHotKey = null;   //热键事件

        static Hashtable KeyPair = new Hashtable();         //热键哈希表
        private const int WM_HOTKEY = 0x0312;       // 热键消息编号

        public enum KeyFlags    //控制键编码        
        {
            MOD_ALT = 0x1,
            MOD_CONTROL = 0x2,
            MOD_SHIFT = 0x4,
            MOD_WIN = 0x8
        }

        #endregion

        ///<summary>
        /// 构造函数
        ///</summary>
        ///<param name="win">注册窗体</param>
        ///<param name="control">控制键</param>
        ///<param name="key">主键</param>
        public HotKey(Window win, HotKey.KeyFlags control, System.Windows.Forms.Keys key)
        {
            Handle = new WindowInteropHelper(win).Handle;
            Window = win;
            ControlKey = (uint)control;
            Key = (uint)key;
            KeyId = (int)ControlKey + (int)Key * 10;

            if (HotKey.KeyPair.ContainsKey(KeyId))
            {
                throw new Exception("热键已经被注册!");
            }

            //注册热键
            if (false == HotKey.RegisterHotKey(Handle, KeyId, ControlKey, Key))
            {
                throw new Exception("热键注册失败!");
            }

            //消息挂钩只能连接一次!!
            if (HotKey.KeyPair.Count == 0)
            {
                if (false == InstallHotKeyHook(this))
                {
                    throw new Exception("消息挂钩连接失败!");
                }
            }

            //添加这个热键索引
            HotKey.KeyPair.Add(KeyId, this);
        }

        //析构函数,解除热键
        ~HotKey()
        {
            HotKey.UnregisterHotKey(Handle, KeyId);
        }

        #region core

        [System.Runtime.InteropServices.DllImport("user32")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint controlKey, uint virtualKey);

        [System.Runtime.InteropServices.DllImport("user32")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        //安装热键处理挂钩
        static private bool InstallHotKeyHook(HotKey hk)
        {
            if (hk.Window == null || hk.Handle == IntPtr.Zero)
            {
                return false;
            }

            //获得消息源
            System.Windows.Interop.HwndSource source = System.Windows.Interop.HwndSource.FromHwnd(hk.Handle);
            if (source == null)
            {
                return false;
            }

            //挂接事件            
            source.AddHook(HotKey.HotKeyHook);
            return true;
        }

        //热键处理过程
        static private IntPtr HotKeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                HotKey hk = (HotKey)HotKey.KeyPair[(int)wParam];
                if (hk.OnHotKey != null)
                {
                    hk.OnHotKey();
                }
            }
            return IntPtr.Zero;
        }

        #endregion
    }
}


