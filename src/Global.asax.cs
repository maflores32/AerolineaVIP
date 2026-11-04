using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using AerolineaVIP.Models;

namespace AerolineaVIP
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Inicializar Roles y Usuarios al arrancar la aplicación
            CrearRolesYUsuarios();
        }

        protected void Application_PostAcquireRequestState(object sender, EventArgs e)
        {
            SessionAuthentication.Authenticate(Context);
        }

        private void CrearRolesYUsuarios()
        {
            using (var context = new ApplicationDbContext())
            {
                var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));
                var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context));

                // 1. Roles de usuario
                string[] rolesUsuario = { "SuperUsuario", "Administrador", "Counter", "Cliente" };
                foreach (var rol in rolesUsuario)
                {
                    if (!roleManager.RoleExists(rol))
                    {
                        roleManager.Create(new IdentityRole(rol));
                    }
                }

                // 2. Roles de permisos CRUD con Id específico (1, 2, 3, 4)
                CrearRolConId(roleManager, "1", "Create");
                CrearRolConId(roleManager, "2", "Edit");
                CrearRolConId(roleManager, "3", "Details");
                CrearRolConId(roleManager, "4", "Delete");

                // 3. Crear Usuarios con Nombre de Usuario (para Login) y Correo registrado

                // SuperUsuario: acceso total y eliminación
                CrearUsuarioConRoles(userManager, "superusuario", "superusuario@gmail.com", "Superusuario123!",
                    new string[] { "SuperUsuario", "Create", "Edit", "Details", "Delete" });

                // Administrador: CRUD operativo sin eliminación (no tiene Delete)
                CrearUsuarioConRoles(userManager, "administrador", "administrador@gmail.com", "Admin123!",
                    new string[] { "Administrador", "Create", "Edit", "Details" });

                // Counter: consultas y emisión de check-in
                CrearUsuarioConRoles(userManager, "counter", "counter@gmail.com", "Counter123!",
                    new string[] { "Counter", "Details" });

                // Cliente inicial vinculado a las reservas de demostración del script SQL
                if (userManager.FindByName("repinos1") == null)
                {
                    var clienteDemo = new ApplicationUser
                    {
                        Id = "7c1e4a20-0001-4b6e-9a01-000000000001", // Id oficial de las reservas R01, R06, R11
                        UserName = "repinos1",
                        Email = "repinos1@gmail.com",
                        EmailConfirmed = true
                    };
                    if (userManager.Create(clienteDemo, "Repinos123!").Succeeded)
                    {
                        userManager.AddToRole(clienteDemo.Id, "Cliente");
                    }
                }
            }
        }

        private void CrearRolConId(RoleManager<IdentityRole> roleManager, string id, string nombreRol)
        {
            if (!roleManager.RoleExists(nombreRol))
            {
                roleManager.Create(new IdentityRole { Id = id, Name = nombreRol });
            }
        }

        private void CrearUsuarioConRoles(UserManager<ApplicationUser> userManager, string userName, string email, string password, string[] roles)
        {
            var user = userManager.FindByName(userName) ?? userManager.FindByEmail(email);
            if (user == null)
            {
                user = new ApplicationUser { UserName = userName, Email = email };
                if (!userManager.Create(user, password).Succeeded)
                {
                    return;
                }
            }
            else
            {
                if (user.UserName != userName)
                {
                    user.UserName = userName;
                    userManager.Update(user);
                }
                if (user.Email != email)
                {
                    user.Email = email;
                    userManager.Update(user);
                }
            }

            foreach (var rol in roles)
            {
                if (!userManager.IsInRole(user.Id, rol))
                {
                    userManager.AddToRole(user.Id, rol);
                }
            }
        }
    }

    public static class SessionAuthentication
    {
        private const string UserIdKey = "SessionUserId";
        private const string UserNameKey = "SessionUserName";
        private const string RolesKey = "SessionUserRoles";

        public static void SignIn(HttpContextBase context, string userId, string userName, IEnumerable<string> roles)
        {
            context.Session.Clear();
            context.Session[UserIdKey] = userId;
            context.Session[UserNameKey] = userName;
            context.Session[RolesKey] = (roles ?? Enumerable.Empty<string>()).ToArray();
            SetPrincipal(context, userId, userName, (string[])context.Session[RolesKey]);
        }

        public static void SignOut(HttpContextBase context)
        {
            if (context.Session != null)
            {
                context.Session.Clear();
                context.Session.Abandon();
            }

            var anonymous = new GenericPrincipal(new GenericIdentity(String.Empty), new string[0]);
            context.User = anonymous;
            Thread.CurrentPrincipal = anonymous;
        }

        public static void Authenticate(HttpContext context)
        {
            if (context == null || context.Session == null)
            {
                return;
            }

            string userId = context.Session[UserIdKey] as string;
            string userName = context.Session[UserNameKey] as string;
            string[] roles = context.Session[RolesKey] as string[];

            if (String.IsNullOrWhiteSpace(userId) || String.IsNullOrWhiteSpace(userName))
            {
                var anonymous = new GenericPrincipal(new GenericIdentity(String.Empty), new string[0]);
                context.User = anonymous;
                Thread.CurrentPrincipal = anonymous;
                return;
            }

            SetPrincipal(context, userId, userName, roles ?? new string[0]);
        }

        private static void SetPrincipal(HttpContextBase context, string userId, string userName, IEnumerable<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName)
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Session"));
            context.User = principal;
            Thread.CurrentPrincipal = principal;
        }

        private static void SetPrincipal(HttpContext context, string userId, string userName, IEnumerable<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName)
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Session"));
            context.User = principal;
            Thread.CurrentPrincipal = principal;
        }
    }

    public sealed class SessionAuthorizeAttribute : AuthorizeAttribute
    {
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true) ||
                filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true))
            {
                return;
            }

            base.OnAuthorization(filterContext);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary
                {
                    { "controller", "Account" },
                    { "action", "Login" },
                    { "returnUrl", filterContext.HttpContext.Request.RawUrl }
                });
                return;
            }

            filterContext.Result = new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
        }
    }
}