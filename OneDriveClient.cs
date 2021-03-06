﻿using CoPilot.Interfaces;
using CoPilot.Interfaces.EventArgs;
using CoPilot.Interfaces.Types;
using CoPilot.OneDrive.Items;
using Microsoft.Live;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoPilot.OneDrive
{
    public class OneDriveClient : NetClient
    {
        #region CONST

        private const string CLIEND_ID = "0000000044118C4B";
        private static string[] SCOPES = new string[] { "wl.signin", "wl.basic", "wl.offline_access", "wl.skydrive", "wl.skydrive_update" };

        private const string ROOT_FOLDER_NAME = "Co-Pilot";
        private const string VIDEO_FOLDER_NAME = "Videos";
        private const string PHOTO_FOLDER_NAME = "Pictures";
        private const string DATA_FOLDER_NAME = "Data";

        #endregion

        #region PRIVATE

        private Boolean IsConnected = false;
        private LiveAuthClient loginClient;
        private LiveConnectClient liveClient;
        private List<BackgroundTransferPreferences> usedPreferences = new List<BackgroundTransferPreferences>();

        #endregion

        #region PROPERTY

        private OneDriveItem RootFolder { get; set;}
        private OneDriveItem VideoFolder { get; set; }
        private OneDriveItem PhotoFolder { get; set; }
        private OneDriveItem DataFolder { get; set; }

        #endregion

        /// <summary>
        /// Client
        /// </summary>
        public OneDriveClient()
        {
            //client
            loginClient = new LiveAuthClient(CLIEND_ID);
        }

        #region PRIVATE

        /// <summary>
        /// State change
        /// </summary>
        private void stateChange()
        {
            StateEventArgs args = new StateEventArgs();
            args.State = this.IsConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;

            if (State != null)
            {
                State.Invoke(this, args);
            }
        }

        /// <summary>
        /// Error occured
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="errorType"></param>
        private void errorOccured(Exception exception, ErrorType errorType)
        {
            ErrorEventArgs args = new ErrorEventArgs();
            args.Exception = exception;
            args.Type = errorType;

            if (Error != null)
            {
                Error.Invoke(this, args);
            }
        }

        /// <summary>
        /// Prepare storage
        /// </summary>
        /// <returns></returns>
        private async Task prepareStorage()
        {
            if (RootFolder != null && VideoFolder != null && PhotoFolder != null && DataFolder != null)
            {
                return;
            }

            OneDriveItem loaded;
            //root folder
            try
            {
                loaded = await this.folderLoad("/me/skydrive");
            }
            catch
            {
                loaded = null;
            }

            //check if loaded
            if (loaded != null)
            {
                try
                {
                    RootFolder = await this.ensureExists(RootFolder, loaded, ROOT_FOLDER_NAME);
                    if (RootFolder != null)
                    {
                        VideoFolder = await this.ensureExists(VideoFolder, RootFolder, VIDEO_FOLDER_NAME);
                        PhotoFolder = await this.ensureExists(PhotoFolder, RootFolder, PHOTO_FOLDER_NAME);
                        DataFolder = await this.ensureExists(DataFolder, RootFolder, DATA_FOLDER_NAME);
                        return;
                    }
                }
                catch
                {
                    errorOccured(new Exception("Can not create folders in storage."), ErrorType.CreateFolderFail);
                }
            }
            errorOccured(new Exception("Can not create folders in storage."), ErrorType.CreateFolderFail);
        }

        /// <summary>
        /// Folder load
        /// </summary>
        /// <param name="where"></param>
        /// <returns></returns>
        private async Task<OneDriveItem> folderLoad(string where)
        {
            LiveOperationResult operationResult;
            try
            {
                operationResult = await this.liveClient.GetAsync(where);
            }
            catch
            {
                operationResult = null;
            }
            //check if loaded
            if (operationResult != null)
            {
                dynamic result = operationResult.Result;
                if (result != null)
                {
                    return new OneDriveItem(result);
                }
            }
            return null;
        }

        /// <summary>
        /// Ensure exists
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="where"></param>
        /// <param name="what"></param>
        /// <returns></returns>
        private async Task<OneDriveItem> ensureExists(OneDriveItem folder, OneDriveItem where, string what)
        {
            //root folder
            if (folder == null)
            {
                var loaded = await this.folderExists(where.FilesPath, what);
                if (loaded == null)
                {
                    return await this.folderCreate(where, what);
                }
                else
                {
                    return loaded;
                }
            }
            else
            {
                return folder;
            }
        }

        /// <summary>
        /// Folder exists
        /// </summary>
        /// <param name="where"></param>
        /// <param name="what"></param>
        /// <returns></returns>
        private async Task<OneDriveItem> folderExists(string where, string what)
        {
            LiveOperationResult operationResult;
            try
            {
                operationResult = await this.liveClient.GetAsync(where);
            }
            catch
            {
                operationResult = null;
            }

            //load data
            if (operationResult != null)
            {
                dynamic result = operationResult.Result;
                if (result.data != null)
                {
                    foreach (dynamic item in result.data)
                    {
                        var file = new OneDriveItem(item);
                        if (file.IsFolder && file.Name == what)
                        {
                            return file;
                        }
                    }
                }
                else
                {
                    this.errorOccured(new Exception("Server did not return a valid response."), ErrorType.InvalidResponse);
                }
            }
            else
            {
                this.errorOccured(new Exception("Server did not return a valid response."), ErrorType.InvalidResponse);
            }
            return null;
        }

        /// <summary>
        /// File exists
        /// </summary>
        /// <param name="where"></param>
        /// <param name="what"></param>
        /// <returns></returns>
        private async Task<OneDriveItem> fileExists(string where, string what)
        {
            LiveOperationResult operationResult = await this.liveClient.GetAsync(where);
            dynamic result = operationResult.Result;
            if (result.data != null)
            {
                foreach (dynamic item in result.data)
                {
                    var file = new OneDriveItem(item);
                    if (!file.IsFolder && file.Name == what)
                    {
                        return file;
                    }
                }
            }
            else
            {
                this.errorOccured(new Exception("Server did not return a valid response."), ErrorType.InvalidResponse);
            }
            return null;
        }

        /// <summary>
        /// Create folder
        /// </summary>
        /// <param name="where"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private async Task<OneDriveItem> folderCreate(OneDriveItem where, string name)
        {
            try
            {
                //data
                var folderData = new Dictionary<string, object>();
                folderData.Add("name", name);

                LiveOperationResult operationResult = await liveClient.PostAsync(where.Id, folderData);
                dynamic result = operationResult.Result;
                if (result != null)
                {
                    return new OneDriveItem(result);
                }
                else
                {
                    this.errorOccured(new Exception("Server did not return a valid response."), ErrorType.InvalidResponse);
                }
            }
            catch (LiveConnectException exception)
            {
                this.errorOccured(exception, ErrorType.InvalidResponse);
            }
            return null;
        }

        /// <summary>
        /// Get folder by type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string getFolderByType(FileType type)
        {
            switch (type)
            {
                case FileType.Data:
                    return DataFolder.Id;
                case FileType.Photo:
                    return PhotoFolder.Id;
                case FileType.Thumbnail:
                case FileType.Video:
                    return VideoFolder.Id;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Get uplaod on background
        /// </summary>
        private LivePendingUpload GetUploadOnBackground(Uri url)
        {
            var uploads = this.liveClient.GetPendingBackgroundUploads();
            foreach (var upload in uploads)
            {
                var fields = upload.GetType().GetRuntimeFields().ToArray();
                foreach (var field in fields)
                {
                    if (field.Name == "request")
                    {
                        var request = field.GetValue(upload);
                        var requestUrl = request.GetType().GetRuntimeProperty("UploadLocation").GetValue(request) as Uri;
                        if (requestUrl != null && requestUrl.OriginalString.Replace("\\", "/") == url.OriginalString)
                        {
                            return upload;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get download on background
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private LivePendingDownload GetDownloadOnBackground(Uri url)
        {
            var downloads = this.liveClient.GetPendingBackgroundDownloads();
            foreach (var download in downloads)
            {
                var fields = download.GetType().GetRuntimeFields().ToArray();
                foreach (var field in fields)
                {
                    if (field.Name == "request")
                    {
                        var request = field.GetValue(download);
                        var requestUrl = request.GetType().GetRuntimeProperty("DownloadLocation").GetValue(request) as Uri;
                        if (requestUrl != null && requestUrl.OriginalString.Replace("\\", "/") == url.OriginalString)
                        {
                            return download;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// False progress state
        /// </summary>
        /// <param name="bar"></param>
        private void falseProgressState(Progress bar)
        {
            //bar update
            bar.Selected = false;
            bar.InProgress = false;
            bar.BytesTransferred = 0;
            bar.ProgressPercentage = 100;
            bar.TotalBytes = 0;
        }

        /// <summary>
        /// Update preferences
        /// </summary>
        /// <param name="bar"></param>
        private void updatePreferences(Interfaces.Progress bar)
        {
            var client = this.liveClient;
            client.BackgroundTransferPreferences = this.getBackgroundProgressPreferences(bar.Preferences);
        }

        /// <summary>
        /// Get BackgroundTransferPreferencesType
        /// </summary>
        /// <param name="prefs"></param>
        /// <returns></returns>
        private BackgroundTransferPreferences getBackgroundTransferPreferencesType(ProgressPreferences prefs)
        {
            //change type
            switch (prefs)
            {
                case ProgressPreferences.AllowOnCelluralAndBatery:
                    return BackgroundTransferPreferences.AllowCellularAndBattery;
                case ProgressPreferences.AllowOnWifiAndBatery:
                    return BackgroundTransferPreferences.AllowBattery;
                default:
                    return BackgroundTransferPreferences.None;
            }
        }

        /// <summary>
        /// GetBackgroundProgressPreferences
        /// </summary>
        /// <param name="prefs"></param>
        /// <returns></returns>
        private BackgroundTransferPreferences getBackgroundProgressPreferences(ProgressPreferences prefs)
        {
            var backgroundPrefs = this.liveClient.BackgroundTransferPreferences;
            var used = this.usedPreferences;
            var liveRefs = this.getBackgroundTransferPreferencesType(prefs);

            //push
            used.Add(liveRefs);

            //contains
            var cointansNone = used.Contains(BackgroundTransferPreferences.None);
            var cointansAllowBattery = used.Contains(BackgroundTransferPreferences.AllowBattery);

            //returns
            if (cointansNone)
            {
                return BackgroundTransferPreferences.None;
            }
            if (cointansAllowBattery)
            {
                return BackgroundTransferPreferences.AllowBattery;
            }
            return BackgroundTransferPreferences.AllowCellularAndBattery;
        }

        /// <summary>
        /// Remove UsedBackgroundProgressPreferences
        /// </summary>
        /// <param name="prefs"></param>
        private void removeUsedBackgroundProgressPreferences(ProgressPreferences prefs)
        {
            var used = this.usedPreferences;
            var liveRefs = this.getBackgroundTransferPreferencesType(prefs);

            if (used.Contains(liveRefs))
            {
                used.Remove(liveRefs);
            }
        }

        #endregion

        #region INTERFACE

        public event EventHandler<StateEventArgs> State;
        public event EventHandler<ErrorEventArgs> Error;

        /// <summary>
        /// TryLogin
        /// </summary>
        public async void TryLogin()
        {
            try
            {
                LiveLoginResult loginResult = await this.loginClient.InitializeAsync(SCOPES);
                if (loginResult.Status == LiveConnectSessionStatus.Connected)
                {
                    this.liveClient = new LiveConnectClient(loginResult.Session);
                    this.IsConnected = true;
                }
                else
                {
                    this.IsConnected = false;
                }
            }
            catch
            {
                this.IsConnected = false;
            }
            finally
            {
                this.stateChange();
            }
        }

        /// <summary>
        /// Login
        /// </summary>
        public async Task Login()
        {

            try
            {
                LiveLoginResult loginResult = await this.loginClient.LoginAsync(SCOPES);
                if (loginResult.Status == LiveConnectSessionStatus.Connected)
                {
                    this.liveClient = new LiveConnectClient(loginResult.Session);
                    this.IsConnected = true;
                }
                else
                {
                    this.IsConnected = false;
                }
            }
            catch (LiveAuthException)
            {
                this.IsConnected = false;
            } 
            finally
            {
                this.stateChange();
            }
        }

        /// <summary>
        /// Progress
        /// </summary>
        /// <param name="bar"></param>
        /// <returns></returns>
        public async Task<Boolean> Progress(Uri url)
        {
            if (!this.IsConnected)
            {
                return false;
            }

            await Task.Delay(0);

            //pending upload
            LivePendingUpload upload = this.GetUploadOnBackground(url);
            if (upload != null) 
            {
                return true;
            }

            //pending download
            LivePendingDownload download = this.GetDownloadOnBackground(url);
            if (download != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Upload
        /// </summary>
        /// <param name="bar"></param>
        /// <returns></returns>
        public async Task<Response> Upload(Progress bar)
        {
            LiveOperationResult operationResult = null;
            LivePendingUpload pending = null;
            OneDriveItem item;

            if (!this.IsConnected)
            {
                return null;
            }

            try
            {
                //progress
                bar.InProgress = true;
                this.updatePreferences(bar);

                //prepare storage
                await this.prepareStorage();

                //progress
                Progress<LiveOperationProgress> progress = new Progress<LiveOperationProgress>();
                progress.ProgressChanged += (object sender, LiveOperationProgress e) =>
                {
                    bar.BytesTransferred = e.BytesTransferred;
                    bar.ProgressPercentage = e.ProgressPercentage;
                    bar.TotalBytes = e.TotalBytes;
                };

                //get pending
                pending = this.GetUploadOnBackground(bar.Url);

                //try attach pending
                if (pending != null)
                {
                    try
                    {
                        operationResult = await pending.AttachAsync(bar.Cancel.Token, progress);
                    }
                    catch
                    {
                        operationResult = null;
                    }
                }
                else
                {
                    uint error = 0;

                    //background upload
                    try
                    {
                        operationResult = await this.liveClient.BackgroundUploadAsync(this.getFolderByType(bar.Type), bar.Url, OverwriteOption.Overwrite, bar.Cancel.Token, progress);
                    }
                    catch
                    {
                        operationResult = null;
                    }
                }

                //if response is right
                if (operationResult != null)
                {
                    item = new OneDriveItem(operationResult.Result);

                    //remove
                    this.removeUsedBackgroundProgressPreferences(bar.Preferences);

                    //load data about file
                    operationResult = await this.liveClient.GetAsync(item.Id);
                    item = new OneDriveItem(operationResult.Result);

                    //bar update
                    bar.Selected = false;
                    bar.InProgress = false;

                    //return
                    var reponse = new Response();
                    reponse.Id = item.Id;
                    reponse.Url = item.Link;
                    return reponse;
                }
            }
            catch (LiveAuthException e)
            {
                this.errorOccured(e, ErrorType.InvalidResponse);
            }
            this.falseProgressState(bar);
            //remove
            this.removeUsedBackgroundProgressPreferences(bar.Preferences);
            return null;
        }

        /// <summary>
        /// Download
        /// </summary>
        /// <param name="id"></param>
        /// <param name="bar"></param>
        /// <returns></returns>
        public async Task<DownloadStatus> Download(String id, Progress bar)
        {
            LiveOperationResult operationResult = null;
            LivePendingDownload pending = null;

            if (!this.IsConnected)
            {
                return DownloadStatus.Fail;
            }

            
            //progress
            Progress<LiveOperationProgress> progress = new Progress<LiveOperationProgress>();
            progress.ProgressChanged += (object sender, LiveOperationProgress e) =>
            {
                bar.BytesTransferred = e.BytesTransferred;
                bar.ProgressPercentage = e.ProgressPercentage;
                bar.TotalBytes = e.TotalBytes;
            };

            try
            {
                //progress
                bar.InProgress = true;
                this.updatePreferences(bar);

                //prepare storage
                await this.prepareStorage();

                //get pending 
                pending = this.GetDownloadOnBackground(bar.Url);

                //try attach pending
                if (pending != null)
                {
                    try
                    {
                        operationResult = await pending.AttachAsync(bar.Cancel.Token, progress);
                    }
                    catch
                    {
                        operationResult = null;
                    }
                }
                else
                {
                    //download
                    try
                    {
                        operationResult = await this.liveClient.BackgroundDownloadAsync(id + "/content", bar.Url, bar.Cancel.Token, progress);
                    }
                    catch
                    {
                        operationResult = null;
                    }
                }
                //remove
                this.removeUsedBackgroundProgressPreferences(bar.Preferences);
                //bar update
                bar.Selected = false;
                bar.InProgress = false;

                //return
                return operationResult != null && operationResult.Result != null ? DownloadStatus.Complete : DownloadStatus.Fail;
            }
            catch (LiveAuthException e)
            {
                this.errorOccured(e, ErrorType.InvalidResponse);
            }
            this.falseProgressState(bar);
            //remove
            this.removeUsedBackgroundProgressPreferences(bar.Preferences);
            return DownloadStatus.Fail;
        }

        /// <summary>
        /// Backup ID
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<Response> BackupId(String name)
        {
            if (!this.IsConnected)
            {
                return null;
            }

            try
            {
                //prepare storage
                await this.prepareStorage();

                //download
                OneDriveItem item = await this.fileExists(DataFolder.FilesPath, name);

                if (item != null)
                {
                    //return
                    var reponse = new Response();
                    reponse.Id = item.Id;
                    reponse.Url = item.Link;
                    return reponse;
                }
            }
            catch (LiveAuthException e)
            {
                this.errorOccured(e, ErrorType.InvalidResponse);
            }
            catch (Exception e)
            {
                return null;
            }
            return null;
        }

        /// <summary>
        /// Url
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<Response> Url(String id)
        {
            if (!this.IsConnected)
            {
                return null;
            }


            try
            {
                //prepare storage
                await this.prepareStorage();

                //download
                LiveOperationResult operationResult = await this.liveClient.GetAsync(id);
                OneDriveItem item = new OneDriveItem(operationResult.Result);

                //return
                var reponse = new Response();
                reponse.Id = item.Id;
                reponse.Url = item.Source;
                return reponse;
            }
            catch (LiveAuthException e)
            {
                this.errorOccured(e, ErrorType.InvalidResponse);
            }
            catch (Exception e)
            {
                return null;
            }
            return null;
        }

        /// <summary>
        /// Preview ID
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<DownloadStatus> Preview(String id, Progress bar)
        {
            LiveOperationResult operationResult = null;
            LivePendingDownload pending = null;

            if (!this.IsConnected)
            {
                return DownloadStatus.Fail;
            }

                        
            //progress
            Progress<LiveOperationProgress> progress = new Progress<LiveOperationProgress>();
            progress.ProgressChanged += (object sender, LiveOperationProgress e) =>
            {
                bar.BytesTransferred = e.BytesTransferred;
                bar.ProgressPercentage = e.ProgressPercentage;
                bar.TotalBytes = e.TotalBytes;
            };

            try
            {
                //prepare storage
                await this.prepareStorage();
                this.updatePreferences(bar);

                //download
                operationResult = await this.liveClient.GetAsync(id);
                OneDriveItem item = new OneDriveItem(operationResult.Result);

                //get pending 
                pending = this.GetDownloadOnBackground(bar.Url);

                //try attach pending
                if (pending != null)
                {
                    try 
                    {
                        operationResult = await pending.AttachAsync(bar.Cancel.Token, progress);
                    }
                    catch
                    {
                        operationResult = null;
                    }
                }
                else
                {
                    //download
                    try
                    {
                        operationResult = await this.liveClient.BackgroundDownloadAsync(item.Picture, bar.Url, bar.Cancel.Token, progress);
                    }
                    catch
                    {
                        operationResult = null;
                    }
                }
                //remove
                this.removeUsedBackgroundProgressPreferences(bar.Preferences);
                //bar update
                bar.Selected = false;
                bar.InProgress = false;

                //return
                return operationResult.Result != null ? DownloadStatus.Complete : DownloadStatus.Fail;
            }
            catch (LiveAuthException e)
            {
                this.errorOccured(e, ErrorType.InvalidResponse);
            }
            catch (Exception e)
            {
                this.errorOccured(e, ErrorType.InvalidResponse);
            }
            this.falseProgressState(bar);
            //remove
            this.removeUsedBackgroundProgressPreferences(bar.Preferences);
            return DownloadStatus.Fail;
        }

        #endregion
    }
}
