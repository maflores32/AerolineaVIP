using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using AerolineaVIP.Models;

namespace AerolineaVIP.Controllers
{
    public class VuelosController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // ============================================================
        // PANTALLA 5: /Vuelos/Buscar
        // ============================================================
        [AllowAnonymous]
        public ActionResult Buscar(string origen, string destino, DateTime? fecha, int? pasajeros)
        {
            CargarCombosBusqueda(origen, destino, fecha, pasajeros);
            return View();
        }

        // ============================================================
        // PANTALLA 6: /Vuelos/Resultados
        // ============================================================
        [AllowAnonymous]
        public ActionResult Resultados(string origen, string destino, DateTime? fecha, int? pasajeros)
        {
            int numPasajeros = pasajeros ?? 1;
            if (numPasajeros < 1) numPasajeros = 1;
            if (numPasajeros > 7) numPasajeros = 7;

            CargarCombosBusqueda(origen, destino, fecha, numPasajeros);

            // Validación: Origen y Destino no pueden ser el mismo aeropuerto
            if (!string.IsNullOrEmpty(origen) && !string.IsNullOrEmpty(destino) && origen == destino)
            {
                ViewBag.ErrorBusqueda = "El aeropuerto de origen y el de destino no pueden ser el mismo.";
                ViewBag.DisponibilidadDict = new Dictionary<int, vw_DisponibilidadVuelo>();
                return View(new List<vw_VueloDetalle>());
            }

            var vuelos = db.vw_VueloDetalle
                .Where(v => v.EstadoVueloCodigo == "PROGRAMADO" || v.EstadoVueloCodigo == "CONFIRMADO")
                .AsQueryable();

            if (!string.IsNullOrEmpty(origen))
            {
                var aeroOrigen = db.Aeropuertoes.FirstOrDefault(a => a.CodigoIATA == origen);
                if (aeroOrigen != null)
                {
                    vuelos = vuelos.Where(v => v.AeropuertoOrigenId == aeroOrigen.AeropuertoId);
                }
            }

            if (!string.IsNullOrEmpty(destino))
            {
                var aeroDestino = db.Aeropuertoes.FirstOrDefault(a => a.CodigoIATA == destino);
                if (aeroDestino != null)
                {
                    vuelos = vuelos.Where(v => v.AeropuertoDestinoId == aeroDestino.AeropuertoId);
                }
            }

            if (fecha.HasValue)
            {
                DateTime fechaFiltro = fecha.Value.Date;
                vuelos = vuelos.Where(v => DbFunctions.TruncateTime(v.FechaOperacion) == fechaFiltro);
            }

            var listaVuelos = vuelos
                .OrderBy(v => v.FechaOperacion)
                .ThenBy(v => v.HoraSalida)
                .ToList();

            // Diccionario de disponibilidad (libres y ocupados de los 7 asientos)
            var dispDict = db.vw_DisponibilidadVuelo
                .ToDictionary(d => d.VueloId, d => d);

            // Filtramos para mostrar vuelos que tengan suficientes asientos disponibles
            listaVuelos = listaVuelos
                .Where(v => !dispDict.ContainsKey(v.VueloId) || (dispDict[v.VueloId].Disponibles ?? 7) >= numPasajeros)
                .ToList();

            ViewBag.DisponibilidadDict = dispDict;
            ViewBag.Pasajeros = numPasajeros;

            return View(listaVuelos);
        }

        // ============================================================
        // PANTALLA 7: /Vuelos/Disponibilidad/{id}
        // ============================================================
        [AllowAnonymous]
        public ActionResult Disponibilidad(int? id, int? pasajeros)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("Buscar");
            }

            int vueloId = id.Value;
            var vuelo = db.vw_VueloDetalle.FirstOrDefault(v => v.VueloId == vueloId);
            if (vuelo == null)
            {
                return HttpNotFound();
            }

            var disponibilidad = db.vw_DisponibilidadVuelo.FirstOrDefault(d => d.VueloId == vueloId);
            var asientos = db.vw_EstadoAsientoVuelo
                .Where(a => a.VueloId == vueloId)
                .OrderBy(a => a.Fila)
                .ThenBy(a => a.Lado)
                .ToList();

            ViewBag.Disponibilidad = disponibilidad;
            ViewBag.Asientos = asientos;
            ViewBag.Pasajeros = pasajeros ?? 1;

            return View(vuelo);
        }

        private void CargarCombosBusqueda(string origen, string destino, DateTime? fecha, int? pasajeros)
        {
            var aeropuertos = db.Aeropuertoes
                .Where(a => a.Activo)
                .OrderBy(a => a.Localidad)
                .ToList()
                .Select(a => new
                {
                    Codigo = a.CodigoIATA,
                    Texto = a.Localidad + " (" + a.CodigoIATA + ") - " + a.Nombre
                })
                .ToList();

            ViewBag.origen = new SelectList(aeropuertos, "Codigo", "Texto", origen);
            ViewBag.destino = new SelectList(aeropuertos, "Codigo", "Texto", destino);
            ViewBag.fecha = fecha.HasValue ? fecha.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.pasajeros = pasajeros ?? 1;
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