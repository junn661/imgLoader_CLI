﻿using imgLoader_WPF.Windows;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace imgLoader_WPF
{
    //컬렉션은 IndexItem을 담고있음                        
    //매 /interval/ 밀리초마다 인덱싱

    internal class IndexingService
    {
        private const int Interval = 3000;

        private readonly Thread _service;
        private bool _stop;

        public ObservableCollection<IndexItem> Index;

        private readonly ImgLoader _sender;

        public IndexingService(ObservableCollection<IndexItem> index, ImgLoader sender)
        {
            Index = index;
            _sender = sender;

            _service = new Thread(() =>
            {
                while (!_stop)
                {
                    Debug.WriteLine("IdxSvc");

                    Thread.Sleep(Interval);

                    if (Properties.Settings.Default.NoIndex) continue;

                    var sb = new StringBuilder();
                    DoIndex(sb);
                }
            });
            _service.Name = "IdxSvc";

            DoIndex(new StringBuilder());
        }

        internal void DoIndex(StringBuilder sb)
        {
            if (!Directory.Exists(Core.Route)) return;

            var infoFiles = Directory.GetFiles(Core.Route, $"*.{Core.InfoExt}", SearchOption.AllDirectories);
            foreach (var item in new ObservableCollection<IndexItem>(Index))
            {
                if (item.IsDownloading)
                    continue;
                if (infoFiles.Contains(item.Route)) continue;

                Debug.WriteLine($"remove {item.Number}");
                _sender.Dispatcher.Invoke(() => Index.Remove(item));
            }

            foreach (var infoRoute in infoFiles)
            {
                if (!File.Exists(infoRoute)) continue;

                using var sr = new StreamReader(Core.DelayStream(infoRoute, FileMode.Open, FileAccess.Read), Encoding.UTF8);
                var infos = sr.ReadToEnd().Replace("\r\n", "\n");
                sr.Close();
                if (string.IsNullOrWhiteSpace(infos)) continue;

                var info = infos.Split('\n');
                if (info.Length != 8)
                {
                    Debug.WriteLine($"Insufficient Info: {infoRoute.Split('\\')[^1].Split('.')[0]}");
                    continue;
                }

                if (info.Length > 7 && info[7] == "0") //목록에서만 제거 처리
                {
                    var temp = Index.Where(t => t.Number == infoRoute.Split('\\')[^1].Split('.')[0]).ToArray();

                    if (temp.Length > 0) foreach (var item in temp) _sender.Dispatcher.Invoke(() => Index.Remove(item));

                    continue;
                }

                if (Index.Any(idx => idx.Route == infoRoute)) continue;

                if (info[2].Contains('|'))
                {
                    //if (info[1].Contains("첫사랑"))
                    //    ;
                    foreach (var s in info[2].Split('|')[0].Split(';'))
                    {
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        sb.Append(s).Append(", ");
                    }

                    if(sb.Length != 0) sb.Remove(sb.Length - 2, 2);

                    if (info[2].Split('|')[1].Contains(';'))
                    {
                        sb.Append(" (");
                        foreach (var s in info[2].Split('|')[1].Split(';'))
                        {
                            if (string.IsNullOrWhiteSpace(s)) continue;
                            sb.Append(s).Append(", ");
                        }
                        sb.Remove(sb.Length - 2, 2);
                        sb.Append(')');
                    }
                }
                else
                {
                    sb.Append(info[2]);
                }

                _sender.Dispatcher.Invoke(() =>
                    Index.Add(
                    new IndexItem
                    {
                        Title = info[1],
                        Author = sb.ToString(),
                        SiteName = info[0],
                        ImgCount = info[3],
                        Number = Core.EHNumForInternal(infoRoute.Split('\\')[^1].Split('.')[0]),
                        Route = infoRoute,
                        Tags = info[4].Split("tags:")[1].Split('\n')[0].Split(';')
                    }
                    ));
                sb.Clear();
            }
        }

        internal void Start()
        {
            _stop = false;
            _service.Start();
        }

        internal void Stop()
        {
            _stop = true;
        }
    }

    internal class IndexItem
    {
        public delegate void NoParam();
        public delegate void IntOne(int value);
        public delegate void DblOne(double value);

        public Processor Proc;

        public string Title { get; set; }
        public string Author { get; set; }
        public string SiteName { get; set; }
        public string ImgCount { get; set; }
        public string Number { get; set; }
        public string[] Tags { get; set; }
        public int Vote { get; set; }
        public bool IsRead { get; set; }
        public string Route { get; set; }

        //public bool Selected = false;
        public bool Show = true;
        public bool IsDownloading;

        public NoParam RefreshInfo;

        public NoParam ShownChang;

        public NoParam ProgPanelHide;
        public NoParam ProgPanelShow;
        public NoParam TagPanelHide;
        public NoParam TagPanelShow;

        public IntOne ProgBarMax;
        public NoParam ProgBarVal;

        public DblOne SizeChange;
    }

}
