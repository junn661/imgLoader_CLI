﻿using imgLoader_WPF.LoaderListCtrl;
using imgLoader_WPF.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using static imgLoader_WPF.Services.Sorter;

namespace imgLoader_WPF.Windows
{
    //todo: 서로 다른 작품 자동 연결
    //todo: 완전히 같은 이미지 탐색
    //todo: 배경색깔 강제 통일 기능 (https://hiyobi.me/reader/1847608)
    //조회수
    //todo: 여러 폴더를 탭으로 동시에 관리
    //todo: 폴더 두 개를 열고 없는 항목 체크
    //todo: 조건이 있는 랜덤
    //뷰어: 계속 다시 로드하지 말고 배열에 이미지를 담아놓을것
    //todo: 단행본 나누기

    //todo: 여러 작품이 하나로 나오는 것 처리 (예시: Gakuen Rankou (jairou))
    //todo: 작업 표시줄에 프로그래스바 
    //numericupdown 같은것으로 작품별로 순위 매기는 시스템
    //todo: 작가, 태그 등으로 자동으로 폴더로 나눠주는 시스템
    //todo: 항상 위로 상태로 떠 있다가 인터넷 창에서 누르면 자동으로 해당 작품 다운로드 
    //todo: 작가/태그 분포, 주로 보는 작품 등 분석 기능
    //todo: 여러 폴더를 지정해 동시에 관리
    //todo: 아무 값 없는 분류(그냥 빨주노초파남보) 분류 기능
    //todo: 우클릭 시 해당 항목 작가명/기타로 검색
    //todo: 더블클릭으로 열기
    //todo: 드래그로 사용자 정의 순서

    //todo: 자체 탐색기 만들기(메뉴-관리)
    //todo: 정보 복구
    //todo: 정보 직접 수정
    //todo: 작가별 트리식 정렬
    //todo: 특정 이미지 숨기기(삭제x)

    public partial class ImgLoader
    {
        internal InfoSavingService InfSvc;
        internal IndexingService IdxSvc;
        internal PaginationService PgSvc;

        internal Sorter Sorter;
        internal Searcher Searcher;
        internal ConditionIndicator CondInd;

        private Settings _winSetting;

        internal readonly List<IndexItem> Index = new();                             //인덱싱 결과
        internal readonly List<IndexItem> List = new();                              //표시되어야 할 총 항목
        internal readonly ObservableCollection<IndexItem> ShowItems = new();         //실제 표시되는 항목

        private IndexItem _clickedItem;

        public ImgLoader()
        {
            InitializeComponent();
        }

        private void HideBorder(UIElement border, TextBox txtB, TextBlock label)
        {
            border.Visibility = Visibility.Hidden;
            Focus();

            txtB.Text = "";
            label.Visibility = Visibility.Visible;
        }

        private void DeleteItemDir(IndexItem item)
        {
            if (!File.Exists(item.Route)) return;

            item.Show = false;
            item.IsDownloading = false;

            new Thread(() =>
            {
                item.Proc?.DoStop();

                var wait = true;
                do
                {
                    if (item.Proc == null) break;

                    foreach (var isLoading in item.Proc.IsImgLoading)
                    {
                        if (isLoading)
                        {
                            wait = true;
                            Thread.Sleep(50);
                            break;
                        }

                        wait = false;
                    }
                } while (wait);

                Directory.Delete(Core.GetDirectoryFromFile(item.Route), true);

                IdxSvc.DoIndex();

                List.Remove(_clickedItem);
                Dispatcher.Invoke(() => ShowItems.Remove(_clickedItem));
            }).Start();
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            ;
        }
        private void ImgLoader_WPF_Loaded(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
                {
                    while (true)
                    {
                        Debug.WriteLine($"_index:{Index.Count}/_list:{List.Count}/_showitems:{ShowItems.Count}");
                        Thread.Sleep(1000);
                    }
                })
            { IsBackground = true }.Start();

            Properties.Settings.Default.Upgrade();

