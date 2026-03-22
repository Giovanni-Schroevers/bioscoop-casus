using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BioscoopCasus.Web.Handlers;

public class AcceptLanguageHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.ParseAdd(culture);
        
        return base.SendAsync(request, cancellationToken);
    }
}
