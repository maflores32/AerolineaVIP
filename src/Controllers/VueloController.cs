using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Data.SqlClient;
using System.Web;
using System.Web.Mvc;
using AerolineaVIP.Models;

namespace AerolineaVIP.Controllers
{
    [SessionAuthorize(Roles = "SuperUsuario, Administrador")]
    public class VueloController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

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

        [HttpGet]
        public ActionResult Programacion(DateTime? fechaInicio, DateTime? fechaFin, string estado)
        {
            if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio.Value.Date > fechaFin.Value.Date)
            {
                TempData["ErrorAdmin"] = "La fecha inicial debe ser anterior o igual a la fecha final.";
                return RedirectToAction("Programacion");
            }

            var query = db.vw_VueloDetalle.AsQueryable();
            if (fechaInicio.HasValue)
            {
                DateTime inicio = fechaInicio.Value.Date;
                query = query.Where(v => DbFunctions.TruncateTime(v.FechaOperacion) >= inicio);
            }
            if (fechaFin.HasValue)
            {
                DateTime fin = fechaFin.Value.Date;
                query = query.Where(v => DbFunctions.TruncateTime(v.FechaOperacion) <= fin);
            }
            if (!string.IsNullOrWhiteSpace(estado))
            {
                query = query.Where(v => v.EstadoVueloCodigo == estado);
            }

