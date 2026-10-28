using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using AerolineaVIP.Models;

namespace AerolineaVIP.Controllers
{
    [Authorize(Roles = "SuperUsuario, Administrador, Counter")]
    public class AdminController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // ============================================================
        // PANTALLA 17: PANEL DE ADMINISTRACIÓN
        // ============================================================
        // GET: /Admin
        // GET: /Admin/Index
        public ActionResult Index()
        {
            return View();
        }

        // ============================================================
        // PANTALLA 18: PROGRAMACIÓN OPERATIVA DE VUELOS
        // ============================================================
        // GET: /Admin/Vuelos
        public ActionResult Vuelos(DateTime? fechaInicio, DateTime? fechaFin, string estado)
        {
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
            ViewBag.Aeronaves = new SelectList(db.Aeronaves.Where(a => a.EstadoAeronaveCodigo == "DISPONIBLE").ToList(), "AeronaveId", "Matricula");
            ViewBag.FechaInicio = fechaInicio.HasValue ? fechaInicio.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.FechaFin = fechaFin.HasValue ? fechaFin.Value.ToString("yyyy-MM-dd") : "";

            return View(lista);
        }

        // POST: /Admin/GenerarVuelos (Invoca el Stored Procedure dbo.usp_GenerarVuelos)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperUsuario, Administrador")]
        public ActionResult GenerarVuelos(DateTime fechaInicio, DateTime fechaFin)
        {
            if (fechaInicio > fechaFin)
            {
                TempData["ErrorAdmin"] = "La fecha de inicio no puede ser mayor a la fecha de fin.";
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
        [Authorize(Roles = "SuperUsuario, Administrador")]
        public ActionResult CambiarEstadoVuelo(int vueloId, string nuevoEstado, int? aeronaveId)
        {
            var vuelo = db.Vueloes.FirstOrDefault(v => v.VueloId == vueloId);
            if (vuelo != null)
            {
                try
                {
                    vuelo.EstadoVueloCodigo = nuevoEstado;
                    if (aeronaveId.HasValue)
                    {
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

        // POST: /Admin/CambiarEstadoAeronave
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperUsuario, Administrador")]
        public ActionResult CambiarEstadoAeronave(int aeronaveId, string nuevoEstado)
        {
            var aeronave = db.Aeronaves.FirstOrDefault(a => a.AeronaveId == aeronaveId);
            if (aeronave != null)
            {
                try
                {
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
        public ActionResult Gestion(string tab = "usuarios", string buscar = "")
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
                // Pestaña por defecto: Usuarios (Usamos Pasajeros como ejemplo de usuarios del sistema)
                // NOTA: Si tienes una tabla de Identity mapeada (ej. AspNetUsers), haz la consulta ahí.
                var queryUsuarios = db.Pasajeroes.AsQueryable();
                if (!string.IsNullOrEmpty(buscar))
                {
                    queryUsuarios = queryUsuarios.Where(p => p.Nombres.Contains(buscar) || p.NumeroDocumento.Contains(buscar));
                }
                model.Usuarios = queryUsuarios.OrderBy(p => p.Nombres).Take(50).ToList();
            }

            return View(model);
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
        public List<Pasajero> Pasajeros { get; set; }
        public List<Reserva> Reservas { get; set; }
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