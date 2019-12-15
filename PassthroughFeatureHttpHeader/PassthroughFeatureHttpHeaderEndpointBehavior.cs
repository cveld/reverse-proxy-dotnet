using System;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Web;

// https://blogs.msmvps.com/paulomorgado/2007/04/26/wcf-building-an-http-user-agent-message-inspector/

namespace PassthroughFeatureHttpHeader
{
    // the following attribute is required in order to access ASP.NET's current HttpContext:
    // https://stackoverflow.com/questions/5904313/access-httpcontext-current-from-wcf-web-service
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Required)]
    public class PassthroughFeatureHttpHeaderEndpointBehavior : BehaviorExtensionElement, IEndpointBehavior, IClientMessageInspector
    {
        const string FEATURE_HEADER = "feature";

        public override Type BehaviorType => typeof(PassthroughFeatureHttpHeaderEndpointBehavior);

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
            // empty
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            // no changes required
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            var inspector = this;
            // HttpUserAgentMessageInspector inspector = new HttpUserAgentMessageInspector(this.m_userAgent);
            clientRuntime.MessageInspectors.Add(inspector);
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            // empty
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            HttpRequestMessageProperty httpRequestMessage;

            object httpRequestMessageObject;
            var feature = HttpContext.Current.Request.Headers[FEATURE_HEADER];

            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out httpRequestMessageObject))

            {
                httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;

                if (string.IsNullOrEmpty(httpRequestMessage.Headers[FEATURE_HEADER]))
                {                    
                    httpRequestMessage.Headers[FEATURE_HEADER] = feature;
                }
            }
            else
            {
                httpRequestMessage = new HttpRequestMessageProperty();
                httpRequestMessage.Headers.Add(FEATURE_HEADER, feature);
                request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
            }

            return null;
        }

        public void Validate(ServiceEndpoint endpoint)
        {
            // empty
        }

        protected override object CreateBehavior()
        {
            return this;
            // return new HttpUserAgentEndpointBehavior(UserAgent);
        }
    }
}
