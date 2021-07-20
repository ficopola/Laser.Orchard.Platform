using Orchard.UI.Resources;

namespace Laser.Orchard.Cookies
{
    public class ResourceManifest : IResourceManifestProvider
    {
        public void BuildManifests(ResourceManifestBuilder builder)
        {
            var manifest = builder.Add();

            manifest.DefineScript("jQueryCookie")
                .SetUrl("jquery.cookie.min.js", "jquery.cookie.js")
                .SetDependencies("jQuery")
                .SetVersion("1.4.0");

            manifest.DefineScript("CookieCuttr")
                .SetUrl("jquery.cookiecuttr.min.js?v=1.1.3", "jquery.cookiecuttr.js?v=1.1.3")
                .SetDependencies("jQueryCookie", "jQuery")
                .SetVersion("1.1.3");

            manifest.DefineStyle("CookieCuttr")
                .SetUrl("cookiecuttr.min.css", "cookiecuttr.css")
                .SetVersion("1.0.4");
        }
    }
}
