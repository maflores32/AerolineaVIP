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
    public class AeropuertoController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // GET: Aeropuerto
        public ActionResult Index()
        {
            return View(db.Aeropuertoes.OrderBy(a => a.Localidad).ThenBy(a => a.CodigoIATA).ToList());
        }

        // GET: Aeropuerto/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aeropuerto aeropuerto = db.Aeropuertoes
                .Include(a => a.Rutas)
                .Include(a => a.Rutas1)
                .FirstOrDefault(a => a.AeropuertoId == id.Value);
            if (aeropuerto == null)
            {
                return HttpNotFound();
            }
            return View(aeropuerto);
        }

        // GET: Aeropuerto/Create
        public ActionResult Create()
        {
            ViewBag.CodigoIataSugerido = "";
            return View();
        }

        // POST: Aeropuerto/Create
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que quiere enlazarse. Para obtener 
        // más detalles, vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "AeropuertoId,CodigoIATA,Nombre,Localidad,Activo")] Aeropuerto aeropuerto)
        {
            if (aeropuerto == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            aeropuerto.CodigoIATA = CatalogDomainRules.NormalizeIata(aeropuerto.CodigoIATA);
            aeropuerto.Nombre = (aeropuerto.Nombre ?? "").Trim();
            aeropuerto.Localidad = (aeropuerto.Localidad ?? "").Trim();
            foreach (var error in CatalogDomainRules.ValidateAeropuerto(db, aeropuerto))
            {
                ModelState.AddModelError(error.PropertyName, error.Message);
            }
            if (ModelState.IsValid)
            {
                aeropuerto.Activo = true;
                db.Aeropuertoes.Add(aeropuerto);
                db.SaveChanges();
                TempData["MensajeAdmin"] = "Aeropuerto creado correctamente.";
                return RedirectToAction("Index");
            }

            return View(aeropuerto);
        }

        // GET: Aeropuerto/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aeropuerto aeropuerto = db.Aeropuertoes.Find(id);
            if (aeropuerto == null)
            {
                return HttpNotFound();
            }
            return View(aeropuerto);
        }

        // POST: Aeropuerto/Edit/5
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que quiere enlazarse. Para obtener 
        // más detalles, vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "AeropuertoId,CodigoIATA,Nombre,Localidad,Activo")] Aeropuerto aeropuerto)
        {
            if (aeropuerto == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var actual = db.Aeropuertoes.Find(aeropuerto.AeropuertoId);
            if (actual == null)
            {
                return HttpNotFound();
            }
            aeropuerto.CodigoIATA = CatalogDomainRules.NormalizeIata(aeropuerto.CodigoIATA);
            aeropuerto.Nombre = (aeropuerto.Nombre ?? "").Trim();
            aeropuerto.Localidad = (aeropuerto.Localidad ?? "").Trim();
            foreach (var error in CatalogDomainRules.ValidateAeropuerto(db, aeropuerto))
            {
                ModelState.AddModelError(error.PropertyName, error.Message);
            }
            if (ModelState.IsValid)
            {
                actual.CodigoIATA = aeropuerto.CodigoIATA;
                actual.Nombre = aeropuerto.Nombre;
                actual.Localidad = aeropuerto.Localidad;
                actual.Activo = aeropuerto.Activo;
                db.SaveChanges();
                TempData["MensajeAdmin"] = "Aeropuerto actualizado correctamente.";
                return RedirectToAction("Index");
            }
            return View(aeropuerto);
        }

        // GET: Aeropuerto/Delete/5
        [SessionAuthorize(Roles = "SuperUsuario")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aeropuerto aeropuerto = db.Aeropuertoes.Find(id);
            if (aeropuerto == null)
            {
                return HttpNotFound();
            }
            return View(aeropuerto);
        }

        // POST: Aeropuerto/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(Roles = "SuperUsuario")]
        public ActionResult DeleteConfirmed(int id)
        {
            Aeropuerto aeropuerto = db.Aeropuertoes.Find(id);
            if (aeropuerto == null)
            {
                return HttpNotFound();
            }
            if (!CatalogDomainRules.PuedeEliminarAeropuerto(db, id))
            {
                TempData["ErrorAdmin"] = "No se puede eliminar un aeropuerto asociado a rutas. Desactívelo desde Editar.";
                return RedirectToAction("Index");
            }
            db.Aeropuertoes.Remove(aeropuerto);
            db.SaveChanges();
            TempData["MensajeAdmin"] = "Aeropuerto eliminado.";
            return RedirectToAction("Index");
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
