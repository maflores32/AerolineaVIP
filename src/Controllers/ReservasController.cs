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
    public class ReservasController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        private bool EsCliente
        {
            get { return User.IsInRole("Cliente"); }
        }

        // ============================================================
        // PANTALLA 8: /Reservas/SeleccionAsiento
        // ============================================================
        [HttpGet]
        [SessionAuthorize(Roles = "Cliente")]
        public ActionResult SeleccionAsiento(int? vueloId, int? pasajeros)
        {
            if (!vueloId.HasValue)
            {
                return RedirectToAction("Buscar", "Vuelos");
            }

            int numPasajeros = pasajeros ?? 1;
            if (numPasajeros < 1 || numPasajeros > 7)
            {
                TempData["ErrorAsientos"] = "Seleccione entre 1 y 7 pasajeros.";
                return RedirectToAction("Buscar", "Vuelos");
            }

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
        [SessionAuthorize(Roles = "Cliente")]
        public ActionResult SeleccionAsiento(int vueloId, int pasajeros, int[] asientosSeleccionados)
        {
            if (pasajeros < 1 || pasajeros > 7 || asientosSeleccionados == null || asientosSeleccionados.Length != pasajeros ||
                asientosSeleccionados.Distinct().Count() != asientosSeleccionados.Length ||
                asientosSeleccionados.Any(a => a < 1 || a > 7))
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
        [SessionAuthorize(Roles = "Cliente")]
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

            var numerosAsiento = new List<byte>();
            foreach (string valor in asientos.Split(','))
            {
                byte numero;
                if (!byte.TryParse(valor.Trim(), out numero) || numero < 1 || numero > 7 || numerosAsiento.Contains(numero))
                {
                    TempData["ErrorAsientos"] = "La selección de asientos no es válida. Vuelva a elegirlos.";
                    return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId.Value });
                }
                numerosAsiento.Add(numero);
            }

            var infoAsientos = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == vueloId.Value && numerosAsiento.Contains(a.NumeroAsiento))
                .OrderBy(a => a.NumeroAsiento)
                .ToList();

            if (infoAsientos.Count != numerosAsiento.Count || infoAsientos.Any(a => a.EstadoVisual != "DISPONIBLE"))
            {
                TempData["ErrorAsientos"] = "Uno o más asientos ya no están disponibles. Vuelva a elegirlos.";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId.Value, pasajeros = numerosAsiento.Count });
            }

            var modelo = new RegistroPasajerosViewModel
            {
                VueloId = vueloId.Value,
                Vuelo = vuelo,
                Pasajeros = infoAsientos.Select(a => new PasajeroReservaViewModel
                {
                    NumeroAsiento = a.NumeroAsiento,
                    CodigoAsiento = a.CodigoAsiento,
                    Ubicacion = a.Ubicacion
                }).ToList()
            };

            PrepararVistaPasajeros(modelo, vuelo);
            return View(modelo);
        }

        // POST: /Reservas/GuardarReserva
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "Cliente")]
        public ActionResult GuardarReserva(RegistroPasajerosViewModel modelo)
        {
            if (modelo == null || modelo.VueloId <= 0 || modelo.Pasajeros == null ||
                modelo.Pasajeros.Count < 1 || modelo.Pasajeros.Count > 7 || modelo.Pasajeros.Any(p => p == null))
            {
                TempData["ErrorAsientos"] = "Revise los datos de todos los pasajeros e inténtelo de nuevo.";
                return RedirectToAction("Buscar", "Vuelos");
            }

            int vueloId = modelo.VueloId;
            var pasajeros = modelo.Pasajeros;
            int cantidadPasajeros = pasajeros.Count;
            int[] numeroAsiento = pasajeros.Select(p => p.NumeroAsiento).ToArray();
            string[] tipoDocumento = pasajeros.Select(p => p.TipoDocumento).ToArray();
            string[] numeroDocumento = pasajeros.Select(p => p.NumeroDocumento).ToArray();
            string[] nombres = pasajeros.Select(p => p.Nombres).ToArray();
            string[] apellidos = pasajeros.Select(p => p.Apellidos).ToArray();
            DateTime?[] fechaNacimiento = pasajeros.Select(p => p.FechaNacimiento).ToArray();
            string[] correo = pasajeros.Select(p => p.Correo).ToArray();
            string[] telefono = pasajeros.Select(p => p.Telefono).ToArray();

            var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == vueloId);
            if (vuelo == null)
            {
                return HttpNotFound();
            }

            DateTime salida = vuelo.FechaOperacion.Date.Add(vuelo.HoraSalida);
            if (salida <= DateTime.Now)
            {
                TempData["ErrorAsientos"] = "No se pueden reservar vuelos que ya salieron.";
                return RedirectToAction("Buscar", "Vuelos");
            }

            if (!ModelState.IsValid)
            {
                PrepararVistaPasajeros(modelo, vuelo);
                return View("Pasajeros", modelo);
            }

            if (numeroAsiento.Distinct().Count() != cantidadPasajeros || numeroAsiento.Any(n => n < 1 || n > 7))
            {
                TempData["ErrorAsientos"] = "La selección de asientos no es válida.";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = cantidadPasajeros });
            }

            var asientosExistentes = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == vueloId)
                .Select(a => a.NumeroAsiento)
                .ToList();
            if (numeroAsiento.Any(n => !asientosExistentes.Contains((byte)n)))
            {
                TempData["ErrorAsientos"] = "Uno o más asientos no pertenecen a la cabina de este vuelo.";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = cantidadPasajeros });
            }

            var documentosPasajeros = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < cantidadPasajeros; i++)
            {
                string doc = (numeroDocumento[i] ?? "").Trim();
                bool documentoDuplicado = !documentosPasajeros.Add(tipoDocumento[i] + "|" + doc);
                if (documentoDuplicado)
                {
                    ModelState.AddModelError("Pasajeros[" + i + "].NumeroDocumento", "Este documento ya está repetido en la reserva.");
                }
                string codigoTipoDocumento = tipoDocumento[i];
                if (!db.TipoDocumentoIdentidads.Any(t => t.Codigo == codigoTipoDocumento))
                {
                    ModelState.AddModelError("Pasajeros[" + i + "].TipoDocumento", "Seleccione un tipo de documento válido.");
                }
            }

            if (!ModelState.IsValid)
            {
                PrepararVistaPasajeros(modelo, vuelo);
                return View("Pasajeros", modelo);
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
                         && (r.EstadoAsignacionCodigo == "CONFIRMADA" ||
                             (r.EstadoAsignacionCodigo == "PENDIENTE" &&
                              (!r.Reserva.FechaExpiracion.HasValue || r.Reserva.FechaExpiracion.Value > DateTime.Now))))
                .Any();

            if (ocupados)
            {
                TempData["ErrorAsientos"] = "Uno o más de los asientos seleccionados ya no están disponibles. Por favor elija otros.";
                return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = numeroAsiento.Length });
            }

            // TRANSACCIÓN ATÓMICA DE BASE DE DATOS (Si un asiento falla, se revierte toda la reserva)
            using (var transaccion = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    bool asientoOcupadoEnTransaccion = db.ReservaPasajeroVueloes
                        .Where(r => r.VueloId == vueloId
                                 && listaAsientosBytes.Contains(r.NumeroAsiento)
                                 && (r.EstadoAsignacionCodigo == "CONFIRMADA" ||
                                     (r.EstadoAsignacionCodigo == "PENDIENTE" &&
                                      (!r.Reserva.FechaExpiracion.HasValue || r.Reserva.FechaExpiracion.Value > DateTime.Now))))
                        .Any();
                    if (asientoOcupadoEnTransaccion)
                    {
                        transaccion.Rollback();
                        TempData["ErrorAsientos"] = "Uno o más asientos acaban de ser reservados. Elija otros.";
                        return RedirectToAction("SeleccionAsiento", new { vueloId = vueloId, pasajeros = cantidadPasajeros });
                    }

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
                catch
                {
                    transaccion.Rollback(); // REVERTIR TODO EN CASO DE ERROR
                    ModelState.AddModelError("", "No se pudo completar la reserva. Revise los datos e inténtelo de nuevo.");
                    PrepararVistaPasajeros(modelo, vuelo);
                    return View("Pasajeros", modelo);
                }
            }
        }

        private void PrepararVistaPasajeros(RegistroPasajerosViewModel modelo, vw_VueloDetalle vuelo)
        {
            modelo.Vuelo = vuelo;
            var numerosAsiento = modelo.Pasajeros
                .Where(p => p.NumeroAsiento >= 1 && p.NumeroAsiento <= 7)
                .Select(p => (byte)p.NumeroAsiento)
                .Distinct()
                .ToList();
            var asientos = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == modelo.VueloId && numerosAsiento.Contains(a.NumeroAsiento))
                .ToList();

            foreach (var pasajero in modelo.Pasajeros)
            {
                var asiento = asientos.FirstOrDefault(a => a.NumeroAsiento == pasajero.NumeroAsiento);
                if (asiento != null)
                {
                    pasajero.CodigoAsiento = asiento.CodigoAsiento;
                    pasajero.Ubicacion = asiento.Ubicacion;
                }
            }

            ViewBag.TiposDocumento = new SelectList(
                db.TipoDocumentoIdentidads.OrderBy(t => t.Codigo).ToList(),
                "Codigo",
                "Descripcion");
        }

        // ============================================================
        // PANTALLA 10: /Reservas/Confirmar/{id}
        // ============================================================

        // GET: /Reservas/Confirmar/5
        [HttpGet]
        [SessionAuthorize(Roles = "Cliente")]
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
            if (!EsCliente)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }
            string userId = User.Identity.GetUserId();

            var query = db.Reservas
                .Include(r => r.EstadoReserva)
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.Pasajero))
                .Include(r => r.Compras)
                .AsQueryable();

            query = query.Where(r => r.UsuarioId == userId);

            if (!string.IsNullOrEmpty(estado))
            {
                query = query.Where(r => r.EstadoReservaCodigo == estado);
            }

            var lista = query.OrderByDescending(r => r.FechaCreacion).ToList();

            ViewBag.VuelosDict = db.vw_VueloDetalle.ToDictionary(v => v.VueloId, v => v);
            ViewBag.EstadoSeleccionado = estado;

            return View(lista);
        }

        [HttpGet]
        [SessionAuthorize(Roles = "Counter")]
        public ActionResult Gestion()
        {
            var reservas = db.Reservas
                .Include(r => r.ReservaPasajeroVueloes.Select(a => a.Pasajero))
                .OrderByDescending(r => r.FechaCreacion)
                .Take(100)
                .ToList();
            ViewBag.VuelosDict = db.vw_VueloDetalle.ToDictionary(v => v.VueloId, v => v);
            return View("MisReservas", reservas);
        }

        // ============================================================
        // PANTALLA 14: /Reservas/Gestionar/{id}
        // ============================================================
        // GET: /Reservas/Gestionar/5
        [HttpGet]
        public ActionResult Gestionar(int? id)
        {
            if (!EsCliente && !User.IsInRole("Counter"))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }
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
            bool esCounter = User.IsInRole("Counter");
            if (!esCounter && reserva.UsuarioId != userId)
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
            ViewBag.EsCounter = esCounter;

            return View(reserva);
        }

        // POST: /Reservas/CambiarAsiento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarAsiento(int asignacionId, byte nuevoAsiento)
        {
            if (!EsCliente)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }
            var asignacion = db.ReservaPasajeroVueloes
                .Include(a => a.Reserva)
                .FirstOrDefault(a => a.AsignacionId == asignacionId);

            if (asignacion == null)
            {
                return RedirectToAction("MisReservas");
            }

            // VALIDACIÓN DE TITULARIDAD
            string userId = User.Identity.GetUserId();
            if (asignacion.Reserva.UsuarioId != userId)
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
            if (!EsCliente)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden);
            }
            var reserva = db.Reservas
                .Include(r => r.ReservaPasajeroVueloes)
                .FirstOrDefault(r => r.ReservaId == reservaId);

            if (reserva != null && reserva.EstadoReservaCodigo != "CANCELADA")
            {
                // VALIDACIÓN DE TITULARIDAD
                string userId = User.Identity.GetUserId();
                if (reserva.UsuarioId != userId)
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