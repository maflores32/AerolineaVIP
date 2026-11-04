using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using AerolineaVIP.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;

namespace AerolineaVIP.Controllers
{
    [SessionAuthorize(Roles = "SuperUsuario, Administrador")]
    public class AdminController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();
        private ApplicationUserManager UserManager
        {
            get { return HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>(); }
        }

        // ============================================================
        // PANTALLA 17: PANEL DE ADMINISTRACIÓN
        // ============================================================
        // GET: /Admin
        // GET: /Admin/Index
        public ActionResult Index()
        {
            DateTime inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            decimal ingresos = db.Compras
                .Where(c => c.EstadoCompraCodigo == "PAGADA" && c.FechaCompra >= inicioMes)
                .Select(c => (decimal?)c.Importe)
                .ToList()
                .Sum() ?? 0m;

            return View(new AdminDashboardViewModel
            {
                VuelosHoy = db.Vueloes.Count(v => DbFunctions.TruncateTime(v.FechaOperacion) == DateTime.Today),
                ReservasPendientes = db.Reservas.Count(r => r.EstadoReservaCodigo == "PENDIENTE"),
                AeronavesDisponibles = db.Aeronaves.Count(a => a.EstadoAeronaveCodigo == "DISPONIBLE"),
                RutasActivas = db.Rutas.Count(r => r.Activa),
                IngresosMes = ingresos
            });
        }

        // ============================================================
        // PANTALLA 18: PROGRAMACIÓN OPERATIVA DE VUELOS
        // ============================================================
        // GET: /Admin/Vuelos
        public ActionResult Vuelos(DateTime? fechaInicio, DateTime? fechaFin, string estado)
        {
            return RedirectToAction("Programacion", "Vuelo", new { fechaInicio, fechaFin, estado });
        }

