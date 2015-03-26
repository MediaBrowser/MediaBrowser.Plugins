﻿﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EmbyTV.TunerHelpers; 
namespace EmbyTV
{
    /// <summary>
    /// Class LiveTvService
    /// </summary>
    public class LiveTvService : ILiveTvService
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private int _liveStreams;
        private readonly Dictionary<int, int> _heartBeat = new Dictionary<int, int>();
        private TunerServer tunerServer;
        private Plugin _plugin;
        private GuideData.SchedulesDirect tvGuide;
        private EmbyTV.GeneralHelpers.HttpClientHelper Helper;
        private DateTime configLastModified;


        public LiveTvService(IHttpClient httpClient, IJsonSerializer jsonSerializer, ILogger logger, IXmlSerializer xmlSerializer)
        {
            logger.Info("[EmbyTV] Bringing up Live TV Service");
            Helper = new GeneralHelpers.HttpClientHelper("EmbyTV");
            Helper.httpClient = httpClient;
            Helper.jsonSerializer  = jsonSerializer;
            Helper.logger = logger;
            Helper.xmlSerializer  = xmlSerializer;
             _plugin = Plugin.Instance;
            Name = "EmbyTV";
            tunerServer = new TunerServer(_plugin.Configuration.apiURL);
            tunerServer.onlyLoadFavorites = _plugin.Configuration.loadOnlyFavorites;           
            tvGuide = new GuideData.SchedulesDirect(_plugin.Configuration.username, _plugin.Configuration.hashPassword, _plugin.Configuration.tvLineUp);
            RefreshConfigData();
            checkForUpdates();

           
        }

