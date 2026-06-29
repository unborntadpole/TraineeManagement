using System.Net;

namespace  SubmissionProcessor.HttpHandler;

public class HttpStatusCodeFallbackHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable || (int)response.StatusCode >= 500) 
        {
            var fallbackResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };
            return fallbackResponse;
        }
        return response;
    }
}