        // ============================================================
        // PANTALLA 19: GESTIÓN DE FLOTA Y AERONAVES
        // ============================================================
        // GET: /Admin/Aeronaves
        public ActionResult Aeronaves()
        {
            var aeronaves = db.Aeronaves
                .Include(a => a.ModeloAeronave)
                .Include(a => a.EstadoAeronave)
                .OrderBy(a => a.Matricula)
                .ToList();

            ViewBag.Estados = new SelectList(db.EstadoAeronaves.ToList(), "Codigo", "Descripcion");
            ViewBag.Modelos = new SelectList(db.ModeloAeronaves.ToList(), "ModeloAeronaveId", "Nombre");

            return View(aeronaves);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearAeronave(string matricula, int modeloAeronaveId)
        {
            matricula = (matricula ?? "").Trim().ToUpperInvariant();
            var modelo = db.ModeloAeronaves.FirstOrDefault(m => m.ModeloAeronaveId == modeloAeronaveId);
            if (!System.Text.RegularExpressions.Regex.IsMatch(matricula, @"^[A-Z0-9-]{3,15}$") ||
                db.Aeronaves.Any(a => a.Matricula == matricula) || modelo == null || modelo.CapacidadAsientos < 1 || modelo.CapacidadAsientos > 7)
            {
                TempData["ErrorAdmin"] = "Ingrese una matrícula única y un modelo activo con capacidad de hasta 7 asientos.";
                return RedirectToAction("Aeronaves");
            }

            db.Aeronaves.Add(new Aeronave
            {
                Matricula = matricula,
                ModeloAeronaveId = modeloAeronaveId,
                EstadoAeronaveCodigo = "DISPONIBLE"
            });
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Aeronave creada y registrada como disponible.";
            return RedirectToAction("Aeronaves");
        }

        // POST: /Admin/CambiarEstadoAeronave
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "SuperUsuario, Administrador")]
        public ActionResult CambiarEstadoAeronave(int aeronaveId, string nuevoEstado)
        {
            if (nuevoEstado != "DISPONIBLE" && nuevoEstado != "NO_DISPONIBLE")
            {
                TempData["ErrorAdmin"] = "Seleccione un estado operativo válido.";
                return RedirectToAction("Aeronaves");
            }
            var aeronave = db.Aeronaves.FirstOrDefault(a => a.AeronaveId == aeronaveId);
            if (aeronave != null)
            {
                try
                {
                    if (!db.EstadoAeronaves.Any(e => e.Codigo == nuevoEstado))
                    {
                        TempData["ErrorAdmin"] = "Seleccione un estado de aeronave válido.";
                        return RedirectToAction("Aeronaves");
                    }
                    aeronave.EstadoAeronaveCodigo = nuevoEstado;
                    db.SaveChanges();
                    TempData["MensajeAdmin"] = "Aeronave " + aeronave.Matricula + " actualizada a '" + nuevoEstado + "'.";
                }
                catch (Exception ex)
                {
                    string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    TempData["ErrorAdmin"] = "Error al cambiar estado: " + msg;
                }
            }
            return RedirectToAction("Aeronaves");
        }

        // ============================================================
        // PANTALLA 20: GESTIÓN DE USUARIOS, PASAJEROS Y RESERVAS
        // ============================================================
        // GET: /Admin/Gestion
        public async System.Threading.Tasks.Task<ActionResult> Gestion(string tab = "usuarios", string buscar = "")
        {
            // Preparamos el ViewModel para la vista
            var model = new GestionAdminViewModel
            {
                TabActivo = tab,
                Buscar = buscar
            };

            // Lógica para las pestañas
            if (tab == "pasajeros")
            {
                var queryPasajeros = db.Pasajeroes.AsQueryable();
                if (!string.IsNullOrEmpty(buscar))
                {
                    queryPasajeros = queryPasajeros.Where(p => p.Nombres.Contains(buscar) || p.Apellidos.Contains(buscar) || p.NumeroDocumento.Contains(buscar));
                }
                model.Pasajeros = queryPasajeros.OrderBy(p => p.Apellidos).Take(50).ToList();
            }
            else if (tab == "reservas")
            {
                var queryReservas = db.Reservas.AsQueryable();
                if (!string.IsNullOrEmpty(buscar))
                {
                    queryReservas = queryReservas.Where(r => r.CodigoReserva.Contains(buscar));
                }
                model.Reservas = queryReservas.OrderByDescending(r => r.FechaCreacion).Take(50).ToList();
            }
            else
            {
                var usuarios = UserManager.Users.ToList();
                var usuariosFiltrados = string.IsNullOrWhiteSpace(buscar)
                    ? usuarios
                    : usuarios.Where(u => (u.UserName ?? "").IndexOf(buscar, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          (u.Email ?? "").IndexOf(buscar, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                var filas = new List<UsuarioAdminViewModel>();
                foreach (var usuario in usuariosFiltrados.OrderBy(u => u.UserName).Take(50))
                {
                    var rolesUsuario = await UserManager.GetRolesAsync(usuario.Id);
                    filas.Add(new UsuarioAdminViewModel
                    {
                        Id = usuario.Id,
                        Nombre = usuario.UserName,
                        Correo = usuario.Email,
                        Rol = string.Join(", ", rolesUsuario),
                        Activo = !await UserManager.IsLockedOutAsync(usuario.Id)
                    });
                }
                model.Cuentas = filas;
                model.EsSuperUsuario = User.IsInRole("SuperUsuario");
                ViewBag.RolesDisponibles = new SelectList(new[] { "Cliente", "Counter", "Administrador", "SuperUsuario" });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "SuperUsuario")]
        public async System.Threading.Tasks.Task<ActionResult> CambiarRol(string usuarioId, string rol)
        {
            var rolesValidos = new[] { "Cliente", "Counter", "Administrador", "SuperUsuario" };
            var usuario = await UserManager.FindByIdAsync(usuarioId);
            if (usuario == null || !rolesValidos.Contains(rol))
            {
                TempData["ErrorAdmin"] = "Seleccione una cuenta y un rol válidos.";
                return RedirectToAction("Gestion", new { tab = "usuarios" });
            }
            if (usuario.Id == User.Identity.GetUserId() && rol != "SuperUsuario")
            {
                TempData["ErrorAdmin"] = "No puede retirar su propio rol de SuperUsuario.";
                return RedirectToAction("Gestion", new { tab = "usuarios" });
            }

            var rolesActuales = await UserManager.GetRolesAsync(usuario.Id);
            using (var identityContext = new ApplicationDbContext())
            {
                var superRole = await identityContext.Roles.FirstOrDefaultAsync(r => r.Name == "SuperUsuario");
                if (rolesActuales.Contains("SuperUsuario") && rol != "SuperUsuario" &&
                    (superRole == null || UserManager.Users.Count(u => u.Roles.Any(r => r.RoleId == superRole.Id)) <= 1))
                {
                    TempData["ErrorAdmin"] = "Debe conservar al menos un SuperUsuario.";
                    return RedirectToAction("Gestion", new { tab = "usuarios" });
                }
            }

            if (rolesActuales.Any())
            {
                var quitarResultado = await UserManager.RemoveFromRolesAsync(usuario.Id, rolesActuales.ToArray());
                if (!quitarResultado.Succeeded)
                {
                    TempData["ErrorAdmin"] = string.Join(" ", quitarResultado.Errors);
                    return RedirectToAction("Gestion", new { tab = "usuarios" });
                }
            }
            var permisos = new Dictionary<string, string[]>
            {
                { "SuperUsuario", new[] { "SuperUsuario", "Create", "Edit", "Details", "Delete" } },
                { "Administrador", new[] { "Administrador", "Create", "Edit", "Details" } },
                { "Counter", new[] { "Counter", "Details" } },
                { "Cliente", new[] { "Cliente" } }
            };
            var resultado = await UserManager.AddToRolesAsync(usuario.Id, permisos[rol]);
            if (!resultado.Succeeded && rolesActuales.Any())
            {
                await UserManager.AddToRolesAsync(usuario.Id, rolesActuales.ToArray());
            }
            TempData[resultado.Succeeded ? "MensajeAdmin" : "ErrorAdmin"] = resultado.Succeeded
                ? "Rol actualizado correctamente. La cuenta debe volver a iniciar sesión para aplicar el cambio."
                : string.Join(" ", resultado.Errors);
            return RedirectToAction("Gestion", new { tab = "usuarios" });
        }

        // ============================================================
        // PANTALLA 21: REPORTES OPERATIVOS Y DE OCUPACIÓN
        // ============================================================
        // GET: /Admin/Reportes
        public ActionResult Reportes()
        {
            var reporte = (from vd in db.vw_VueloDetalle
                           join disp in db.vw_DisponibilidadVuelo on vd.VueloId equals disp.VueloId into gDisp
                           from d in gDisp.DefaultIfEmpty()
                           select new ReporteVueloViewModel
                           {
                               VueloId = vd.VueloId,
                               CodigoVuelo = vd.CodigoVuelo,
                               FechaOperacion = vd.FechaOperacion,
                               Ruta = vd.CiudadOrigen + " (" + vd.Origen + ") -> " + vd.CiudadDestino + " (" + vd.Destino + ")",
                               TarifaReferencial = vd.TarifaReferencial,
                               Capacidad = d != null ? d.Capacidad : 7,
                               Ocupados = d != null ? d.Ocupados : 0,
                               Disponibles = d != null && d.Disponibles.HasValue ? d.Disponibles.Value : 7,
                               Estado = vd.EstadoVueloCodigo,
                               Aeronave = d != null ? d.Matricula : "-"
                           })
                           .OrderByDescending(r => r.FechaOperacion)
                           .Take(30)
                           .ToList();

            return View(reporte);
        }

        // ============================================================
        // LIBERACIÓN DE RECURSOS
        // ============================================================
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ============================================================
    // VIEWMODELS AUXILIARES
    // ============================================================

    // Modelo para la Pantalla 20 (Gestión)
    public class GestionAdminViewModel
    {
        public string TabActivo { get; set; }
        public string Buscar { get; set; }

        // Listas para las tablas
        public List<Pasajero> Usuarios { get; set; }
        public List<UsuarioAdminViewModel> Cuentas { get; set; }
        public bool EsSuperUsuario { get; set; }
        public List<Pasajero> Pasajeros { get; set; }
        public List<Reserva> Reservas { get; set; }
    }

    public class UsuarioAdminViewModel
    {
        public string Id { get; set; }
        public string Nombre { get; set; }
        public string Correo { get; set; }
        public string Rol { get; set; }
        public bool Activo { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int VuelosHoy { get; set; }
        public int ReservasPendientes { get; set; }
        public int AeronavesDisponibles { get; set; }
        public int RutasActivas { get; set; }
        public decimal IngresosMes { get; set; }
    }

    // Modelo auxiliar para el reporte de ocupación de la Pantalla 21
    public class ReporteVueloViewModel
    {
        public int VueloId { get; set; }
        public string CodigoVuelo { get; set; }
        public DateTime FechaOperacion { get; set; }
        public string Ruta { get; set; }
        public decimal TarifaReferencial { get; set; }
        public int Capacidad { get; set; }
        public int Ocupados { get; set; }
        public int Disponibles { get; set; }
        public string Estado { get; set; }
        public string Aeronave { get; set; }
        public decimal IngresosEstimados => Ocupados * TarifaReferencial;
        public double PorcentajeOcupacion => Capacidad > 0 ? Math.Round(((double)Ocupados / Capacidad) * 100, 1) : 0;
    }
}