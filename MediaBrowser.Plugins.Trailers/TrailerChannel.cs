﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.Trailers
{
    public class TrailerChannel : IChannel
    {
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        public TrailerChannel(IHttpClient httpClient, ILogger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public string DataVersion
        {
            get
            {
                return "5";
            }
        }

        public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var items = await GetChannelItems(cancellationToken).ConfigureAwait(false);

            return new ChannelItemResult
            {
                Items = items.ToList(),
                CacheLength = TimeSpan.FromDays(3)
            };
        }

        private async Task<IEnumerable<ChannelItemInfo>> GetChannelItems(CancellationToken cancellationToken)
        {
            var trailers = await AppleTrailerListingDownloader.GetTrailerList(_httpClient, _logger, cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var maxDays = Plugin.Instance.Configuration.MaxTrailerAge;

            return trailers.Where(i => !maxDays.HasValue || (now - i.PostDate).TotalDays <= maxDays.Value)
                .Select(i => new ChannelItemInfo
            {
                CommunityRating = i.CommunityRating,
                ContentType = ChannelMediaContentType.Trailer,
                Genres = i.Genres,
                ImageUrl = i.HdImageUrl ?? i.ImageUrl,
                IsInfiniteStream = false,
                MediaType = ChannelMediaType.Video,
                Name = i.Name,
                OfficialRating = i.OfficialRating,
                Overview = i.Overview,
                People = i.People,
                Type = ChannelItemType.Media,
                Id = i.TrailerUrl.GetMD5().ToString("N"),
                PremiereDate = i.PremiereDate,
                ProductionYear = i.ProductionYear,
                Studios = i.Studios,
                RunTimeTicks = i.RunTimeTicks,

                MediaSources = new List<ChannelMediaInfo>
                {
                    new ChannelMediaInfo
                    {
                         Path = i.TrailerUrl
                    }
                }
            });
        }

        public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        {
            switch (type)
            {
                case ImageType.Primary:
                case ImageType.Thumb:
                    {
                        var path = GetType().Namespace + ".Images." + type.ToString().ToLower() + ".jpg";

                        return Task.FromResult(new DynamicImageResponse
                        {
                            Format = ImageFormat.Jpg,
                            HasImage = true,
                            Stream = GetType().Assembly.GetManifestResourceStream(path)
                        });
                    }
                default:
                    throw new ArgumentException("Unsupported image type: " + type);
            }
        }

        public IEnumerable<ImageType> GetSupportedChannelImages()
        {
            return new List<ImageType>
            {
                ImageType.Thumb,
                ImageType.Primary
            };
        }

        public string HomePageUrl
        {
            get { return "http://mediabrowser3.com"; }
        }

        public bool IsEnabledFor(User user)
        {
            return true;
        }

        public string Name
        {
            get { return "Trailers"; }
        }

        public Task<IEnumerable<ChannelItemInfo>> Search(ChannelSearchInfo searchInfo, User user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public ChannelFeatures GetChannelFeatures()
        {
            return new ChannelFeatures
            {
                CanSearch = false,

                CanGetAllMedia = true,

                ContentTypes = new List<ChannelMediaContentType>
                 {
                     ChannelMediaContentType.Trailer
                 },

                MediaTypes = new List<ChannelMediaType>
                  {
                       ChannelMediaType.Video
                  }
            };
        }


        public Task<ChannelItemResult> GetAllMedia(InternalAllChannelItemsQuery query, CancellationToken cancellationToken)
        {
            return GetChannelItems(new InternalChannelItemQuery
            {
                Limit = query.Limit,
                StartIndex = query.StartIndex,
                User = query.User

            }, cancellationToken);
        }
    }
}