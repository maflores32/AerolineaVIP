using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using AerolineaVIP.Models;

namespace AerolineaVIP.Controllers
{
    public class AeronavesController : Controller
    {
        private AerolineaVIP_Entities db = new AerolineaVIP_Entities();

        // GET: Aeronaves
        [Authorize(Roles = "Details")]
        public ActionResult Index()
        {
            var aeronaves = db.Aeronaves.Include(a => a.EstadoAeronave).Include(a => a.ModeloAeronave);
            return View(aeronaves.ToList());
        }

        // GET: Aeronaves/Details/5
        [Authorize(Roles = "Details")]
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aeronave aeronave = db.Aeronaves.Find(id);
            if (aeronave == null)
            {
                return HttpNotFound();
            }
            return View(aeronave);
        }

        // GET: Aeronaves/Create
        [Authorize(Roles = "Create")]
        public ActionResult Create()
        {
            ViewBag.EstadoAeronaveCodigo = new SelectList(db.EstadoAeronaves, "Codigo", "Descripcion");
            ViewBag.ModeloAeronaveId = new SelectList(db.ModeloAeronaves, "ModeloAeronaveId", "Nombre");
            return View();
        }

        // POST: Aeronaves/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Create")]
        public ActionResult Create([Bind(Include = "AeronaveId,Matricula,ModeloAeronaveId,EstadoAeronaveCodigo")] Aeronave aeronave)
        {
            if (ModelState.IsValid)
            {
                db.Aeronaves.Add(aeronave);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.EstadoAeronaveCodigo = new SelectList(db.EstadoAeronaves, "Codigo", "Descripcion", aeronave.EstadoAeronaveCodigo);
            ViewBag.ModeloAeronaveId = new SelectList(db.ModeloAeronaves, "ModeloAeronaveId", "Nombre", aeronave.ModeloAeronaveId);
            return View(aeronave);
        }

        // GET: Aeronaves/Edit/5
        [Authorize(Roles = "Edit")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aeronave aeronave = db.Aeronaves.Find(id);
            if (aeronave == null)
            {
                return HttpNotFound();
            }
            ViewBag.EstadoAeronaveCodigo = new SelectList(db.EstadoAeronaves, "Codigo", "Descripcion", aeronave.EstadoAeronaveCodigo);
            ViewBag.ModeloAeronaveId = new SelectList(db.ModeloAeronaves, "ModeloAeronaveId", "Nombre", aeronave.ModeloAeronaveId);
            return View(aeronave);
        }

        // POST: Aeronaves/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Edit")]
        public ActionResult Edit([Bind(Include = "AeronaveId,Matricula,ModeloAeronaveId,EstadoAeronaveCodigo")] Aeronave aeronave)
        {
            if (ModelState.IsValid)
            {
                db.Entry(aeronave).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.EstadoAeronaveCodigo = new SelectList(db.EstadoAeronaves, "Codigo", "Descripcion", aeronave.EstadoAeronaveCodigo);
            ViewBag.ModeloAeronaveId = new SelectList(db.ModeloAeronaves, "ModeloAeronaveId", "Nombre", aeronave.ModeloAeronaveId);
            return View(aeronave);
        }

        // GET: Aeronaves/Delete/5
        // REGLA CLAVE: Solo el SuperUsuario tiene el rol 'Delete'; el Administrador NO puede eliminar
        [Authorize(Roles = "Delete")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aeronave aeronave = db.Aeronaves.Find(id);
            if (aeronave == null)
            {
                return HttpNotFound();
            }
            return View(aeronave);
        }

        // POST: Aeronaves/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Delete")]
        public ActionResult DeleteConfirmed(int id)
        {
            Aeronave aeronave = db.Aeronaves.Find(id);
            db.Aeronaves.Remove(aeronave);
            db.SaveChanges();
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