﻿using imgLoader_WPF.LoaderListCtrl;

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace imgLoader_WPF.Windows
{
    //todo: 서로 다른 작품 자동 연결
    //todo: 자체 탐색기 만들기
    //todo: 완전히 같은 이미지 탐색
    //todo: 배경색깔 강제 통일 기능 (https://hiyobi.me/reader/1847608)
    //todo: 페이지네이션
    //todo: 조회수
    //todo: 여러 폴더를 탭으로 동시에 관리

    public partial class ImgLoader
    {
        internal InfoSavingService _infSvc;
        private IndexingService _idxSvc;
        private PaginationService _pgSvc;

        private Settings _winSetting;

        private readonly ObservableCollection<IndexItem> _index = new();   //단순 인덱싱 결과
        internal ObservableCollection<IndexItem> _list = new();            //표시되어야 할 총 항목
        private ObservableCollection<IndexItem> _showItems = new();        //실제 표시되는 항목

        private IndexItem _clickedItem;
        private readonly StringBuilder _sb = new();

        public ImgLoader()
        {
            InitializeComponent();
        }

        private void HideAddBorder()
        {
            AddBorder.Visibility = Visibility.Hidden;
            Focus();

            TxtUrl.Text = "";
            LabelBlock.Visibility = Visibility.Visible;
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            ;
            //_list = _index;
        }

        private void ImgLoader_WPF_Loaded(object sender, RoutedEventArgs e)
        {
            Menu.Focus(); //메뉴 미리 로드

            if (Core.Route.Length == 0 && File.Exists($"{Path.GetTempPath()}{Core.RouteFile}.txt") && Directory.Exists(File.ReadAllText($"{Path.GetTempPath()}{Core.RouteFile}.txt")))
            {
                Core.Route = File.ReadAllText($"{Path.GetTempPath()}{Core.RouteFile}.txt");
            }
            else
            {
                _winSetting.Show();
            }

#if DEBUG
            Core.Route = "D:\\문서\\사진\\Saved Pictures\\고니\\i\\새 폴더 (5)";
#endif

            Title = Core.Route;

            _winSetting = new Settings(Scroll, _showItems, _index);

            ItemCtrl.ItemsSource = _showItems;

            new Thread(() =>
            {
                while (true)
                {
                    Debug.WriteLine(_list.Count);
                    Thread.Sleep(200);
                }
            }).Start();

            _infSvc = new InfoSavingService(this);
            _idxSvc = new IndexingService(_index, this);
            _pgSvc = new PaginationService(this, Scroll, _showItems, _list, _index);

            _infSvc.Start();
            _idxSvc.Start();
            _pgSvc.Paginate();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //var temp = _index.Keys.ToArray()[new Random().Next(0, _index.Count)];
            //Process.Start("explorer.exe", temp.Substring(0, temp.IndexOf(temp.Split('\\').Last(), StringComparison.Ordinal)));
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //for (int j = 0; j < 700; j++)
            //{
            //var item = new LoaderItem($"Test_test_{i}", $"imgL_{i}", i++.ToString(), "Hiyobi", $"C:\\test{j}", "000000", 0);
            //LList.Children.Add();
            //}
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            _winSetting.ShowDialog();
        }

        private void TxtUrl_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (TxtUrl.Text.Length == 0) return;

            var url = TxtUrl.Text;
            var lItem = new IndexItem() { Author = "준비 중...", ImgCount = "\n" }; //imgcount = "\n" => hides "장"

            HideAddBorder();

            var thrTemp = new Thread(() =>
            {
                ItemCtrl.Dispatcher.Invoke(() => _index.Add(lItem));

                lItem.Proc = new Processor(url, lItem, this);

                if (!lItem.Proc.IsValidated)
                {
                    ItemCtrl.Dispatcher.Invoke(() => _index.Remove(lItem));
                    return;
                }

                if (lItem.Proc.CheckDupl())
                {
                    MessageBox.Show("Already Exists.");
                    ItemCtrl.Dispatcher.Invoke(() => _index.Remove(lItem));
                    return;
                }

                lItem.RefreshInfo();

                lItem.Proc.Load();
            });

            thrTemp.Name = "AddItem";
            thrTemp.SetApartmentState(ApartmentState.STA);
            thrTemp.Start();
        }

        private void ImgLoader_WPF_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _infSvc.Stop();
            _idxSvc.Stop();

            _winSetting.Close();
            _winSetting.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        }

        private void LItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender == null) return;

            _clickedItem = (IndexItem)((LoaderItem)sender).DataContext;

            for (var j = 1; j < ItemCtrl.ContextMenu.Items.Count; j++)
            {
                var item = ItemCtrl.ContextMenu.Items[j];
                if (item.GetType() == typeof(Separator)) continue;

                if (!_clickedItem.IsDownloading && (j == 5 || j == 6 || j == 7))
                {
                    ((MenuItem)item).IsEnabled = false;
                    continue;
                }

                if (j == 7)
                {
                    if (_clickedItem.Proc == null) continue;
                    if (_clickedItem.Proc.Pause)
                    {
                        ((MenuItem)item).IsEnabled = true;
                        ((MenuItem)ItemCtrl.ContextMenu.Items[4]).IsEnabled = false;
                        continue;
                    }

                    ((MenuItem)item).IsEnabled = false;
                    continue;
                }

                ((MenuItem)item).IsEnabled = true;
            }

            e.Handled = true;
        }

        private void LList_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            for (var j = 1; j < ItemCtrl.ContextMenu.Items.Count; j++)
            {
                var item = ItemCtrl.ContextMenu.Items[j];
                if (item.GetType() == typeof(Separator)) continue;
                ((MenuItem)item).IsEnabled = false;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            _clickedItem.Show = false;
            _clickedItem.IsDownloading = false;

            Directory.Delete(Core.GetDirectoryFromFile(_clickedItem.Route), true);

            _idxSvc.DoIndex(_sb);
        }

        private void RemoveOnlyList_Click(object sender, RoutedEventArgs e)
        {
            _clickedItem.Show = false;

            InfoSavingService.Save(this);
            _index.Remove(_clickedItem);

            _idxSvc.DoIndex(_sb);
        }

        private void OpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            Core.OpenDir(Core.GetDirectoryFromFile(_clickedItem.Route));
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var img = new BitmapImage();
            var temp = Directory.GetFiles(Core.GetDirectoryFromFile(_clickedItem.Route), "*.*").Where(f => !f.Contains(".ilif")).ToArray();

            img.BeginInit();
            img.UriSource = new Uri(temp[0]);
            img.EndInit();

            var canvas = new CanvasWindow.CanvasWindow { Image = img, Title = img.UriSource.LocalPath.Split('\\')[^1], FileList = temp };
            canvas.Show();

            _clickedItem.IsRead = true;
            _clickedItem.ShownChang.Invoke();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _clickedItem.Proc.Stop = true;

            _clickedItem.Show = false;
            _clickedItem.IsDownloading = false;

            Directory.Delete(Core.GetDirectoryFromFile(_clickedItem.Route), true);

            _idxSvc.DoIndex(_sb);
        }

        private void Resume_Click(object sender, RoutedEventArgs e)
        {
            _clickedItem.Proc.Pause = false;
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            _clickedItem.Proc.Pause = true;
        }

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            AddBorder.Visibility = Visibility.Visible;
            TxtUrl.Focus();
        }

        private void AddBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                HideAddBorder();
            }
        }

        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void TxtUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtUrl.Text.Length == 0)
            {
                LabelBlock.Visibility = Visibility.Visible;
                return;
            }

            LabelBlock.Visibility = Visibility.Collapsed;
        }

        private void CopyAddress_Click(object sender, RoutedEventArgs e)
        {
            switch (_clickedItem.SiteName)
            {
                case "Hiyobi":
                    Clipboard.SetText($"https://hiyobi.me/reader/{_clickedItem.Number}");
                    break;
                case "Hitomi":
                    Clipboard.SetText($"https://hitomi.la/galleries/{_clickedItem.Number}.html");
                    break;
                case "EHentai":
                    Clipboard.SetText($"https://e-hentai.org/g/{_clickedItem.Number}/");
                    break;
                case "NHentai":
                    Clipboard.SetText($"https://nhentai.net/g/{_clickedItem.Number}");
                    break;
            }
        }

        private void TxtUrl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void Scroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!(Math.Abs(e.VerticalOffset - Scroll.ScrollableHeight) < 1) || _index.Count <= _showItems.Count) return;

            var sder = ((ScrollViewer)sender);
            _pgSvc.Paginate();
        }

        private void Scroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (var item in _showItems)
            {
                if (item.SizeChange == null) return;
                item.SizeChange(Scroll.ActualWidth - 10.0);
                Debug.WriteLine("size");
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            _showItems.Clear();
            Core.SearchFromAll(_index, "loli", _list);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            _showItems.Add(new IndexItem() { Title = "test" });
        }
    }
}