using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using AerolineaVIP.Models;

namespace AerolineaVIP.Controllers
{
    [SessionAuthorize(Roles = "SuperUsuario, Administrador")]
    public class RutaController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // GET: Ruta
        public ActionResult Index()
        {
            var rutas = db.Rutas.Include(r => r.Aeropuerto).Include(r => r.Aeropuerto1).OrderBy(r => r.Aeropuerto.CodigoIATA);
            return View(rutas.ToList());
        }

        // GET: Ruta/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Ruta ruta = db.Rutas.Include(r => r.Aeropuerto).Include(r => r.Aeropuerto1).FirstOrDefault(r => r.RutaId == id.Value);
            if (ruta == null)
            {
                return HttpNotFound();
            }
            return View(ruta);
        }

        // GET: Ruta/Create
        public ActionResult Create()
        {
            CargarAeropuertos();
            return View();
        }

        // POST: Ruta/Create
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que quiere enlazarse. Para obtener 
        // más detalles, vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "RutaId,AeropuertoOrigenId,AeropuertoDestinoId,TarifaReferencial,Activa")] Ruta ruta)
        {
            if (ruta == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            foreach (var error in CatalogDomainRules.ValidateRuta(db, ruta))
            {
                ModelState.AddModelError(error.PropertyName, error.Message);
            }
            if (ModelState.IsValid)
            {
                ruta.Activa = true;
                db.Rutas.Add(ruta);
                db.SaveChanges();
                TempData["MensajeAdmin"] = "Ruta creada correctamente.";
                return RedirectToAction("Index");
            }

            CargarAeropuertos(ruta.AeropuertoOrigenId, ruta.AeropuertoDestinoId);
            return View(ruta);
        }

        // GET: Ruta/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Ruta ruta = db.Rutas.Include(r => r.Aeropuerto).Include(r => r.Aeropuerto1).FirstOrDefault(r => r.RutaId == id.Value);
            if (ruta == null)
            {
                return HttpNotFound();
            }
            CargarAeropuertos(ruta.AeropuertoOrigenId, ruta.AeropuertoDestinoId);
            return View(ruta);
        }

        // POST: Ruta/Edit/5
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que quiere enlazarse. Para obtener 
        // más detalles, vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "RutaId,AeropuertoOrigenId,AeropuertoDestinoId,TarifaReferencial,Activa")] Ruta ruta)
        {
            if (ruta == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var actual = db.Rutas.Find(ruta.RutaId);
            if (actual == null)
            {
                return HttpNotFound();
            }
            foreach (var error in CatalogDomainRules.ValidateRuta(db, ruta))
            {
                ModelState.AddModelError(error.PropertyName, error.Message);
            }
            if (ModelState.IsValid)
            {
                actual.AeropuertoOrigenId = ruta.AeropuertoOrigenId;
                actual.AeropuertoDestinoId = ruta.AeropuertoDestinoId;
                actual.TarifaReferencial = ruta.TarifaReferencial;
                actual.Activa = ruta.Activa;
                db.SaveChanges();
                TempData["MensajeAdmin"] = "Ruta actualizada correctamente.";
                return RedirectToAction("Index");
            }
            CargarAeropuertos(ruta.AeropuertoOrigenId, ruta.AeropuertoDestinoId);
            return View(ruta);
        }

        // GET: Ruta/Delete/5
        [SessionAuthorize(Roles = "SuperUsuario")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Ruta ruta = db.Rutas.Include(r => r.Aeropuerto).Include(r => r.Aeropuerto1).FirstOrDefault(r => r.RutaId == id.Value);
            if (ruta == null)
            {
                return HttpNotFound();
            }
            return View(ruta);
        }

        // POST: Ruta/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "SuperUsuario")]
        public ActionResult DeleteConfirmed(int id)
        {
            Ruta ruta = db.Rutas.Find(id);
            if (ruta == null)
            {
                return HttpNotFound();
            }
            if (!CatalogDomainRules.PuedeEliminarRuta(db, id))
            {
                TempData["ErrorAdmin"] = "No se puede eliminar una ruta con frecuencias o vuelos asociados. Desactívela en Editar.";
                return RedirectToAction("Index");
            }
            db.Rutas.Remove(ruta);
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Ruta eliminada.";
            return RedirectToAction("Index");
        }

        private void CargarAeropuertos(int? origenId = null, int? destinoId = null)
        {
            var aeropuertos = db.Aeropuertoes.Where(a => a.Activo).OrderBy(a => a.CodigoIATA).ToList();
            ViewBag.AeropuertoOrigenId = new SelectList(aeropuertos, "AeropuertoId", "CodigoIATA", origenId);
            ViewBag.AeropuertoDestinoId = new SelectList(aeropuertos, "AeropuertoId", "CodigoIATA", destinoId);
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
