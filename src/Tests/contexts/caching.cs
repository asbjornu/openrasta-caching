using System;
using System.Collections.Generic;
using OpenRasta.Caching;
using OpenRasta.Caching.Configuration;
using OpenRasta.Caching.Providers;
using OpenRasta.Configuration;
using OpenRasta.Configuration.Fluent;
using OpenRasta.DI;
using OpenRasta.Hosting.InMemory;
using OpenRasta.Testing;
using OpenRasta.Web;

namespace Tests.contexts
{
    public class caching : IDisposable
    {
        protected static DateTimeOffset? now = DateTimeOffset.UtcNow;
        protected object resource;
        protected IResponse response;
        readonly TestConfiguration _configuration = new TestConfiguration();
        readonly IDictionary<string, string> _requestHeaders = new Dictionary<string, string>();
        InMemoryHost _host;
        string _method = "GET";

        public caching()
        {
            _configuration.Uses.Add(() => ResourceSpace.Uses.Caching());
        }

        protected ICacheProvider cache
        {
            get { return _host.Resolver.Resolve<ICacheProvider>(); }
        }

        void IDisposable.Dispose()
        {
            _host.Close();
        }

        protected void given_has(Action<IHas> has)
        {
            _configuration.Has.Add(() => has(ResourceSpace.Has));
        }

        protected void given_request_header(string header, string value)
        {
            _requestHeaders[header] = value;
        }

        protected void given_request_header(string header, DateTimeOffset? value)
        {
            _requestHeaders[header] = value.Value.ToUniversalTime().ToString("R");
        }

        protected void given_resource<T>(string uri, Action<IResource> configuration, T resource)
        {
            Action action = () =>
            {
                IResourceDefinition res = ResourceSpace.Has.ResourcesOfType<T>();
                if (configuration != null) configuration(res);
                res.AtUri(uri)
                    .HandledBy<ResourceHandler>()
                    .TranscodedBy<NullCodec>();
            };
            _configuration.Has.Add(action);
            this.resource = resource;
        }

        protected void given_resource<T>(string uri, T resource)
        {
            given_resource(uri, null, resource);
        }

        protected void given_time(DateTimeOffset? dateTimeOffset)
        {
            now = dateTimeOffset;
            ServerClock.UtcNowDefinition = () => dateTimeOffset.Value;
        }

        protected void given_uses(Action<IUses> use)
        {
            _configuration.Uses.Add(() => use(ResourceSpace.Uses));
        }

        protected void should_be_date(string input, DateTimeOffset? expected)
        {
            DateTimeOffset.Parse(response.Headers["last-modified"]).ToUniversalTime()
                .ToString("R").ShouldBe(expected.Value.ToUniversalTime().ToString("R"));
        }

        protected void when_executing_request(string uri)
        {
            _host = new InMemoryHost(_configuration);
            _host.Resolver.AddDependencyInstance(typeof(caching), this, DependencyLifetime.Singleton);
            var request = new InMemoryRequest
            {
                HttpMethod = _method,
                Uri = new Uri(new Uri("http://localhost"), new Uri(uri, UriKind.RelativeOrAbsolute))
            };
            foreach (var kv in _requestHeaders)
                request.Headers.Add(kv);
            response = _host.ProcessRequest(request);
        }

        public class ResourceHandler
        {
            readonly caching test;

            public ResourceHandler(caching test)
            {
                this.test = test;
            }

            public object Get()
            {
                return test.resource;
            }
        }
    }

    public class TestConfiguration : IConfigurationSource
    {
        public List<Action> Has = new List<Action>();
        public List<Action> Uses = new List<Action>();

        public void Configure()
        {
            using (OpenRastaConfiguration.Manual)
            {
                Uses.ForEach(x => x());
                Has.ForEach(x => x());
            }
        }
    }
}