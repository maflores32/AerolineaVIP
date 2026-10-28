using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using AerolineaVIP.Models;
using Microsoft.AspNet.Identity;

namespace AerolineaVIP.Controllers
{
    [Authorize]
    public class CheckInController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // ============================================================
        // PANTALLA 15: /CheckIn/Index?codigoReserva=VIP-...
        // ============================================================
        [HttpGet]
        public ActionResult Index(string codigoReserva)
        {
            if (string.IsNullOrEmpty(codigoReserva))
            {
                return View();
            }

            codigoReserva = codigoReserva.Trim().ToUpper();
            var reserva = db.Reservas
                .Include(r => r.EstadoReserva)
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.Pasajero))
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.CheckIns))
                .FirstOrDefault(r => r.CodigoReserva == codigoReserva);

            if (reserva == null)
            {
                ViewBag.ErrorCheckIn = "No se encontró ninguna reserva con el código '" + codigoReserva + "'.";
                return View();
            }

            // Regla del trigger trg_CheckIn_Validar: La reserva debe estar pagada/confirmada
            if (reserva.EstadoReservaCodigo != "CONFIRMADA")
            {
                ViewBag.ErrorCheckIn = "La reserva se encuentra en estado '" + reserva.EstadoReservaCodigo + "'. El check-in solo está disponible para reservas con pago confirmado.";
                return View(reserva);
            }

            var primerTramo = reserva.ReservaPasajeroVueloes.FirstOrDefault();
            vw_VueloDetalle vuelo = null;
            if (primerTramo != null)
            {
                vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == primerTramo.VueloId);
            }

            ViewBag.Vuelo = vuelo;
            return View(reserva);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarCheckIn(int asignacionId, string codigoReserva)
        {
            var asignacion = db.ReservaPasajeroVueloes
                .Include(a => a.CheckIns)
                .FirstOrDefault(a => a.AsignacionId == asignacionId);

            if (asignacion == null)
            {
                return RedirectToAction("Index");
            }

            // Verificar si ya tiene check-in completado
            bool yaRegistrado = asignacion.CheckIns.Any(c => c.EstadoCheckInCodigo == "COMPLETADO");
            if (!yaRegistrado)
            {
                var nuevoCheckIn = new CheckIn
                {
                    AsignacionId = asignacionId,
                    FechaRegistro = DateTime.Now,
                    EstadoCheckInCodigo = "COMPLETADO"
                };
                db.CheckIns.Add(nuevoCheckIn);
                db.SaveChanges();

                TempData["MensajeCheckIn"] = "¡Check-in completado con éxito! Se ha generado su pase de abordar.";
            }

            return RedirectToAction("Index", new { codigoReserva = codigoReserva });
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