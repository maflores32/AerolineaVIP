using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using AerolineaVIP.Models;

namespace AerolineaVIP.Controllers
{
    [SessionAuthorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager )
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set 
            { 
                _signInManager = value; 
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // No cuenta los errores de inicio de sesión para el bloqueo de la cuenta
            // Para permitir que los errores de contraseña desencadenen el bloqueo de la cuenta, cambie a shouldLockout: true
            var user = await UserManager.FindByNameAsync(model.UserName);
            if (user == null)
            {
                ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                return View(model);
            }

            if (await UserManager.IsLockedOutAsync(user.Id))
            {
                return View("Lockout");
            }

            if (!await UserManager.CheckPasswordAsync(user, model.Password))
            {
                await UserManager.AccessFailedAsync(user.Id);
                ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                return await UserManager.IsLockedOutAsync(user.Id) ? View("Lockout") : View(model);
            }

            await UserManager.ResetAccessFailedCountAsync(user.Id);
            var roles = await UserManager.GetRolesAsync(user.Id);
            SessionAuthentication.SignIn(HttpContext, user.Id, user.UserName, roles);
            return RedirectToLocal(returnUrl, roles.Contains("Administrador") || roles.Contains("SuperUsuario"));
        }

        //
        // GET: /Account/VerifyCode
        [AllowAnonymous]
        public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "La autenticación en dos pasos no está disponible con el acceso por sesión actual.");
        }

        //
        // POST: /Account/VerifyCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "La autenticación en dos pasos no está disponible con el acceso por sesión actual.");
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            if (User.Identity.IsAuthenticated && !User.IsInRole("SuperUsuario"))
            {
                return RedirectToAction("Index", "Home");
            }

            var model = new RegisterViewModel();
            CargarRoles(model);
            return View(model);
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (User.Identity.IsAuthenticated && !User.IsInRole("SuperUsuario"))
            {
                return RedirectToAction("Index", "Home");
            }

            // Un visitante siempre queda como Cliente; el rol enviado solo se respeta si quien registra es SuperUsuario
            string rolFinal = "Cliente";
            if (User.IsInRole("SuperUsuario") && model.Rol != null && RolesYPermisos.ContainsKey(model.Rol))
            {
                rolFinal = model.Rol;
            }

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.UserName, Email = model.Email };
                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    foreach (var rol in RolesYPermisos[rolFinal])
                    {
                        await UserManager.AddToRoleAsync(user.Id, rol);
                    }

                    if (User.IsInRole("SuperUsuario"))
                    {
                        // El SuperUsuario conserva su sesión al crear cuentas para otros
                        ModelState.Clear();
                        ViewBag.Mensaje = "Cuenta creada correctamente.";
                        var nuevo = new RegisterViewModel();
                        CargarRoles(nuevo);
                        return View(nuevo);
                    }

                    SessionAuthentication.SignIn(HttpContext, user.Id, user.UserName, await UserManager.GetRolesAsync(user.Id));

                    return RedirectToAction("Index", "Home");
                }
                AddErrors(result);
            }

            // Si llegamos a este punto, es que se ha producido un error y volvemos a mostrar el formulario
            CargarRoles(model);
            return View(model);
        }

        // Roles y los permisos CRUD que recibe cada uno (igual que en Global.asax.cs)
        private static readonly Dictionary<string, string[]> RolesYPermisos = new Dictionary<string, string[]>
        {
            { "SuperUsuario", new string[] { "SuperUsuario", "Create", "Edit", "Details", "Delete" } },
            { "Administrador", new string[] { "Administrador", "Create", "Edit", "Details" } },
            { "Counter", new string[] { "Counter", "Details" } },
            { "Cliente", new string[] { "Cliente" } }
        };

        private void CargarRoles(RegisterViewModel model)
        {
            var roles = new List<SelectListItem>();

            if (User.IsInRole("SuperUsuario"))
            {
                foreach (var rol in new string[] { "Cliente", "Counter", "Administrador", "SuperUsuario" })
                {
                    roles.Add(new SelectListItem { Value = rol, Text = rol });
                }
            }
            else
            {
                roles.Add(new SelectListItem { Value = "Cliente", Text = "Cliente" });
                model.Rol = "Cliente";
            }

            model.Roles = roles;
        }

        //
        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        //
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "La recuperación de contraseña requiere configurar el envío de correo.");
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "La recuperación de contraseña requiere configurar el envío de correo.");
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "La recuperación de contraseña requiere configurar el envío de correo.");
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "La recuperación de contraseña requiere configurar el envío de correo.");
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        //
        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "El inicio de sesión externo no está disponible en modo de autenticación por sesión.");
        }

        //
        // GET: /Account/SendCode
        [AllowAnonymous]
        public async Task<ActionResult> SendCode(string returnUrl, bool rememberMe)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "La autenticación en dos pasos no está disponible con el acceso por sesión actual.");
        }

        //
        // POST: /Account/SendCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendCode(SendCodeViewModel model)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "La autenticación en dos pasos no está disponible con el acceso por sesión actual.");
        }

        //
        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "El inicio de sesión externo no está disponible en modo de autenticación por sesión.");
        }

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotImplemented, "El inicio de sesión externo no está disponible en modo de autenticación por sesión.");
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            SessionAuthentication.SignOut(HttpContext);
            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Account/ExternalLoginFailure
        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Aplicaciones auxiliares
        // Se usa para la protección XSRF al agregar inicios de sesión externos
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl, bool isAdministrator)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            if (isAdministrator)
            {
                return RedirectToAction("Index", "Admin");
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}