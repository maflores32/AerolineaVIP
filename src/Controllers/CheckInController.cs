using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using AerolineaVIP.Models;
using Microsoft.AspNet.Identity;

namespace AerolineaVIP.Controllers
{
    [SessionAuthorize(Roles = "Cliente,Counter")]
    public class CheckInController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // ============================================================
        // PANTALLA 15: /CheckIn/Index?codigoReserva=VIP-...
        // ============================================================
        [HttpGet]
        public ActionResult Index(string codigoReserva, string pasajero)
        {
            if (User.IsInRole("Counter") && !string.IsNullOrWhiteSpace(pasajero))
            {
                string busqueda = pasajero.Trim();
                var pasajeros = db.Pasajeroes
                    .Where(p => p.NumeroDocumento.Contains(busqueda) || p.Nombres.Contains(busqueda) || p.Apellidos.Contains(busqueda))
                    .OrderBy(p => p.Apellidos)
                    .ThenBy(p => p.Nombres)
                    .Take(20)
                    .ToList();
                ViewBag.PasajerosEncontrados = pasajeros;
                var codigos = db.ReservaPasajeroVueloes
                    .Where(a => pasajeros.Select(p => p.PasajeroId).Contains(a.PasajeroId) &&
                                a.EstadoAsignacionCodigo == "CONFIRMADA" && a.Reserva.EstadoReservaCodigo == "CONFIRMADA")
                    .Select(a => a.Reserva.CodigoReserva)
                    .Distinct()
                    .ToList();
                ViewBag.ReservasPasajero = db.Reservas
                    .Where(r => codigos.Contains(r.CodigoReserva))
                    .OrderByDescending(r => r.FechaCreacion)
                    .ToList();
                ViewBag.BusquedaPasajero = busqueda;
                return View();
            }

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

            if (reserva == null || (!User.IsInRole("Counter") && reserva.UsuarioId != User.Identity.GetUserId()))
            {
                ViewBag.ErrorCheckIn = "No se encontró una reserva a su nombre con ese código.";
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

            if (vuelo == null)
            {
                ViewBag.ErrorCheckIn = "No se encontró el vuelo asociado a la reserva.";
                return View(reserva);
            }

            ViewBag.Vuelo = vuelo;
            ViewBag.CheckInAbierto = EstaDentroDeVentana(vuelo, out string mensajeVentana);
            ViewBag.MensajeVentana = mensajeVentana;
            return View(reserva);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarCheckIn(int asignacionId, string codigoReserva)
        {
            codigoReserva = (codigoReserva ?? "").Trim().ToUpperInvariant();
            using (var transaccion = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    var asignacion = db.ReservaPasajeroVueloes
                        .Include(a => a.CheckIns)
                        .Include(a => a.Reserva)
                        .FirstOrDefault(a => a.AsignacionId == asignacionId && a.Reserva.CodigoReserva == codigoReserva);

                    if (asignacion == null || (!User.IsInRole("Counter") && asignacion.Reserva.UsuarioId != User.Identity.GetUserId()))
                    {
                        transaccion.Rollback();
                        return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound);
                    }

                    if (asignacion.Reserva.EstadoReservaCodigo != "CONFIRMADA" || asignacion.EstadoAsignacionCodigo != "CONFIRMADA")
                    {
                        transaccion.Rollback();
                        TempData["ErrorCheckIn"] = "El pasajero debe tener una reserva pagada y un asiento confirmado.";
                        return RedirectToAction("Index", new { codigoReserva = codigoReserva });
                    }

                    var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == asignacion.VueloId);
                    string mensajeVentana = "No se encontró el vuelo.";
                    if (vuelo == null || !EstaDentroDeVentana(vuelo, out mensajeVentana))
                    {
                        transaccion.Rollback();
                        TempData["ErrorCheckIn"] = mensajeVentana;
                        return RedirectToAction("Index", new { codigoReserva = codigoReserva });
                    }

                    if (asignacion.CheckIns.Any(c => c.EstadoCheckInCodigo == "COMPLETADO"))
                    {
                        transaccion.Commit();
                        TempData["MensajeCheckIn"] = "El check-in de este pasajero ya estaba registrado.";
                        return RedirectToAction("Index", new { codigoReserva = codigoReserva });
                    }

                    db.CheckIns.Add(new CheckIn
                    {
                        AsignacionId = asignacionId,
                        FechaRegistro = DateTime.Now,
                        EstadoCheckInCodigo = "COMPLETADO"
                    });
                    db.SaveChanges();
                    transaccion.Commit();

                    TempData["MensajeCheckIn"] = "Check-in completado. Ya puede imprimir el pase de abordar.";
                }
                catch
                {
                    transaccion.Rollback();
                    TempData["ErrorCheckIn"] = "No se pudo completar el check-in. Actualice la página e inténtelo de nuevo.";
                }
            }

            return RedirectToAction("Index", new { codigoReserva = codigoReserva });
        }

        private static bool EstaDentroDeVentana(vw_VueloDetalle vuelo, out string mensaje)
        {
            DateTime salida = vuelo.FechaOperacion.Date.Add(vuelo.HoraSalida);
            DateTime ahora = DateTime.Now;
            DateTime apertura = salida.AddHours(-24);
            DateTime cierre = salida.AddHours(-1);

            if (ahora < apertura)
            {
                mensaje = "El check-in estará disponible 24 horas antes de la salida.";
                return false;
            }
            if (ahora >= cierre)
            {
                mensaje = "El check-in cerró 60 minutos antes de la salida.";
                return false;
            }

            mensaje = "Check-in disponible hasta 60 minutos antes de la salida.";
            return true;
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