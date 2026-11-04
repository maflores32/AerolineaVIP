using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Data.SqlClient;
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

        private bool ExisteConflictoAeronave(Vuelo vuelo, int aeronaveId)
        {
            DateTime salida = vuelo.FechaOperacion.Date.Add(vuelo.Frecuencia.HoraSalida);
            DateTime llegada = vuelo.FechaOperacion.Date.Add(vuelo.Frecuencia.HoraLlegada);
            var vuelosMismaAeronave = db.Vueloes
                .Include(v => v.Frecuencia)
                .Where(v => v.AeronaveId == aeronaveId && v.VueloId != vuelo.VueloId &&
                            v.EstadoVueloCodigo != "CANCELADO" &&
                            DbFunctions.TruncateTime(v.FechaOperacion) == vuelo.FechaOperacion.Date)
                .ToList();

            return vuelosMismaAeronave.Any(otro =>
            {
                DateTime otraSalida = otro.FechaOperacion.Date.Add(otro.Frecuencia.HoraSalida);
                DateTime otraLlegada = otro.FechaOperacion.Date.Add(otro.Frecuencia.HoraLlegada);
                return salida < otraLlegada.AddMinutes(45) && llegada.AddMinutes(45) > otraSalida;
            });
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
            if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio.Value.Date > fechaFin.Value.Date)
            {
                TempData["ErrorAdmin"] = "La fecha inicial debe ser anterior o igual a la fecha final.";
                return RedirectToAction("Vuelos");
            }

            var query = db.vw_VueloDetalle.AsQueryable();

            if (fechaInicio.HasValue)
            {
                DateTime fIni = fechaInicio.Value.Date;
                query = query.Where(v => DbFunctions.TruncateTime(v.FechaOperacion) >= fIni);
            }

            if (fechaFin.HasValue)
            {
                DateTime fFin = fechaFin.Value.Date;
                query = query.Where(v => DbFunctions.TruncateTime(v.FechaOperacion) <= fFin);
            }

            if (!string.IsNullOrEmpty(estado))
            {
                query = query.Where(v => v.EstadoVueloCodigo == estado);
            }

            var lista = query.OrderByDescending(v => v.FechaOperacion)
                             .ThenBy(v => v.HoraSalida)
                             .ToList();

            ViewBag.Estados = new SelectList(db.EstadoVueloes.ToList(), "Codigo", "Descripcion", estado);
            ViewBag.Aeronaves = new SelectList(db.Aeronaves.Where(a => a.EstadoAeronaveCodigo == "DISPONIBLE").OrderBy(a => a.Matricula).ToList(), "AeronaveId", "Matricula");
            ViewBag.FechaInicio = fechaInicio.HasValue ? fechaInicio.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.FechaFin = fechaFin.HasValue ? fechaFin.Value.ToString("yyyy-MM-dd") : "";

            return View(lista);
        }

        // POST: /Admin/GenerarVuelos (Invoca el Stored Procedure dbo.usp_GenerarVuelos)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "SuperUsuario, Administrador")]
        public ActionResult GenerarVuelos(DateTime fechaInicio, DateTime fechaFin)
        {
            if (fechaInicio.Date < DateTime.Today || fechaInicio.Date > fechaFin.Date || fechaFin.Date > DateTime.Today.AddDays(365))
            {
                TempData["ErrorAdmin"] = "Use fechas futuras y un rango máximo de un año.";
                return RedirectToAction("Vuelos");
            }

            try
            {
                var paramInicio = new SqlParameter("@FechaInicio", fechaInicio.Date);
                var paramFin = new SqlParameter("@FechaFin", fechaFin.Date);

                db.Database.ExecuteSqlCommand("EXEC dbo.usp_GenerarVuelos @FechaInicio, @FechaFin", paramInicio, paramFin);

                TempData["MensajeAdmin"] = "Se ejecutó el procedimiento dbo.usp_GenerarVuelos exitosamente desde " + fechaInicio.ToString("dd/MM/yyyy") + " hasta " + fechaFin.ToString("dd/MM/yyyy") + ".";
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException != null && ex.InnerException.InnerException != null
                    ? ex.InnerException.InnerException.Message
                    : ex.Message;
                TempData["ErrorAdmin"] = "Error al generar vuelos: " + msg;
            }

            return RedirectToAction("Vuelos", new { fechaInicio = fechaInicio.ToString("yyyy-MM-dd"), fechaFin = fechaFin.ToString("yyyy-MM-dd") });
        }

        // POST: /Admin/CambiarEstadoVuelo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "SuperUsuario, Administrador")]
        public ActionResult CambiarEstadoVuelo(int vueloId, string nuevoEstado, int? aeronaveId)
        {
            var estadosPermitidos = new[] { "PROGRAMADO", "CONFIRMADO", "CANCELADO", "COMPLETADO" };
            var vuelo = db.Vueloes.Include(v => v.Frecuencia).FirstOrDefault(v => v.VueloId == vueloId);
            if (vuelo != null)
            {
                try
                {
                    if (!estadosPermitidos.Contains(nuevoEstado) || !db.EstadoVueloes.Any(e => e.Codigo == nuevoEstado))
                    {
                        TempData["ErrorAdmin"] = "Seleccione un estado de vuelo válido.";
                        return RedirectToAction("Vuelos");
                    }

                    vuelo.EstadoVueloCodigo = nuevoEstado;
                    if (aeronaveId.HasValue)
                    {
                        var aeronave = db.Aeronaves.Include(a => a.ModeloAeronave)
                            .FirstOrDefault(a => a.AeronaveId == aeronaveId.Value && a.EstadoAeronaveCodigo == "DISPONIBLE");
                        if (aeronave == null)
                        {
                            TempData["ErrorAdmin"] = "La aeronave seleccionada no está disponible.";
                            return RedirectToAction("Vuelos");
                        }

                        int asignacionesActivas = db.ReservaPasajeroVueloes.Count(a => a.VueloId == vueloId &&
                            (a.EstadoAsignacionCodigo == "PENDIENTE" || a.EstadoAsignacionCodigo == "CONFIRMADA"));
                        if (aeronave.ModeloAeronave.CapacidadAsientos < asignacionesActivas || aeronave.ModeloAeronave.CapacidadAsientos > 7)
                        {
                            TempData["ErrorAdmin"] = "La capacidad de la aeronave no cubre las plazas asignadas al vuelo.";
                            return RedirectToAction("Vuelos");
                        }

                        if (ExisteConflictoAeronave(vuelo, aeronaveId.Value))
                        {
                            TempData["ErrorAdmin"] = "La aeronave tiene otro vuelo incompatible; debe respetar al menos 45 minutos de preparación.";
                            return RedirectToAction("Vuelos");
                        }
                        vuelo.AeronaveId = aeronaveId.Value;
                    }
                    db.SaveChanges();
                    TempData["MensajeAdmin"] = "Vuelo #" + vueloId + " actualizado a estado '" + nuevoEstado + "'.";
                }
                catch (Exception ex)
                {
                    string msg = ex.InnerException != null && ex.InnerException.InnerException != null
                        ? ex.InnerException.InnerException.Message
                        : ex.Message;
                    TempData["ErrorAdmin"] = "Error al actualizar estado del vuelo: " + msg;
                }
            }
            return RedirectToAction("Vuelos");
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
            // Mantenemos los ViewBags originales por si se usan en otra parte
            ViewBag.TotalAeropuertos = db.Aeropuertoes.Count();
            ViewBag.TotalRutas = db.Rutas.Count();
            ViewBag.TotalFrecuencias = db.Frecuencias.Count();
            ViewBag.TotalAeronaves = db.Aeronaves.Count();
            ViewBag.TotalPasajeros = db.Pasajeroes.Count();
            ViewBag.TotalReservas = db.Reservas.Count();
            ViewBag.TotalCompras = db.Compras.Count();

            // Preparamos el ViewModel para la vista
            var model = new GestionAdminViewModel
            {
                TabActivo = tab,
                Buscar = buscar
            };

            if (tab == "rutas")
            {
                model.Rutas = db.Rutas
                    .Include(r => r.Aeropuerto)
                    .Include(r => r.Aeropuerto1)
                    .OrderBy(r => r.Aeropuerto.CodigoIATA)
                    .ToList();
                model.Frecuencias = db.Frecuencias
                    .Include(f => f.Ruta.Aeropuerto)
                    .Include(f => f.Ruta.Aeropuerto1)
                    .OrderBy(f => f.RutaId)
                    .ThenBy(f => f.DiaSemana)
                    .ToList();
                ViewBag.Aeropuertos = new SelectList(db.Aeropuertoes.Where(a => a.Activo).OrderBy(a => a.Localidad).ToList(), "AeropuertoId", "Nombre");
                ViewBag.RutasActivas = db.Rutas
                    .Include(r => r.Aeropuerto)
                    .Include(r => r.Aeropuerto1)
                    .Where(r => r.Activa)
                    .OrderBy(r => r.RutaId)
                    .ToList()
                    .Select(r => new SelectListItem
                    {
                        Value = r.RutaId.ToString(),
                        Text = r.Aeropuerto.CodigoIATA + " → " + r.Aeropuerto1.CodigoIATA
                    }).ToList();
                return View(model);
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearRuta(RutaAdminViewModel model)
        {
            bool origenValido = db.Aeropuertoes.Any(a => a.AeropuertoId == model.AeropuertoOrigenId && a.Activo);
            bool destinoValido = db.Aeropuertoes.Any(a => a.AeropuertoId == model.AeropuertoDestinoId && a.Activo);
            if (!ModelState.IsValid || !origenValido || !destinoValido || model.AeropuertoOrigenId == model.AeropuertoDestinoId)
            {
                TempData["ErrorAdmin"] = "Seleccione dos aeropuertos activos distintos e ingrese una tarifa válida.";
                return RedirectToAction("Gestion", new { tab = "rutas" });
            }

            bool duplicada = db.Rutas.Any(r => r.AeropuertoOrigenId == model.AeropuertoOrigenId && r.AeropuertoDestinoId == model.AeropuertoDestinoId);
            if (duplicada)
            {
                TempData["ErrorAdmin"] = "Ya existe una ruta con ese origen y destino.";
                return RedirectToAction("Gestion", new { tab = "rutas" });
            }

            db.Rutas.Add(new Ruta
            {
                AeropuertoOrigenId = model.AeropuertoOrigenId,
                AeropuertoDestinoId = model.AeropuertoDestinoId,
                TarifaReferencial = model.TarifaReferencial,
                Activa = true
            });
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Ruta creada. Ahora agregue sus frecuencias para poder generar vuelos.";
            return RedirectToAction("Gestion", new { tab = "rutas" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearFrecuencia(FrecuenciaAdminViewModel model)
        {
            bool rutaActiva = db.Rutas.Any(r => r.RutaId == model.RutaId && r.Activa);
            if (!ModelState.IsValid || !rutaActiva || model.DiaSemana < 1 || model.DiaSemana > 7 || model.HoraLlegada <= model.HoraSalida)
            {
                TempData["ErrorAdmin"] = "Seleccione una ruta activa y horarios válidos; la llegada debe ser posterior a la salida.";
                return RedirectToAction("Gestion", new { tab = "rutas" });
            }

            if (db.Frecuencias.Any(f => f.RutaId == model.RutaId && f.DiaSemana == model.DiaSemana))
            {
                TempData["ErrorAdmin"] = "La ruta ya tiene una frecuencia para ese día.";
                return RedirectToAction("Gestion", new { tab = "rutas" });
            }

            db.Frecuencias.Add(new Frecuencia
            {
                RutaId = model.RutaId,
                DiaSemana = model.DiaSemana,
                HoraSalida = model.HoraSalida,
                HoraLlegada = model.HoraLlegada,
                Activa = true
            });
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Frecuencia creada. Genere los vuelos desde Programación de Vuelos.";
            return RedirectToAction("Gestion", new { tab = "rutas" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstadoRuta(int rutaId, bool activa)
        {
            var ruta = db.Rutas.Find(rutaId);
            if (ruta == null)
            {
                return HttpNotFound();
            }
            ruta.Activa = activa;
            if (!activa)
            {
                foreach (var frecuencia in db.Frecuencias.Where(f => f.RutaId == rutaId))
                {
                    frecuencia.Activa = false;
                }
            }
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Estado de la ruta actualizado.";
            return RedirectToAction("Gestion", new { tab = "rutas" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ActualizarTarifaRuta(int rutaId, decimal tarifaReferencial)
        {
            if (tarifaReferencial <= 0 || tarifaReferencial > 10000)
            {
                TempData["ErrorAdmin"] = "La tarifa debe ser mayor que cero y no superar USD 10 000.";
                return RedirectToAction("Gestion", new { tab = "rutas" });
            }
            var ruta = db.Rutas.Find(rutaId);
            if (ruta == null)
            {
                return HttpNotFound();
            }
            ruta.TarifaReferencial = tarifaReferencial;
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Tarifa de ruta actualizada. Las reservas existentes conservan su importe.";
            return RedirectToAction("Gestion", new { tab = "rutas" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstadoFrecuencia(int frecuenciaId, bool activa)
        {
            var frecuencia = db.Frecuencias.Include(f => f.Ruta).FirstOrDefault(f => f.FrecuenciaId == frecuenciaId);
            if (frecuencia == null)
            {
                return HttpNotFound();
            }
            frecuencia.Activa = activa && frecuencia.Ruta.Activa;
            db.SaveChanges();
            TempData["MensajeAdmin"] = frecuencia.Activa ? "Frecuencia activada." : "Frecuencia desactivada.";
            return RedirectToAction("Gestion", new { tab = "rutas" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ActualizarFrecuencia(int frecuenciaId, byte diaSemana, TimeSpan horaSalida, TimeSpan horaLlegada)
        {
            var frecuencia = db.Frecuencias.Find(frecuenciaId);
            if (frecuencia == null)
            {
                return HttpNotFound();
            }
            bool duplicada = db.Frecuencias.Any(f => f.FrecuenciaId != frecuenciaId && f.RutaId == frecuencia.RutaId && f.DiaSemana == diaSemana);
            bool tieneVuelosFuturos = db.Vueloes.Any(v => v.FrecuenciaId == frecuenciaId && v.FechaOperacion >= DateTime.Today);
            if (diaSemana < 1 || diaSemana > 7 || horaLlegada <= horaSalida || duplicada || tieneVuelosFuturos)
            {
                TempData["ErrorAdmin"] = tieneVuelosFuturos
                    ? "No se puede cambiar una frecuencia con vuelos ya generados en el futuro. Desactívela y cree otra."
                    : "Revise el día y los horarios; no debe existir otra frecuencia para ese día.";
                return RedirectToAction("Gestion", new { tab = "rutas" });
            }
            frecuencia.DiaSemana = diaSemana;
            frecuencia.HoraSalida = horaSalida;
            frecuencia.HoraLlegada = horaLlegada;
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Frecuencia actualizada.";
            return RedirectToAction("Gestion", new { tab = "rutas" });
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
        public List<Ruta> Rutas { get; set; }
        public List<Frecuencia> Frecuencias { get; set; }
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

    public class RutaAdminViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un aeropuerto de origen.")]
        public int AeropuertoOrigenId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un aeropuerto de destino.")]
        public int AeropuertoDestinoId { get; set; }

        [Range(typeof(decimal), "0.01", "10000", ErrorMessage = "La tarifa debe ser mayor que cero y menor o igual a 10 000.")]
        public decimal TarifaReferencial { get; set; }
    }

    public class FrecuenciaAdminViewModel
    {
        [Range(1, int.MaxValue)]
        public int RutaId { get; set; }

        [Range(1, 7)]
        public byte DiaSemana { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan HoraSalida { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan HoraLlegada { get; set; }
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