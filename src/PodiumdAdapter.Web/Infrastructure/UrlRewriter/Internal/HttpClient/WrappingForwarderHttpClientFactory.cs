using Yarp.ReverseProxy.Forwarder;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter.Internal.HttpClient
{
    public class WrappingForwarderHttpClientFactory : ForwarderHttpClientFactory
    {
        private readonly IEnumerable<WrapHandler> _wrappers;

        public WrappingForwarderHttpClientFactory(IEnumerable<WrapHandler> wrappers)
        {
            _wrappers = wrappers;
        }

        protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
        {
            var inner = base.WrapHandler(context, handler);
            return _wrappers.Aggregate(inner, (x, wrapper) => wrapper.Invoke(x));
        }
    }
}
