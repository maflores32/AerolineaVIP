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
    public class ReservasController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // ============================================================
        // PANTALLA 8: /Reservas/SeleccionAsiento
        // ============================================================
        [HttpGet]
        public ActionResult SeleccionAsiento(int? vueloId, int? pasajeros)
        {
            if (!vueloId.HasValue)
            {
                return RedirectToAction("Buscar", "Vuelos");
            }

            int numPasajeros = pasajeros ?? 1;
            if (numPasajeros < 1) numPasajeros = 1;
            if (numPasajeros > 7) numPasajeros = 7;

            var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == vueloId.Value);
            if (vuelo == null)
            {
                return HttpNotFound();
            }

            var asientos = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == vueloId.Value)
                .OrderBy(a => a.Fila)
                .ThenBy(a => a.Lado)
                .ToList();

            ViewBag.Asientos = asientos;
            ViewBag.Pasajeros = numPasajeros;

            return View(vuelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SeleccionAsiento(int vueloId, int pasajeros, int[] asientosSeleccionados)
        {
            if (asientosSeleccionados == null || asientosSeleccionados.Length != pasajeros)
            {
                TempData["ErrorAsientos"] = "Debe seleccionar exactamente " + pasajeros + " asiento(s) disponible(s).";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = pasajeros });
            }

            string asientosCsv = string.Join(",", asientosSeleccionados);
            return RedirectToAction("Pasajeros", new { vueloId = vueloId, asientos = asientosCsv });
        }

        // ============================================================
        // PANTALLA 9: /Reservas/Pasajeros
        // ============================================================
        [HttpGet]
        public ActionResult Pasajeros(int? vueloId, string asientos)
        {
            if (!vueloId.HasValue || string.IsNullOrEmpty(asientos))
            {
                return RedirectToAction("Buscar", "Vuelos");
            }

            var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == vueloId.Value);
            if (vuelo == null)
            {
                return HttpNotFound();
            }

            var numerosAsiento = asientos.Split(',')
                .Select(s => byte.Parse(s.Trim()))
                .ToList();

            var infoAsientos = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == vueloId.Value && numerosAsiento.Contains(a.NumeroAsiento))
                .OrderBy(a => a.NumeroAsiento)
                .ToList();

            ViewBag.Vuelo = vuelo;
            ViewBag.InfoAsientos = infoAsientos;
            ViewBag.AsientosCsv = asientos;
            ViewBag.TiposDocumento = db.TipoDocumentoIdentidads.OrderBy(t => t.Codigo).ToList();

            return View();
        }

        // POST: /Reservas/GuardarReserva
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuardarReserva(
            int vueloId,
            int[] numeroAsiento,
            string[] tipoDocumento,
            string[] numeroDocumento,
            string[] nombres,
            string[] apellidos,
            DateTime?[] fechaNacimiento,
            string[] correo,
            string[] telefono)
        {
            var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == vueloId);
            if (vuelo == null || numeroAsiento == null || numeroAsiento.Length == 0)
            {
                return RedirectToAction("Buscar", "Vuelos");
            }

            // Validar estado del vuelo
            if (vuelo.EstadoVueloCodigo != "PROGRAMADO" && vuelo.EstadoVueloCodigo != "CONFIRMADO")
            {
                TempData["ErrorAsientos"] = "Este vuelo no permite nuevas reservas porque se encuentra en estado " + vuelo.EstadoVueloCodigo + ".";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = numeroAsiento.Length });
            }

            var listaAsientosBytes = numeroAsiento.Select(n => (byte)n).ToList();

            // Verificar si algún asiento ya fue reservado o comprado
            var ocupados = db.ReservaPasajeroVueloes
                .Where(r => r.VueloId == vueloId
                         && listaAsientosBytes.Contains(r.NumeroAsiento)
                         && (r.EstadoAsignacionCodigo == "PENDIENTE" || r.EstadoAsignacionCodigo == "CONFIRMADA"))
                .Any();

            if (ocupados)
            {
                TempData["ErrorAsientos"] = "Uno o más de los asientos seleccionados ya no están disponibles. Por favor elija otros.";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = numeroAsiento.Length });
            }

            // TRANSACCIÓN ATÓMICA DE BASE DE DATOS (Si un asiento falla, se revierte toda la reserva)
            using (var transaccion = db.Database.BeginTransaction())
            {
                try
                {
                    string codigoGenerado = "VIP-" + DateTime.Now.ToString("HHmmss");
                    while (db.Reservas.Any(r => r.CodigoReserva == codigoGenerado))
                    {
                        codigoGenerado = "VIP-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
                    }

                    var nuevaReserva = new Reserva
                    {
                        CodigoReserva = codigoGenerado,
                        UsuarioId = User.Identity.GetUserId(),
                        FechaCreacion = DateTime.Now,
                        FechaExpiracion = DateTime.Now.AddHours(24),
                        EstadoReservaCodigo = "PENDIENTE"
                    };

                    db.Reservas.Add(nuevaReserva);
                    db.SaveChanges(); // Guarda la reserva para obtener su ID autoincremental

                    for (int i = 0; i < numeroAsiento.Length; i++)
                    {
                        string tDoc = (tipoDocumento != null && i < tipoDocumento.Length) ? tipoDocumento[i] : "CEDULA";
                        string nDoc = (numeroDocumento != null && i < numeroDocumento.Length) ? (numeroDocumento[i] ?? "").Trim() : "";
                        string nom = (nombres != null && i < nombres.Length) ? (nombres[i] ?? "").Trim() : "";
                        string ape = (apellidos != null && i < apellidos.Length) ? (apellidos[i] ?? "").Trim() : "";
                        DateTime? fNac = (fechaNacimiento != null && i < fechaNacimiento.Length) ? fechaNacimiento[i] : null;
                        string mail = (correo != null && i < correo.Length) ? (correo[i] ?? "").Trim() : "";
                        string tel = (telefono != null && i < telefono.Length) ? (telefono[i] ?? "").Trim() : "";

                        // Validación básica de campos del pasajero
                        if (string.IsNullOrEmpty(nDoc) || string.IsNullOrEmpty(nom) || string.IsNullOrEmpty(ape))
                        {
                            throw new Exception("Debe completar los nombres, apellidos y número de documento de todos los pasajeros.");
                        }

                        var pasajero = db.Pasajeroes.FirstOrDefault(p => p.TipoDocumentoCodigo == tDoc && p.NumeroDocumento == nDoc);
                        if (pasajero == null)
                        {
                            pasajero = new Pasajero
                            {
                                TipoDocumentoCodigo = tDoc,
                                NumeroDocumento = nDoc,
                                Nombres = nom,
                                Apellidos = ape,
                                FechaNacimiento = fNac,
                                Correo = mail,
                                Telefono = tel,
                                FechaRegistro = DateTime.Now
                            };
                            db.Pasajeroes.Add(pasajero);
                            db.SaveChanges();
                        }
                        else
                        {
                            pasajero.Nombres = nom;
                            pasajero.Apellidos = ape;
                            if (fNac.HasValue) pasajero.FechaNacimiento = fNac;
                            if (!string.IsNullOrEmpty(mail)) pasajero.Correo = mail;
                            if (!string.IsNullOrEmpty(tel)) pasajero.Telefono = tel;
                            db.SaveChanges();
                        }

                        var asignacion = new ReservaPasajeroVuelo
                        {
                            ReservaId = nuevaReserva.ReservaId,
                            PasajeroId = pasajero.PasajeroId,
                            VueloId = vueloId,
                            NumeroAsiento = (byte)numeroAsiento[i],
                            ImporteTramo = vuelo.TarifaReferencial,
                            EstadoAsignacionCodigo = "PENDIENTE",
                            FechaAsignacion = DateTime.Now
                        };

                        db.ReservaPasajeroVueloes.Add(asignacion);
                    }

                    db.SaveChanges(); // Guarda todas las asignaciones
                    transaccion.Commit(); // CONFIRMAR TRANSACCIÓN COMPLETA

                    return RedirectToAction("Confirmar", new { id = nuevaReserva.ReservaId });
                }
                catch (Exception ex)
                {
                    transaccion.Rollback(); // REVERTIR TODO EN CASO DE ERROR

                    string errorAmigable = ex.Message;
                    if (ex.InnerException != null && ex.InnerException.InnerException != null)
                    {
                        errorAmigable = ex.InnerException.InnerException.Message;
                    }

                    TempData["ErrorAsientos"] = "No se pudo completar la reserva: " + errorAmigable;
                    return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = numeroAsiento.Length });
                }
            }
        }

        // ============================================================
        // PANTALLA 10: /Reservas/Confirmar/{id}
        // ============================================================

        // GET: /Reservas/Confirmar/5
        [HttpGet]
        public ActionResult Confirmar(int? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("MisReservas");
            }

            var reserva = db.Reservas
                .Include(r => r.EstadoReserva)
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.Pasajero))
                .FirstOrDefault(r => r.ReservaId == id.Value);

            if (reserva == null)
            {
                return HttpNotFound();
            }

            // VALIDACIÓN DE TITULARIDAD: Solo el dueño o el personal puede ver la confirmación
            string userId = User.Identity.GetUserId();
            bool esPersonal = User.IsInRole("SuperUsuario") || User.IsInRole("Administrador") || User.IsInRole("Counter");
            if (!esPersonal && reserva.UsuarioId != userId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "No tiene permisos para ver esta reserva.");
            }

            var primerTramo = reserva.ReservaPasajeroVueloes.FirstOrDefault();
            vw_VueloDetalle vueloDetalle = null;
            if (primerTramo != null)
            {
                vueloDetalle = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == primerTramo.VueloId);
            }

            ViewBag.Vuelo = vueloDetalle;
            return View(reserva);
        }

        // ============================================================
        // PANTALLA 13: /Reservas/MisReservas
        // ============================================================
        // GET: /Reservas/MisReservas
        [HttpGet]
        public ActionResult MisReservas(string estado)
        {
            string userId = User.Identity.GetUserId();
            bool esPersonal = User.IsInRole("SuperUsuario") || User.IsInRole("Administrador") || User.IsInRole("Counter");

            var query = db.Reservas
                .Include(r => r.EstadoReserva)
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.Pasajero))
                .Include(r => r.Compras)
                .AsQueryable();

            // Consulta estándar limpia: si es cliente, filtra únicamente por su UsuarioId real
            if (!esPersonal)
            {
                query = query.Where(r => r.UsuarioId == userId);
            }

            if (!string.IsNullOrEmpty(estado))
            {
                query = query.Where(r => r.EstadoReservaCodigo == estado);
            }

            var lista = query.OrderByDescending(r => r.FechaCreacion).ToList();

            ViewBag.VuelosDict = db.vw_VueloDetalle.ToDictionary(v => v.VueloId, v => v);
            ViewBag.EstadoSeleccionado = estado;

            return View(lista);
        }

        // ============================================================
        // PANTALLA 14: /Reservas/Gestionar/{id}
        // ============================================================
        // GET: /Reservas/Gestionar/5
        [HttpGet]
        public ActionResult Gestionar(int? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("MisReservas");
            }

            var reserva = db.Reservas
                .Include(r => r.EstadoReserva)
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.Pasajero))
                .Include(r => r.Compras)
                .FirstOrDefault(r => r.ReservaId == id.Value);

            if (reserva == null)
            {
                return HttpNotFound();
            }

            // VALIDACIÓN DE TITULARIDAD
            string userId = User.Identity.GetUserId();
            bool esPersonal = User.IsInRole("SuperUsuario") || User.IsInRole("Administrador") || User.IsInRole("Counter");
            if (!esPersonal && reserva.UsuarioId != userId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "No tiene permisos para gestionar esta reserva.");
            }

            var primerTramo = reserva.ReservaPasajeroVueloes.FirstOrDefault();
            vw_VueloDetalle vuelo = null;
            var asientosDisponibles = new List<vw_EstadoAsientoVuelo>();

            if (primerTramo != null)
            {
                vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == primerTramo.VueloId);
                asientosDisponibles = db.vw_EstadoAsientoVuelo
                    .Where(a => a.VueloId == primerTramo.VueloId && a.EstadoVisual == "DISPONIBLE")
                    .OrderBy(a => a.NumeroAsiento)
                    .ToList();
            }

            ViewBag.Vuelo = vuelo;
            ViewBag.AsientosDisponibles = asientosDisponibles;

            return View(reserva);
        }

        // POST: /Reservas/CambiarAsiento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarAsiento(int asignacionId, byte nuevoAsiento)
        {
            var asignacion = db.ReservaPasajeroVueloes
                .Include(a => a.Reserva)
                .FirstOrDefault(a => a.AsignacionId == asignacionId);

            if (asignacion == null)
            {
                return RedirectToAction("MisReservas");
            }

            // VALIDACIÓN DE TITULARIDAD
            string userId = User.Identity.GetUserId();
            bool esPersonal = User.IsInRole("SuperUsuario") || User.IsInRole("Administrador") || User.IsInRole("Counter");
            if (!esPersonal && asignacion.Reserva.UsuarioId != userId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }

            bool ocupado = db.ReservaPasajeroVueloes.Any(a =>
                a.VueloId == asignacion.VueloId &&
                a.NumeroAsiento == nuevoAsiento &&
                (a.EstadoAsignacionCodigo == "PENDIENTE" || a.EstadoAsignacionCodigo == "CONFIRMADA"));

            if (ocupado)
            {
                TempData["MensajeGestion"] = "El asiento seleccionado ya no está disponible.";
            }
            else
            {
                asignacion.NumeroAsiento = nuevoAsiento;
                db.SaveChanges();
                TempData["MensajeGestion"] = "Asiento actualizado correctamente al asiento #" + nuevoAsiento + ".";
            }

            return RedirectToAction("Gestionar", new { id = asignacion.ReservaId });
        }

        // POST: /Reservas/CancelarReserva
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelarReserva(int reservaId)
        {
            var reserva = db.Reservas
                .Include(r => r.ReservaPasajeroVueloes)
                .FirstOrDefault(r => r.ReservaId == reservaId);

            if (reserva != null && reserva.EstadoReservaCodigo != "CANCELADA")
            {
                // VALIDACIÓN DE TITULARIDAD
                string userId = User.Identity.GetUserId();
                bool esPersonal = User.IsInRole("SuperUsuario") || User.IsInRole("Administrador") || User.IsInRole("Counter");
                if (!esPersonal && reserva.UsuarioId != userId)
                {
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
                }

                reserva.EstadoReservaCodigo = "CANCELADA";
                foreach (var asig in reserva.ReservaPasajeroVueloes)
                {
                    asig.EstadoAsignacionCodigo = "CANCELADA";
                }
                db.SaveChanges();
                TempData["MensajeGestion"] = "La reserva " + reserva.CodigoReserva + " ha sido cancelada y sus asientos fueron liberados.";
            }

            return RedirectToAction("Gestionar", new { id = reservaId });
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