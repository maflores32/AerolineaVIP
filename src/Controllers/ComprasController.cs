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
    [SessionAuthorize(Roles = "Cliente")]
    public class ComprasController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // ============================================================
        // PANTALLA 11: /Compras/Pagar?reservaId=X
        // ============================================================
        [HttpGet]
        public ActionResult Pagar(int? reservaId)
        {
            if (!reservaId.HasValue)
            {
                return RedirectToAction("MisReservas", "Reservas");
            }

            var reserva = db.Reservas
                .Include(r => r.ReservaPasajeroVueloes.Select(rpv => rpv.Pasajero))
                .FirstOrDefault(r => r.ReservaId == reservaId.Value);

            if (reserva == null)
            {
                return HttpNotFound();
            }

            if (reserva.UsuarioId != User.Identity.GetUserId())
            {
                return HttpNotFound();
            }

            if (reserva.EstadoReservaCodigo == "CONFIRMADA")
            {
                var compraExistente = db.Compras.FirstOrDefault(c => c.ReservaId == reserva.ReservaId && c.EstadoCompraCodigo == "PAGADA");
                if (compraExistente != null)
                {
                    return RedirectToAction("Confirmacion", new { id = compraExistente.CompraId });
                }
            }

            if (reserva.EstadoReservaCodigo != "PENDIENTE" ||
                (reserva.FechaExpiracion.HasValue && reserva.FechaExpiracion.Value <= DateTime.Now))
            {
                TempData["ErrorPago"] = "La reserva ya no está disponible para pago.";
                return RedirectToAction("MisReservas", "Reservas");
            }

            var primerTramo = reserva.ReservaPasajeroVueloes.FirstOrDefault();
            vw_VueloDetalle vuelo = null;
            if (primerTramo != null)
            {
                vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == primerTramo.VueloId);
            }

            ViewBag.Vuelo = vuelo;
            ViewBag.Marcas = db.MarcaTarjetas.Where(m => m.Activa).OrderBy(m => m.Nombre).ToList();
            ViewBag.Metodos = db.MetodoPagoes.Where(m => m.Activo).OrderBy(m => m.Nombre).ToList();

            return View(reserva);
        }

        // ============================================================
        // PROCESAR PAGO SIMULADO (POST)
        // ============================================================

        // POST: /Compras/ProcesarPago
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcesarPago(
            int reservaId,
            string metodoPagoCodigo,
            string marcaTarjetaCodigo,
            string numeroTarjeta,
            string nombreTitular,
            string mesExpiracion,
            string anioExpiracion,
            string cvv)
        {
            var reserva = db.Reservas
                .Include(r => r.ReservaPasajeroVueloes)
                .FirstOrDefault(r => r.ReservaId == reservaId);

            if (reserva == null)
            {
                TempData["ErrorPago"] = "La reserva no está disponible para procesar pagos.";
                return RedirectToAction("MisReservas", "Reservas");
            }

            string userId = User.Identity.GetUserId();
            if (reserva.UsuarioId != userId)
            {
                return HttpNotFound();
            }

            if (reserva.EstadoReservaCodigo != "PENDIENTE" ||
                (reserva.FechaExpiracion.HasValue && reserva.FechaExpiracion.Value <= DateTime.Now))
            {
                TempData["ErrorPago"] = "La reserva ya fue procesada o expiró.";
                return RedirectToAction("MisReservas", "Reservas");
            }

            // ============================================================
            // VALIDACIÓN DEL FORMATO DE LA TARJETA EN EL SERVIDOR
            // ============================================================
            string soloNumeros = new string((numeroTarjeta ?? "").Where(char.IsDigit).ToArray());
            if ((numeroTarjeta ?? "").Any(c => !char.IsDigit(c) && c != ' ' && c != '-') || soloNumeros.Length < 13 || soloNumeros.Length > 19)
            {
                TempData["ErrorPago"] = "El número de tarjeta debe contener entre 13 y 19 dígitos numéricos.";
                return RedirectToAction("Pagar", new { reservaId = reservaId });
            }

            string soloCvv = new string((cvv ?? "").Where(char.IsDigit).ToArray());
            if ((cvv ?? "").Any(c => !char.IsDigit(c)) || soloCvv.Length < 3 || soloCvv.Length > 4)
            {
                TempData["ErrorPago"] = "El código de seguridad (CVV) debe contener 3 o 4 dígitos numéricos.";
                return RedirectToAction("Pagar", new { reservaId = reservaId });
            }

            int mes, anio;
            if (!int.TryParse(mesExpiracion, out mes) || mes < 1 || mes > 12 ||
                !int.TryParse(anioExpiracion, out anio) || anio < DateTime.Today.Year ||
                (anio == DateTime.Today.Year && mes < DateTime.Today.Month))
            {
                TempData["ErrorPago"] = "Ingrese una fecha de vencimiento válida y vigente.";
                return RedirectToAction("Pagar", new { reservaId = reservaId });
            }

            string titular = (nombreTitular ?? "").Trim();
            if (titular.Length < 3 || titular.Length > 80 || titular.Any(c => !char.IsLetter(c) && c != ' ' && c != '\'' && c != '-'))
            {
                TempData["ErrorPago"] = "Ingrese el nombre del titular usando letras y espacios.";
                return RedirectToAction("Pagar", new { reservaId = reservaId });
            }

            bool metodoActivo = db.MetodoPagoes.Any(m => m.Codigo == metodoPagoCodigo && m.Activo);
            bool marcaActiva = db.MarcaTarjetas.Any(m => m.Codigo == marcaTarjetaCodigo && m.Activa);
            if (!metodoActivo || !marcaActiva)
            {
                TempData["ErrorPago"] = "Seleccione un método y una marca de tarjeta disponibles.";
                return RedirectToAction("Pagar", new { reservaId = reservaId });
            }

            string cuatroDigitos = soloNumeros.Substring(soloNumeros.Length - 4);

            // Regla de simulación: si la tarjeta termina en '0000', se simula un RECHAZO BANCARIO (como en el script SQL)
            bool esRechazada = cuatroDigitos == "0000";

            using (var transaccion = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    db.Entry(reserva).Reload();
                    if (reserva.EstadoReservaCodigo == "CONFIRMADA")
                    {
                        var compraPagada = db.Compras.FirstOrDefault(c => c.ReservaId == reserva.ReservaId && c.EstadoCompraCodigo == "PAGADA");
                        if (compraPagada != null)
                        {
                            transaccion.Commit();
                            return RedirectToAction("Confirmacion", new { id = compraPagada.CompraId });
                        }
                    }

                    if (reserva.UsuarioId != User.Identity.GetUserId() || reserva.EstadoReservaCodigo != "PENDIENTE" ||
                        (reserva.FechaExpiracion.HasValue && reserva.FechaExpiracion.Value <= DateTime.Now))
                    {
                        transaccion.Rollback();
                        TempData["ErrorPago"] = "La reserva ya no está disponible para pago.";
                        return RedirectToAction("MisReservas", "Reservas");
                    }

                    foreach (var asignacion in reserva.ReservaPasajeroVueloes)
                    {
                        db.Entry(asignacion).Reload();
                    }
                    decimal totalPagar = reserva.ReservaPasajeroVueloes.Sum(x => x.ImporteTramo);

                    // 1. Crear registro de Compra
                    var nuevaCompra = new Compra
                    {
                        ReservaId = reserva.ReservaId,
                        FechaCompra = DateTime.Now,
                        Importe = totalPagar,
                        MonedaCodigo = "USD",
                        EstadoCompraCodigo = esRechazada ? "PENDIENTE" : "PAGADA",
                        ClaveIdempotencia = Guid.NewGuid()
                    };
                    db.Compras.Add(nuevaCompra);
                    db.SaveChanges();

                    // 2. Registrar el intento de pago
                    string codOperacion = "SIM-" + reserva.CodigoReserva + "-" + DateTime.Now.ToString("HHmmss");
                    var nuevoPago = new Pago
                    {
                        CompraId = nuevaCompra.CompraId,
                        FechaPago = DateTime.Now,
                        Importe = totalPagar,
                        EstadoPagoCodigo = esRechazada ? "RECHAZADO" : "APROBADO",
                        MetodoPagoCodigo = !string.IsNullOrEmpty(metodoPagoCodigo) ? metodoPagoCodigo : "TARJETA_CREDITO",
                        MarcaTarjetaCodigo = !string.IsNullOrEmpty(marcaTarjetaCodigo) ? marcaTarjetaCodigo : "VISA",
                        UltimosCuatroDigitos = cuatroDigitos,
                        NombreTitular = titular.ToUpperInvariant(),
                        CodigoOperacion = codOperacion,
                        EsSimulado = true,
                        MensajeResultado = esRechazada ? "Pago simulado rechazado por fondos insuficientes (demostración)." : "Pago simulado aprobado exitosamente."
                    };
                    db.Pagoes.Add(nuevoPago);

                    if (esRechazada)
                    {
                        db.SaveChanges();
                        transaccion.Commit();
                        TempData["ErrorPago"] = "La transacción ha sido rechazada por el emisor simulado (terminación 0000). Intente con otra tarjeta.";
                        return RedirectToAction("Pagar", new { reservaId = reservaId });
                    }

                    // 3. Si es aprobada: Cambiar reserva a CONFIRMADA
                    reserva.EstadoReservaCodigo = "CONFIRMADA";

                    // 4. Cambiar asientos a CONFIRMADA (rojo en cabina)
                    foreach (var asig in reserva.ReservaPasajeroVueloes)
                    {
                        asig.EstadoAsignacionCodigo = "CONFIRMADA";
                    }

                    // 5. Emitir Comprobantes
                    string numConfirmacion = "CNF-" + nuevaCompra.CompraId.ToString("D6");
                    string numFactura = "ACAD-" + nuevaCompra.CompraId.ToString("D6");

                    db.Comprobantes.Add(new Comprobante
                    {
                        CompraId = nuevaCompra.CompraId,
                        TipoComprobanteCodigo = "CONFIRMACION",
                        NumeroComprobante = numConfirmacion,
                        FechaEmision = DateTime.Now,
                        EstadoComprobanteCodigo = "EMITIDO"
                    });

                    db.Comprobantes.Add(new Comprobante
                    {
                        CompraId = nuevaCompra.CompraId,
                        TipoComprobanteCodigo = "FACTURA",
                        NumeroComprobante = numFactura,
                        FechaEmision = DateTime.Now,
                        EstadoComprobanteCodigo = "EMITIDO"
                    });

                    db.SaveChanges();
                    transaccion.Commit(); // Confirmar transacción

                    return RedirectToAction("Confirmacion", new { id = nuevaCompra.CompraId });
                }
                catch (Exception ex)
                {
                    transaccion.Rollback();
                    TempData["ErrorPago"] = "No se pudo completar el pago. Revise la reserva e inténtelo de nuevo.";
                    return RedirectToAction("Pagar", new { reservaId = reservaId });
                }
            }
        }

        [HttpGet]
        public ActionResult Confirmacion(int? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("MisReservas", "Reservas");
            }

            var compra = db.Compras
                .Include(c => c.Reserva.ReservaPasajeroVueloes.Select(a => a.Pasajero))
                .Include(c => c.Pagoes)
                .Include(c => c.Comprobantes)
                .FirstOrDefault(c => c.CompraId == id.Value && c.EstadoCompraCodigo == "PAGADA");

            if (compra == null || compra.Reserva.UsuarioId != User.Identity.GetUserId())
            {
                return HttpNotFound();
            }

            var primerTramo = compra.Reserva.ReservaPasajeroVueloes.FirstOrDefault();
            ViewBag.Vuelo = primerTramo == null ? null : db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == primerTramo.VueloId);
            return View(compra);
        }

        // GET: /Comprobantes/Imprimir/5
        [HttpGet]
        public ActionResult Imprimir(int? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("Index");
            }

            var comprobante = db.vw_ComprobanteDetalle.FirstOrDefault(c => c.ComprobanteId == id.Value);
            if (comprobante == null)
            {
                return HttpNotFound();
            }

            // VALIDACIÓN DE TITULARIDAD: Solo el dueño o el personal puede ver el comprobante
            string userId = User.Identity.GetUserId();
            if (comprobante.UsuarioId != userId)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "No tiene permisos para ver este comprobante.");
            }

            var emisor = db.DatosEmisors.FirstOrDefault();
            var asignaciones = db.ReservaPasajeroVueloes
                .Include(a => a.Pasajero)
                .Include(a => a.Vuelo)
                .Where(a => a.ReservaId == comprobante.ReservaId)
                .ToList();

            ViewBag.Emisor = emisor;
            ViewBag.Asignaciones = asignaciones;

            return View(comprobante);
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