            Menu.Focus(); //메뉴 미리 로드
            _winSetting = new Settings(this, Scroll, Index);

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
#if !DEBUG
            D_Stop.IsEnabled = false;
            D_Else1.IsEnabled = false;
            D_Stop.Visibility = Visibility.Collapsed;
            D_Else1.Visibility = Visibility.Collapsed;
#endif
            foreach (RadioButton radio in RadioPanel.Children)
            {
                radio.PreviewKeyUp += TxtSrchAll_KeyUp;
            }

            Title = Core.Route;

            _winSetting = new Settings(this, Scroll, Index);

            ItemCtrl.ItemsSource = ShowItems;

            CondInd = new ConditionIndicator(this);
            Sorter = new Sorter(this, List);
            Searcher = new Searcher(this, List);

            PgSvc = new PaginationService(this, Scroll.ActualHeight, ShowItems, List);
            InfSvc = new InfoSavingService();
            IdxSvc = new IndexingService(this);

            InfSvc.Start();
            IdxSvc.Start();
            PgSvc.Paginate();
        }

        private void TxtUrl_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (TxtUrl.Text.Length == 0) return;

            var url = TxtUrl.Text;
            TxtUrl.Text = "";
            LabelBlock_Add.Visibility = Visibility.Visible;

            var lItem = new IndexItem() { Author = "준비 중...", ImgCount = -1, View = -1 };

            var sw = new Stopwatch();
            sw.Start();

            //HideBorder(AddBorder, TxtUrl, LabelBlock_Add);

            var thrTemp = new Thread(() =>
            {
                ItemCtrl.Dispatcher.Invoke(() => ShowItems.Insert(0, lItem));
                Index.Insert(0, lItem);
                List.Insert(0, lItem);

                //InfSvc.Stop();

                lItem.Proc = new Processor(url, lItem);
                lItem.Proc.Pause = !Properties.Settings.Default.Down_Immid;

                if (!lItem.Proc.IsValidated)
                {
                    ItemCtrl.Dispatcher.Invoke(() => ShowItems.Remove(lItem));
                    Index.Remove(lItem);
                    List.Remove(lItem);
                    return;
                }

                if (lItem.Proc.CheckDupl())
                {
                    MessageBox.Show("Already Exists.");
                    ItemCtrl.Dispatcher.Invoke(() => ShowItems.Remove(lItem));
                    Index.Remove(lItem);
                    List.Remove(lItem);
                    return;
                }

                PgSvc.Paginate();

                while (lItem.RefreshInfo == null)
                {
                    Task.Delay(100).Wait();
                    Debug.WriteLine("Main: TxtUrl_KeyUp: Wait");
                }

                lItem.RefreshInfo();
                lItem.Proc.Load();

                //List.Insert(0, lItem);

                //todo: 다운로드 완료 후 정렬될 위치로 삽입
                Debug.WriteLine("Main: TxtUrl_KeyUp: " + sw.Elapsed.Ticks);
                sw.Reset();
            });

