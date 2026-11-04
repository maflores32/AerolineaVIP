using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(AerolineaVIP.Startup))]
namespace AerolineaVIP
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
