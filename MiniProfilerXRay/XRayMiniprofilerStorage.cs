using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Amazon.XRay.Recorder.Core.Internal.Emitters;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.Core.Internal.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using StackExchange.Profiling;
using StackExchange.Profiling.Storage;

namespace MiniProfilerXRay
{
    public class XRayMiniprofilerStorage : IAsyncStorage
    {
        private const int RandomNumberHexDigits = 24;
        private const int Version = 1;

        private static readonly string X_FORWARDED_FOR = "X-Forwarded-For";

        private static readonly OrderedDictionary AlreadySent = new OrderedDictionary();
        private readonly ISegmentEmitter _emmiter = new UdpSegmentEmitter();

        private IServiceProvider _provider;

        public XRayMiniprofilerStorage()
        {
        }

        public XRayMiniprofilerStorage(string xRayEndpoint)
        {
            _emmiter.SetDaemonAddress(xRayEndpoint);
        }

        public IEnumerable<Guid> List(int maxResults, DateTime? start = null, DateTime? finish = null,
            ListResultsOrder orderBy = ListResultsOrder.Descending)
        {
            return Enumerable.Empty<Guid>();
        }

        public void Save(MiniProfiler profiler)
        {
            var currentDateTime = profiler.Started;

            var trace = new Segment(profiler.Name, NewId(currentDateTime));

            trace.StartTime = currentDateTime.AddMilliseconds((double) profiler.Root.StartMilliseconds)
                .ToUnixTimeSeconds();

            if (profiler.Root.DurationMilliseconds != null)
                trace.EndTime = trace.StartTime + profiler.Root.DurationMilliseconds.Value / 1000;

            AddHttpInformation(trace);

            foreach (var child in profiler.Root.Children) ProcessNode(child, trace, trace);

            trace.Release();

            trace.IsInProgress = false;

            if (!AlreadySent.Contains(profiler.Id))
            {
                _emmiter.Send(trace);
                AlreadySent.Add(profiler.Id, true);
            }

            if (AlreadySent.Count >= 10) AlreadySent.RemoveAt(0);
            //File.WriteAllText("profile" + trace.TraceId + ".json", JsonConvert.SerializeObject(profiler));
        }

        public MiniProfiler Load(Guid id)
        {
            return null;
        }

        public void SetUnviewed(string user, Guid id)
        {
        }

        public void SetViewed(string user, Guid id)
        {
        }

        public List<Guid> GetUnviewedIds(string user)
        {
            return new List<Guid>();
        }

        public Task<IEnumerable<Guid>> ListAsync(int maxResults, DateTime? start = null, DateTime? finish = null,
            ListResultsOrder orderBy = ListResultsOrder.Descending)
        {
            return Task.FromResult(Enumerable.Empty<Guid>());
        }

        public Task SaveAsync(MiniProfiler profiler)
        {
            return Task.Run(() => Save(profiler));
        }

        public Task<MiniProfiler> LoadAsync(Guid id)
        {
            return Task.FromResult((MiniProfiler) null);
        }

        public Task SetUnviewedAsync(string user, Guid id)
        {
            return Task.CompletedTask;
        }

        public Task SetViewedAsync(string user, Guid id)
        {
            return Task.CompletedTask;
        }

        public Task<List<Guid>> GetUnviewedIdsAsync(string user)
        {
            return Task.FromResult(new List<Guid>());
        }

        public void SetServiceProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        private void ProcessNode(Timing node, Entity parent, Entity root)
        {
            var subSegment = new Subsegment(node.Name);

            subSegment.StartTime = root.StartTime + node.StartMilliseconds / 1000;

            if (node.DurationMilliseconds != null)
                subSegment.EndTime = subSegment.StartTime + node.DurationMilliseconds.Value / 1000;

            parent.AddSubsegment(subSegment);

            if (node.HasChildren)
                foreach (var nodeChild in node.Children)
                    ProcessNode(nodeChild, subSegment, root);

            if (node.HasCustomTimings)
                foreach (var kvp in node.CustomTimings)
                    if (kvp.Key == "sql")
                        foreach (var sqlTiming in kvp.Value)
                        {
                            var sqlSub = new Subsegment(sqlTiming.ExecuteType);

                            sqlSub.StartTime = root.StartTime + sqlTiming.StartMilliseconds / 1000;

                            if (sqlTiming.DurationMilliseconds != null)
                                sqlSub.EndTime = sqlSub.StartTime + sqlTiming.DurationMilliseconds.Value / 1000;

                            sqlSub.Sql["sanitized_query"] = sqlTiming.CommandString;

                            subSegment.AddSubsegment(sqlSub);

                            sqlSub.Release();
                        }

            subSegment.Release();
        }

        private void AddHttpInformation(Segment trace)
        {
            if (_provider == null) return;

            var httpContextAccesor = _provider.GetRequiredService<IHttpContextAccessor>();

            var httpContext = httpContextAccesor?.HttpContext;

            if (httpContext == null) return;

            var request = httpContext.Request;

            var requestAttributes = new Dictionary<string, object>
            {
                {"url", request.GetDisplayUrl()},
                {"method", request.Method}
            };

            var xForwardedFor = GetXForwardedFor(request);

            if (xForwardedFor == null)
            {
                requestAttributes["client_ip"] = GetClientIpAddress(request);
            }
            else
            {
                requestAttributes["client_ip"] = xForwardedFor;
                requestAttributes["x_forwarded_for"] = true;
            }

            if (request.Headers.ContainsKey(HeaderNames.UserAgent))
                requestAttributes["user_agent"] = request.Headers[HeaderNames.UserAgent].ToString();

            trace.Http["request"] = requestAttributes;

            var response = httpContext.Response;

            var reponseAttributes = new Dictionary<string, object>();

            var statusCode = response.StatusCode;

            reponseAttributes["status"] = statusCode;

            if (response.Headers.ContentLength != null)
                reponseAttributes["content_length"] = response.Headers.ContentLength;

            if (statusCode >= 400 && statusCode <= 499)
            {
                trace.HasError = true;

                if (statusCode == 429) trace.IsThrottled = true;
            }
            else if (statusCode >= 500 && statusCode <= 599)
            {
                trace.HasFault = true;
            }

            trace.Http["response"] = reponseAttributes;
        }

        public static string NewId(DateTime when)
        {
            // Get epoch second as 32bit integer
            var epoch = (int) when.ToUnixTimeSeconds();

            // Get a 96 bit random number
            var randomNumber = ThreadSafeRandom.GenerateHexNumber(RandomNumberHexDigits);

            string[] arr = {"1", epoch.ToString("x", CultureInfo.InvariantCulture), randomNumber};

            // Concatenate elements with dash
            return string.Join("-", arr);
        }

        private static string GetXForwardedFor(HttpRequest request)
        {
            string clientIp = null;

            if (request.HttpContext.Request.Headers.TryGetValue(X_FORWARDED_FOR, out var headerValue))
                if (headerValue.ToArray().Length >= 1)
                    clientIp = headerValue.ToArray()[0];

            return string.IsNullOrEmpty(clientIp) ? null : clientIp.Split(',')[0].Trim();
        }

        private static string GetClientIpAddress(HttpRequest request)
        {
            return request.HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}