            var vuelos = query.OrderByDescending(v => v.FechaOperacion).ThenBy(v => v.HoraSalida).ToList();
            ViewBag.Estados = new SelectList(db.EstadoVueloes.ToList(), "Codigo", "Descripcion", estado);
            ViewBag.Aeronaves = new SelectList(db.Aeronaves
                .Where(a => a.EstadoAeronaveCodigo == "DISPONIBLE")
                .OrderBy(a => a.Matricula)
                .ToList(), "AeronaveId", "Matricula");
            ViewBag.FechaInicio = fechaInicio.HasValue ? fechaInicio.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.FechaFin = fechaFin.HasValue ? fechaFin.Value.ToString("yyyy-MM-dd") : "";
            return View("~/Views/Admin/Vuelos.cshtml", vuelos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerarVuelos(DateTime fechaInicio, DateTime fechaFin)
        {
            if (fechaInicio.Date < DateTime.Today || fechaInicio.Date > fechaFin.Date || fechaFin.Date > DateTime.Today.AddDays(365))
            {
                TempData["ErrorAdmin"] = "Use fechas desde hoy y un rango máximo de un año.";
                return RedirectToAction("Programacion");
            }

            try
            {
                db.Database.ExecuteSqlCommand(
                    "EXEC dbo.usp_GenerarVuelos @FechaInicio, @FechaFin",
                    new SqlParameter("@FechaInicio", fechaInicio.Date),
                    new SqlParameter("@FechaFin", fechaFin.Date));
                TempData["MensajeAdmin"] = "Los vuelos se generaron correctamente para el rango indicado.";
            }
            catch
            {
                TempData["ErrorAdmin"] = "No se pudieron generar los vuelos. Verifique que existan frecuencias activas y que la base de datos esté disponible.";
            }

            return RedirectToAction("Programacion", new
            {
                fechaInicio = fechaInicio.ToString("yyyy-MM-dd"),
                fechaFin = fechaFin.ToString("yyyy-MM-dd")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstadoVuelo(int vueloId, string nuevoEstado, int? aeronaveId)
        {
            var estadosPermitidos = new[] { "PROGRAMADO", "CONFIRMADO", "CANCELADO", "COMPLETADO" };
            var vuelo = db.Vueloes.Include(v => v.Frecuencia).FirstOrDefault(v => v.VueloId == vueloId);
            if (vuelo == null)
            {
                return HttpNotFound();
            }
            if (!estadosPermitidos.Contains(nuevoEstado) || !db.EstadoVueloes.Any(e => e.Codigo == nuevoEstado))
            {
                TempData["ErrorAdmin"] = "Seleccione un estado de vuelo válido.";
                return RedirectToAction("Programacion");
            }

            int? aeronaveAsignada = aeronaveId.HasValue ? aeronaveId : vuelo.AeronaveId;
            if (aeronaveAsignada.HasValue)
            {
                var aeronave = db.Aeronaves.Include(a => a.ModeloAeronave)
                    .FirstOrDefault(a => a.AeronaveId == aeronaveAsignada.Value && a.EstadoAeronaveCodigo == "DISPONIBLE");
                if (aeronave == null)
                {
                    TempData["ErrorAdmin"] = "La aeronave seleccionada no está disponible.";
                    return RedirectToAction("Programacion");
                }
                int asignacionesActivas = db.ReservaPasajeroVueloes.Count(a => a.VueloId == vueloId &&
                    (a.EstadoAsignacionCodigo == "PENDIENTE" || a.EstadoAsignacionCodigo == "CONFIRMADA"));
                if (aeronave.ModeloAeronave.CapacidadAsientos < asignacionesActivas || aeronave.ModeloAeronave.CapacidadAsientos > 7 ||
                    ExisteConflictoAeronave(vuelo, aeronaveAsignada.Value))
                {
                    TempData["ErrorAdmin"] = "La aeronave no tiene capacidad suficiente o tiene otro vuelo dentro del margen de preparación de 45 minutos.";
                    return RedirectToAction("Programacion");
                }
            }

            vuelo.EstadoVueloCodigo = nuevoEstado;
            if (aeronaveId.HasValue)
            {
                vuelo.AeronaveId = aeronaveId.Value;
            }
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Vuelo actualizado correctamente.";
            return RedirectToAction("Programacion");
        }

        // GET: Vuelo
        public ActionResult Index()
        {
            var vueloes = db.Vueloes.Include(v => v.Aeronave).Include(v => v.EstadoVuelo).Include(v => v.Frecuencia)
                .OrderByDescending(v => v.FechaOperacion);
            return View(vueloes.ToList());
        }

        // GET: Vuelo/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Vuelo vuelo = db.Vueloes.Include(v => v.Aeronave).Include(v => v.EstadoVuelo).Include(v => v.Frecuencia).FirstOrDefault(v => v.VueloId == id.Value);
            if (vuelo == null)
            {
                return HttpNotFound();
            }
            return View(vuelo);
        }

        // GET: Vuelo/Create
        public ActionResult Create()
        {
            return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Los vuelos se generan desde las frecuencias activas.");
        }

        // POST: Vuelo/Create
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que quiere enlazarse. Para obtener 
        // más detalles, vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "VueloId,FrecuenciaId,FechaOperacion,AeronaveId,EstadoVueloCodigo")] Vuelo vuelo)
        {
            return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Los vuelos se generan desde las frecuencias activas.");
        }

        // GET: Vuelo/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Vuelo vuelo = db.Vueloes.Find(id);
            if (vuelo == null)
            {
                return HttpNotFound();
            }
            ViewBag.AeronaveId = new SelectList(db.Aeronaves
                .Where(a => a.EstadoAeronaveCodigo == "DISPONIBLE" || a.AeronaveId == vuelo.AeronaveId)
                .OrderBy(a => a.Matricula)
                .ToList(), "AeronaveId", "Matricula", vuelo.AeronaveId);
            ViewBag.EstadoVueloCodigo = new SelectList(db.EstadoVueloes, "Codigo", "Descripcion", vuelo.EstadoVueloCodigo);
            ViewBag.FrecuenciaId = new SelectList(db.Frecuencias, "FrecuenciaId", "FrecuenciaId", vuelo.FrecuenciaId);
            return View(vuelo);
        }

        // POST: Vuelo/Edit/5
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que quiere enlazarse. Para obtener 
        // más detalles, vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "VueloId,FrecuenciaId,FechaOperacion,AeronaveId,EstadoVueloCodigo")] Vuelo vuelo)
        {
            if (vuelo == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var actual = db.Vueloes.Include(v => v.Frecuencia).FirstOrDefault(v => v.VueloId == vuelo.VueloId);
            if (actual == null)
            {
                return HttpNotFound();
            }
            var estadosPermitidos = new[] { "PROGRAMADO", "CONFIRMADO", "CANCELADO", "COMPLETADO" };
            var aeronave = db.Aeronaves.Include(a => a.ModeloAeronave)
                .FirstOrDefault(a => a.AeronaveId == vuelo.AeronaveId && a.EstadoAeronaveCodigo == "DISPONIBLE");
            int asignacionesActivas = db.ReservaPasajeroVueloes.Count(a => a.VueloId == vuelo.VueloId &&
                (a.EstadoAsignacionCodigo == "PENDIENTE" || a.EstadoAsignacionCodigo == "CONFIRMADA"));
            if (!estadosPermitidos.Contains(vuelo.EstadoVueloCodigo) ||
                !db.EstadoVueloes.Any(e => e.Codigo == vuelo.EstadoVueloCodigo) || aeronave == null ||
                aeronave.ModeloAeronave.CapacidadAsientos < asignacionesActivas ||
                aeronave.ModeloAeronave.CapacidadAsientos > 7 || !vuelo.AeronaveId.HasValue ||
                ExisteConflictoAeronave(actual, vuelo.AeronaveId.Value))
            {
                ModelState.AddModelError("", "Seleccione un estado válido y una aeronave disponible, con capacidad suficiente y sin conflicto de horario.");
                ViewBag.AeronaveId = new SelectList(db.Aeronaves
                    .Where(a => a.EstadoAeronaveCodigo == "DISPONIBLE" || a.AeronaveId == vuelo.AeronaveId)
                    .ToList(), "AeronaveId", "Matricula", vuelo.AeronaveId);
                ViewBag.EstadoVueloCodigo = new SelectList(db.EstadoVueloes.ToList(), "Codigo", "Descripcion", vuelo.EstadoVueloCodigo);
                ViewBag.FrecuenciaId = new SelectList(db.Frecuencias.ToList(), "FrecuenciaId", "FrecuenciaId", actual.FrecuenciaId);
                return View(actual);
            }

            actual.AeronaveId = vuelo.AeronaveId;
            actual.EstadoVueloCodigo = vuelo.EstadoVueloCodigo;
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Vuelo actualizado correctamente.";
            return RedirectToAction("Index");
        }

        // GET: Vuelo/Delete/5
        public ActionResult Delete(int? id)
        {
            return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "No se eliminan vuelos individualmente. Si procede, cambie su estado a CANCELADO.");
        }

        // POST: Vuelo/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "No se eliminan vuelos individualmente. Si procede, cambie su estado a CANCELADO.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
