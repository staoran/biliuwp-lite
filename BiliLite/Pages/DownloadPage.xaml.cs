﻿using BiliLite.Helpers;
using BiliLite.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace BiliLite.Pages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class DownloadPage : BasePage
    {
        DownloadVM downloadVM;
        public DownloadPage()
        {
            downloadVM = DownloadVM.Instance;
            this.InitializeComponent();
            Title = "下载";
        }
        protected  override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.New)
            {
                 downloadVM.RefreshDownloaded();
            }
        }
        private void listDowned_ItemClick(object sender, ItemClickEventArgs e)
        {
            var data = e.ClickedItem as DownloadedItem;
            if (data.Epsidoes == null || data.Epsidoes.Count == 0)
            {
                Utils.ShowMessageToast("没有可以播放的视频");
                return;
            }
            if (data.Epsidoes.Count > 1)
            {
                Pane.DataContext = data;
                splitView.IsPaneOpen = true;
                //弹窗选择播放剧集
                return;
            }
            OpenPlayer(data);
        }

        private void OpenPlayer(DownloadedItem data, int index = 0)
        {
            LocalPlayInfo localPlayInfo = new LocalPlayInfo();
            localPlayInfo.Index = index;
            localPlayInfo.PlayInfos = new List<Controls.PlayInfo>();
            foreach (var item in data.Epsidoes)
            {
                IDictionary<string, string> subtitles = new Dictionary<string, string>();
                foreach (var subtitle in item.SubtitlePath)
                {
                    subtitles.Add(subtitle.Name, subtitle.Url);
                }
                PlayUrlInfo info = new PlayUrlInfo();
                if (item.IsDash)
                {
                    info.mode = VideoPlayMode.Dash;
                    info.dash_video_url = new DashItemModel()
                    {
                        baseUrl = item.Paths.FirstOrDefault(x=>x.Contains("video.m4s")),
                        base_url = item.Paths.FirstOrDefault(x => x.Contains("video.m4s")),
                    };
                    info.dash_audio_url = new DashItemModel()
                    {
                        baseUrl = item.Paths.FirstOrDefault(x => x.Contains("audio.m4s")),
                        base_url = item.Paths.FirstOrDefault(x => x.Contains("audio.m4s")),
                    };
                }
                else if (item.Paths.Count == 1)
                {
                    info.mode = VideoPlayMode.SingleMp4;
                    info.url = item.Paths[0];
                }
                else
                {
                    info.mode = VideoPlayMode.MultiFlv;
                    info.multi_flv_url = new List<FlvDurlModel>();
                    foreach (var item2 in item.Paths.OrderBy(x=>x))
                    {
                        info.multi_flv_url.Add(new FlvDurlModel()
                        {
                            url = item2
                        });
                    }
                }
                localPlayInfo.PlayInfos.Add(new Controls.PlayInfo()
                {
                    avid = item.AVID,
                    cid = item.CID,
                    ep_id = item.EpisodeID,
                    play_mode = Controls.VideoPlayType.Download,
                    season_id = data.IsSeason ? data.ID.ToInt32() : 0,
                    order = item.Index,
                    title = item.Title,
                    season_type = 0,
                    LocalPlayInfo = new Controls.LocalPlayInfo()
                    {
                        DanmakuPath = item.DanmakuPath,
                        Quality = item.QualityName,
                        Subtitles = subtitles,
                        Info = info
                    }
                });
            }

            MessageCenter.NavigateToPage(this, new NavigationInfo()
            {
                icon = Symbol.Play,
                page = typeof(LocalPlayerPage),
                parameters = localPlayInfo,
                title = data.Title
            });
        }

        private void listDownloadedEpisodes_ItemClick(object sender, ItemClickEventArgs e)
        {
            var data = Pane.DataContext as DownloadedItem;
            var item = e.ClickedItem as DownloadedSubItem;
            OpenPlayer(data, data.Epsidoes.IndexOf(item));
        }

        private void btnEpisodesPlay_Click(object sender, RoutedEventArgs e)
        {
            var data = Pane.DataContext as DownloadedItem;
            var item = (sender as AppBarButton).DataContext as DownloadedSubItem;
            OpenPlayer(data, data.Epsidoes.IndexOf(item));
        }

        private async void btnEpisodesDelete_Click(object sender, RoutedEventArgs e)
        {
            var data = Pane.DataContext as DownloadedItem;
            var item = (sender as AppBarButton).DataContext as DownloadedSubItem;
            var result = await Utils.ShowDialog("删除下载", $"确定要删除《{item.Title}》吗?\r\n文件将会被永久删除!");
            if (!result)
            {
                return;
            }
            try
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(item.Path);
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                data.Epsidoes.Remove(item);
            }
            catch (Exception ex)
            {
                Utils.ShowMessageToast("目录删除失败，请检查是否文件是否被占用");
                LogHelper.Log("删除下载视频失败", LogType.FATAL, ex);
            }
        }

        private async void btnEpisodesFolder_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as AppBarButton).DataContext as DownloadedSubItem;
            await Launcher.LaunchFolderPathAsync(item.Path);
        }

        private void btnMenuPlay_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as MenuFlyoutItem).DataContext as DownloadedItem;
            if (data.Epsidoes == null || data.Epsidoes.Count == 0)
            {
                Utils.ShowMessageToast("没有可以播放的视频");
                return;
            }
            OpenPlayer(data, 0);
        }

        private async void btnMenuDetail_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as MenuFlyoutItem).DataContext as DownloadedItem;
            var url = "https://b23.tv/";
            if (data.IsSeason)
            {
                url += "ss" + data.ID;
            }
            else
            {
                url += "av" + data.ID;
            }
            await MessageCenter.HandelUrl(url);
        }

        private async void btnMenuFolder_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as MenuFlyoutItem).DataContext as DownloadedItem;
            await Launcher.LaunchFolderPathAsync(data.Path);
        }

        private async void btnMenuDetele_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as MenuFlyoutItem).DataContext as DownloadedItem;
            var result = await Utils.ShowDialog("删除下载", $"确定要删除《{data.Title}》吗?\r\n目录下共有{data.Epsidoes.Count}个视频,将会被永久删除。");
            if (!result)
            {
                return;
            }
            try
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(data.Path);
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                downloadVM.Downloadeds.Remove(data);
            }
            catch (Exception ex)
            {
                Utils.ShowMessageToast("目录删除失败，请检查是否文件是否被占用");
                LogHelper.Log("删除下载视频失败", LogType.FATAL, ex);
            }
           

        }

        private async void btnMerge_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://iliili.cn/index.php/bili-merge.html"));
        }
    }
}