            thrTemp.Name = "AddItem";
            thrTemp.SetApartmentState(ApartmentState.STA);
            thrTemp.Start();
        }
        private void TxtUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtUrl.Text.Length == 0)
            {
                LabelBlock_Add.Visibility = Visibility.Visible;
                return;
            }

            LabelBlock_Add.Visibility = Visibility.Collapsed;
        }
        private void TxtUrl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void TxtSrchAll_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSrchAll.Text.Length == 0)
            {
                LabelBlock_Srch.Visibility = Visibility.Visible;
                return;
            }

            LabelBlock_Srch.Visibility = Visibility.Collapsed;
        }

        private void TxtSrchAll_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (TxtSrchAll.Text.Length == 0) return;
            //List.Clear();
            ShowItems.Clear();

            Search(TxtSrchAll.Text, ConditionIndicator.Condition.Search,
                (int)(
                    AllRadio.IsChecked.Value
                        ? Searcher.SearchOption.All
                        : AuthorRadio.IsChecked.Value
                            ? Searcher.SearchOption.Author
                            : TagRadio.IsChecked.Value
                                ? Searcher.SearchOption.Tag
                                : NumRadio.IsChecked.Value
                                    ? Searcher.SearchOption.Number
                                    : Searcher.SearchOption.Title
                ));

            TxtSrchAll.Text = "";
            TxtSrchAll.Focus();
        }

        private void Search(string label, ConditionIndicator.Condition cond, int option)
        {
            //List.Clear();
            ShowItems.Clear();

            CondInd.Add(label, cond, option);
            PgSvc.Paginate();
        }
        private void Scroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            //if (e.VerticalChange == 0 && e.ExtentHeightChange == 0) return;
            if (!(Math.Abs(e.VerticalOffset - Scroll.ScrollableHeight) < 1) || Index.Count <= ShowItems.Count || List.Count == 0) return;

            PgSvc.Paginate();
        }

        private void Scroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //foreach (var item in ShowItems)
            //{
            //    if (item.SizeChange == null) return;
            //    item.SizeChange(Scroll.ActualWidth - 10.0);
            //    //Debug.WriteLine("size");
            //}
        }

        private void ImgLoader_WPF_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _winSetting.Close();
            _winSetting.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);

            //InfSvc.Stop();
            //IdxSvc.Stop();
        }

        private void LItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender == null || ItemCtrl.ContextMenu == null) return;

            var temp = ((LoaderItem)sender).DataContext;

            if (temp == null) return;

            _clickedItem = (IndexItem)temp;

            foreach (var i in ItemCtrl.ContextMenu.Items)
            {
                if (i.GetType() == typeof(Separator)) continue;

                var item = (MenuItem)i;

                switch (item.Name)
                {
                    case "OpenMenu":
                        item.IsEnabled = !_clickedItem.IsDownloading;
                        break;
                    case "CancelMenu":
                        item.IsEnabled = _clickedItem.IsDownloading;
                        break;
                    case "ResumeMenu":
                        if (!_clickedItem.IsDownloading)
                        {
                            item.IsEnabled = false;
                            break;
                        }

                        if (_clickedItem.Proc == null) continue;
                        if (_clickedItem.Proc.Pause)
                        {
                            item.IsEnabled = true;
                        }

                        break;
                    case "PauseMenu":
                        if (!_clickedItem.IsDownloading)
                        {
                            item.IsEnabled = false;
                            break;
                        }

                        if (_clickedItem.Proc == null) continue;
                        if (_clickedItem.Proc.Pause)
                        {
                            item.IsEnabled = false;
                            break;
                        }

                        item.IsEnabled = true;
                        break;
                    case "ManageMenu":
                        item.IsEnabled = false;
                        break;
                    default:
                        item.IsEnabled = true;
                        break;
                }
            }

            e.Handled = true;
        }

        private void LList_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            foreach (var item in ItemCtrl.ContextMenu?.Items)
            {
                if (item.GetType() == typeof(Separator)) continue;

                switch (((MenuItem)item).Name)
                {
                    case "SettingMenu":
                    case "RandomMenu":
                    case "AddMenu":
                    case "SearchMenu":
                        ((MenuItem)item).IsEnabled = true;
                        break;
                    default:
                        ((MenuItem)item).IsEnabled = false;
                        break;
                }
            }
        }

        #region MenuItem
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show($"Are you sure you want to delete {_clickedItem.Number}?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            DeleteItemDir(_clickedItem);
        }
        //private void RemoveOnlyList_Click(object sender, RoutedEventArgs e)
        //{
        //    _clickedItem.Show = false;

        //    InfSvc.Save(_clickedItem);
        //    _index.Remove(_clickedItem);

        //    _idxSvc.DoIndex(_sb);
        //}
        private void OpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            Core.OpenDir(Core.GetDirectoryFromFile(_clickedItem.Route));
        }
        private void Open_Click(object sender, RoutedEventArgs e)
        {
            Core.OpenOnCanvas(Core.GetDirectoryFromFile(_clickedItem.Route));

            _clickedItem.View++;
            _clickedItem.IsRead = true;
            _clickedItem.ShownChang.Invoke();
            InfSvc.Save(_clickedItem);
         }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DeleteItemDir(_clickedItem);
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
        private void CopyAddress_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_clickedItem.Route)) return;

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
        private void Search_Click(object sender, RoutedEventArgs e)
        {
        }
        private void TitleSort_Click(object sender, RoutedEventArgs e)
        {
            CondInd.Add("Title", ConditionIndicator.Condition.Sort, (int)SortOption.Title);
        }
        private void AuthorSort_Click(object sender, RoutedEventArgs e)
        {
            CondInd.Add("Author", ConditionIndicator.Condition.Sort, (int)SortOption.Author);
        }
        private void PageSort_Click(object sender, RoutedEventArgs e)
        {
            CondInd.Add("Page", ConditionIndicator.Condition.Sort, (int)SortOption.Page);
        }
        private void NumberSort_Click(object sender, RoutedEventArgs e)
        {
            CondInd.Add("Number", ConditionIndicator.Condition.Sort, (int)SortOption.Number);
        }
        private void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_clickedItem.Route)) return;

        }
        private void DateSort_Click(object sender, RoutedEventArgs e)
        {
            CondInd.Add("Date", ConditionIndicator.Condition.Sort, (int)SortOption.Date);
        }
        private void Random_Click(object sender, RoutedEventArgs e)
        {
            var rand = new Random().Next(0, Index.Count);

            Debug.WriteLine("Main: Random_Click: " + Index[rand].Title);

            if (ShowItems.Count < rand + 1)
            {
                Index[rand].IsRead = true;
            }
            else
            {
                ShowItems[rand].IsRead = true;
                ShowItems[rand].ShownChang();
            }

            Core.OpenOnCanvas(Core.GetDirectoryFromFile(Index[rand].Route));
        }
        private void VoteSort_Click(object sender, RoutedEventArgs e)
        {
            CondInd.Add("Vote", ConditionIndicator.Condition.Sort, (int)SortOption.Vote);
        }
        private void ViewSort_Click(object sender, RoutedEventArgs e)
        {
            CondInd.Add("View", ConditionIndicator.Condition.Sort, (int)SortOption.View);
        }
        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            _winSetting.ShowDialog();
        }
        #endregion
        private void AddBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                HideBorder(AddBorder, TxtUrl, LabelBlock_Add);
            }
        }

        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            ShowItems.Clear();
            List.Clear();
            foreach (var item in Index)
            {
                List.Add(item);
            }
            PgSvc.Paginate();
        }

        private void SrchBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                HideBorder(SrchBorder, TxtSrchAll, LabelBlock_Srch);
            }
        }

        private void DockPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LabelBlock_Srch_MouseUp(object sender, MouseButtonEventArgs e)
        {
            TxtSrchAll.Focus();
        }

        private void StackPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void AllRadio_Click(object sender, RoutedEventArgs e)
        {
            TxtSrchAll.Focus();
        }

        private void AuthorSrch_Click(object sender, RoutedEventArgs e)
        {
            Search(_clickedItem.Author, ConditionIndicator.Condition.Search, (int)Searcher.SearchOption.Author);
        }

        private void SiteSrch_Click(object sender, RoutedEventArgs e)
        {
            Search(_clickedItem.SiteName, ConditionIndicator.Condition.Search, (int)Searcher.SearchOption.SiteName);
        }

        private void TagSrch_Click(object sender, RoutedEventArgs e)
        {
            //CondInd.Add(_clickedItem.Tags, ConditionIndicator.Condition.Search, (int)Searcher.SearchOption.Tag);
        }

        private void SearchSMenu_Click(object sender, RoutedEventArgs e)
        {
            SrchBorder.Visibility = Visibility.Visible;
            TxtSrchAll.Focus();
        }
    }
}