        private void checkForUpdates()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    Helper.LogInfo("Last Time config modified:" + configLastModified);
                    if (Plugin.Instance.ConfigurationDateLastModified != configLastModified)
                    {
                        RefreshConfigData();
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            });
        }

        /// <summary>
        /// Ensure that we are connected to the HomeRunTV server
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
        {
            Helper.cancellationToken = cancellationToken;
            if (string.IsNullOrEmpty(_plugin.Configuration.apiURL))
            {
                Helper.LogError("Tunner hostname/ip missing.");
                throw new InvalidOperationException("EmbyTV Tunner hostname/ip missing.");
            } 
            await tunerServer.GetDeviceInfo(Helper);
            if (tunerServer.model == "")
            {
                Helper.LogError("No tuner found at address .");
                throw new ApplicationException("[EmbyTV] No tuner found at address .");
            }
            else
            {
                Name = "EmbyTV";
            }
 
        }

          /// <summary>
        /// Gets the channels async.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{IEnumerable{ChannelInfo}}.</returns>
        public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken)
        {

            Helper.cancellationToken = cancellationToken;
            Helper.LogInfo("Start GetChannels Async, retrieve all channels for " + tunerServer.getWebUrl());
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (!String.IsNullOrWhiteSpace(_plugin.Configuration.tvLineUp))
            {
                await tvGuide.getToken(Helper);                
                var _httpOptions = new HttpRequestOptions { CancellationToken = cancellationToken };
                var response = await tunerServer.GetChannels(Helper);
                return await tvGuide.getChannelInfo(Helper, response);
            }
            else { return new List<ChannelInfo>();}
        
            
        }



        public Task CancelSeriesTimerAsync(string timerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task CancelTimerAsync(string timerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task CloseLiveStream(string id, CancellationToken cancellationToken)
        {
            Helper.LogInfo("Closing " + id);
            return Task.FromResult(0);
        }

        public Task CreateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task CreateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public event EventHandler DataSourceChanged;

        public Task DeleteRecordingAsync(string recordingId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ImageStream> GetChannelImageAsync(string channelId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
        public void RefreshConfigData() {
            tunerServer = new TunerServer(Plugin.Instance.Configuration.apiURL);
            tunerServer.onlyLoadFavorites = Plugin.Instance.Configuration.loadOnlyFavorites;
            tvGuide = new GuideData.SchedulesDirect(Plugin.Instance.Configuration.username, Plugin.Instance.Configuration.hashPassword, Plugin.Instance.Configuration.tvLineUp);
            var task = Task<string>.Run(async () =>
            {
                Plugin.Instance.Configuration.avaliableLineups = await tvGuide.getLineups(Helper);
                var dict = await tvGuide.getHeadends(Plugin.Instance.Configuration.zipCode, Helper);
                var names = "";
                var values = "";
                foreach (KeyValuePair<string, string> entry in dict)
                {
                    names = names + "," + entry.Key;
                    values = values + "," + entry.Value;
                }
                if (!String.IsNullOrWhiteSpace(names)) { names = names.Substring(1); values = values.Substring(1); }
                Plugin.Instance.Configuration.headendName = names;
                Plugin.Instance.Configuration.headendValue = values;
                Plugin.Instance.SaveConfiguration();
                configLastModified = Plugin.Instance.ConfigurationDateLastModified;
            });
            task.Wait();  
        }
        public Task<ChannelMediaInfo> GetChannelStream(string channelId, CancellationToken cancellationToken)
        {
                _liveStreams++;
                string streamUrl = tunerServer.getChannelStreamInfo(channelId);
               Helper.LogInfo("Streaming Channel"+ channelId + "from: "+ streamUrl);
             //  RefreshConfigData();
                return Task.FromResult(new ChannelMediaInfo
                {
                    Id = _liveStreams.ToString(CultureInfo.InvariantCulture),
                    Path = streamUrl,
                    Protocol = MediaProtocol.Http
                });        
            
            throw new ResourceNotFoundException(string.Format("Could not stream channel {0}", channelId)); 
        }

        public Task<SeriesTimerInfo> GetNewTimerDefaultsAsync(CancellationToken cancellationToken, ProgramInfo program = null)
        {
            throw new NotImplementedException();
        }

        public Task<ImageStream> GetProgramImageAsync(string programId, string channelId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
        {
            return await tvGuide.getTvGuideForChannel(Helper,channelId,startDateUtc,endDateUtc);
        }

        public Task<ImageStream> GetRecordingImageAsync(string recordingId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ChannelMediaInfo> GetRecordingStream(string recordingId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<RecordingInfo>> GetRecordingsAsync(CancellationToken cancellationToken)
        {
            IEnumerable < RecordingInfo > result = new List<RecordingInfo>();
            return Task.FromResult(result);
        }

        public Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimersAsync(CancellationToken cancellationToken)
        {
            IEnumerable<SeriesTimerInfo> result = new List<SeriesTimerInfo>();
            return Task.FromResult(result);
        }

        public async Task<LiveTvServiceStatusInfo> GetStatusInfoAsync(CancellationToken cancellationToken)
        {
            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);          
            //Version Check
            bool upgradeAvailable;
            string serverVersion;
            upgradeAvailable = false;
            serverVersion = tunerServer.firmware;
            //Tuner information
            var _httpOptions = new HttpRequestOptions { CancellationToken = cancellationToken };
            List<LiveTvTunerInfo> tvTunerInfos = await tunerServer.GetTunersInfo(Helper);
            return new LiveTvServiceStatusInfo
            {
                HasUpdateAvailable = upgradeAvailable,
                Version = serverVersion,
                Tuners = tvTunerInfos
            };
        }

        public Task<IEnumerable<TimerInfo>> GetTimersAsync(CancellationToken cancellationToken)
        {
            IEnumerable<TimerInfo> result  = new List<TimerInfo>();
            return Task.FromResult(result);
        }

        public string HomePageUrl
        {
            get { return tunerServer.getWebUrl(); }
        }

        public string Name
        {
            get;
            set;
        }

        public Task RecordLiveStream(string id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public event EventHandler<RecordingStatusChangedEventArgs> RecordingStatusChanged;

        public Task ResetTuner(string id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task UpdateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task UpdateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }


      
        public Task<List<MediaBrowser.Model.Dto.MediaSourceInfo>> GetChannelStreamMediaSources(string channelId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<MediaBrowser.Model.Dto.MediaSourceInfo> GetRecordingStream(string recordingId, string streamId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<List<MediaBrowser.Model.Dto.MediaSourceInfo>> GetRecordingStreamMediaSources(string recordingId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }




        public Task<MediaBrowser.Model.Dto.MediaSourceInfo> GetChannelStream(string channelId, string streamId, CancellationToken cancellationToken)
        {
           // RefreshConfigData();
            _liveStreams++;
            string streamUrl = tunerServer.getChannelStreamInfo(channelId);
            Helper.LogInfo("Streaming Channel" + channelId + "from: " + streamUrl);
            return Task.FromResult(new MediaSourceInfo
            {
                Id = _liveStreams.ToString(CultureInfo.InvariantCulture),
                Path = streamUrl,
                Protocol = MediaProtocol.Http,
                MediaStreams = new List<MediaStream>
                        {
                            new MediaStream
                            {
                                Type = MediaStreamType.Video,
                                // Set the index to -1 because we don't know the exact index of the video stream within the container
                                Index = -1
                            },
                            new MediaStream
                            {
                                Type = MediaStreamType.Audio,
                                // Set the index to -1 because we don't know the exact index of the audio stream within the container
                                Index = -1
                            }
                        }
            });        
        }
    }
}