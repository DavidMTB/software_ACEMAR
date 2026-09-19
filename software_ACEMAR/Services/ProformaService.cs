using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IProformaService
    {
        bool CrearProforma(Proforma proforma, out string error);
        List<Proforma> ObtenerTodas();
        List<Proforma> ObtenerPendientes();
        Proforma? ObtenerPorId(int id);
        bool MarcarConvertidaAVenta(int id);
        bool AnularProforma(int id);
    }

    public class ProformaService : IProformaService
    {
        public bool CrearProforma(Proforma proforma, out string error)
        {
            error = string.Empty;

            if (proforma.Detalles == null || !proforma.Detalles.Any())
            {
                error = "La proforma debe contener al menos un producto.";
                return false;
            }

            using var context = new AppDbContext();
            int count = context.Proformas.Count() + 1;
            proforma.NumeroProforma = $"PRF-{DateTime.Now:yyyyMMdd}-{count:D4}";
            proforma.Fecha = DateTime.Now;
            proforma.Estado = "Pendiente";
            proforma.Total = Math.Round(proforma.Detalles.Sum(d => d.Subtotal), 2);

            context.Proformas.Add(proforma);
            context.SaveChanges();
            return true;
        }

        public List<Proforma> ObtenerTodas()
        {
            using var context = new AppDbContext();
            return context.Proformas
                .Include(p => p.Detalles)
                .OrderByDescending(p => p.Fecha)
                .ToList();
        }

        public List<Proforma> ObtenerPendientes()
        {
            using var context = new AppDbContext();
            return context.Proformas
                .Include(p => p.Detalles)
                .Where(p => p.Estado == "Pendiente")
                .OrderByDescending(p => p.Fecha)
                .ToList();
        }

        public Proforma? ObtenerPorId(int id)
        {
            using var context = new AppDbContext();
            return context.Proformas
                .Include(p => p.Detalles)
                .FirstOrDefault(p => p.Id == id);
        }

        public bool MarcarConvertidaAVenta(int id)
        {
            using var context = new AppDbContext();
            var p = context.Proformas.FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                p.Estado = "Convertida";
                context.SaveChanges();
                return true;
            }
            return false;
        }

        public bool AnularProforma(int id)
        {
            using var context = new AppDbContext();
            var p = context.Proformas.FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                p.Estado = "Anulada";
                context.SaveChanges();
                return true;
            }
            return false;
        }
    }
}
