using System;
using System.Collections.Generic;
using System.Linq;
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

        private void CrearRolesYUsuarios()
        {
            ApplicationDbContext context = new ApplicationDbContext();
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

        private void CrearRolConId(RoleManager<IdentityRole> roleManager, string id, string nombreRol)
        {
            if (!roleManager.RoleExists(nombreRol))
            {
                var role = new IdentityRole
                {
                    Id = id,
                    Name = nombreRol
                };
                roleManager.Create(role);
            }
        }

        private void CrearUsuarioConRoles(UserManager<ApplicationUser> userManager, string userName, string email, string password, string[] roles)
        {
            // Busca por nombre de usuario o por email para no duplicar
            var user = userManager.FindByName(userName) ?? userManager.FindByEmail(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = userName,
                    Email = email
                };

                var resultado = userManager.Create(user, password);
                if (resultado.Succeeded)
                {
                    foreach (var rol in roles)
                    {
                        userManager.AddToRole(user.Id, rol);
                    }
                }
            }
            else
            {
                // Si ya existía, actualiza el UserName y el Email para asegurar que coincidan
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

                // Asignar los roles que le falten
                foreach (var rol in roles)
                {
                    if (!userManager.IsInRole(user.Id, rol))
                    {
                        userManager.AddToRole(user.Id, rol);
                    }
                }
            }
        }
    }
}