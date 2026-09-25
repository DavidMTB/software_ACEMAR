using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using software_ACEMAR.Data;
using software_ACEMAR.Models;

namespace software_ACEMAR.Services
{
    public interface IProformaService
    {
        bool CrearProforma(Proforma proforma, out string error);
        Task<(bool Exito, string Error)> CrearProformaAsync(Proforma proforma, CancellationToken cancellationToken = default);

        List<Proforma> ObtenerTodas();
        Task<List<Proforma>> ObtenerTodasAsync(CancellationToken cancellationToken = default);

        List<Proforma> ObtenerPendientes();
        Task<List<Proforma>> ObtenerPendientesAsync(CancellationToken cancellationToken = default);

        Proforma? ObtenerPorId(int id);
        Task<Proforma?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

        bool MarcarConvertidaAVenta(int id);
        Task<bool> MarcarConvertidaAVentaAsync(int id, CancellationToken cancellationToken = default);

        bool AnularProforma(int id);
        Task<bool> AnularProformaAsync(int id, CancellationToken cancellationToken = default);
    }

    public class ProformaService : IProformaService
    {
        public bool CrearProforma(Proforma proforma, out string error)
        {
            var (exito, err) = CrearProformaAsync(proforma).GetAwaiter().GetResult();
            error = err;
            return exito;
        }

        public async Task<(bool Exito, string Error)> CrearProformaAsync(Proforma proforma, CancellationToken cancellationToken = default)
        {
            if (proforma.Detalles == null || !proforma.Detalles.Any())
            {
                return (false, "La proforma debe contener al menos un producto.");
            }

            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var maxId = await context.Proformas.MaxAsync(p => (int?)p.Id, cancellationToken) ?? 0;
                proforma.NumeroProforma = $"PRF-{DateTime.Now:yyyyMMdd}-{(maxId + 1):D4}";
                proforma.Fecha = DateTime.Now;
                proforma.Estado = "Pendiente";
                proforma.Total = Math.Round(proforma.Detalles.Sum(d => d.Subtotal), 2);

                await context.Proformas.AddAsync(proforma, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, $"Error al registrar la proforma: {ex.Message}");
            }
        }

        public List<Proforma> ObtenerTodas()
        {
            return ObtenerTodasAsync().GetAwaiter().GetResult();
        }

        public async Task<List<Proforma>> ObtenerTodasAsync(CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.Proformas
                .AsNoTracking()
                .Include(p => p.Detalles)
                .OrderByDescending(p => p.Fecha)
                .ToListAsync(cancellationToken);
        }

        public List<Proforma> ObtenerPendientes()
        {
            return ObtenerPendientesAsync().GetAwaiter().GetResult();
        }

        public async Task<List<Proforma>> ObtenerPendientesAsync(CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.Proformas
                .AsNoTracking()
                .Include(p => p.Detalles)
                .Where(p => p.Estado == "Pendiente")
                .OrderByDescending(p => p.Fecha)
                .ToListAsync(cancellationToken);
        }

        public Proforma? ObtenerPorId(int id)
        {
            return ObtenerPorIdAsync(id).GetAwaiter().GetResult();
        }

        public async Task<Proforma?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            return await context.Proformas
                .AsNoTracking()
                .Include(p => p.Detalles)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public bool MarcarConvertidaAVenta(int id)
        {
            return MarcarConvertidaAVentaAsync(id).GetAwaiter().GetResult();
        }

        public async Task<bool> MarcarConvertidaAVentaAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            var p = await context.Proformas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (p != null)
            {
                p.Estado = "Convertida";
                await context.SaveChangesAsync(cancellationToken);
                return true;
            }
            return false;
        }

        public bool AnularProforma(int id)
        {
            return AnularProformaAsync(id).GetAwaiter().GetResult();
        }

        public async Task<bool> AnularProformaAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = new AppDbContext();
            var p = await context.Proformas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (p != null)
            {
                p.Estado = "Anulada";
                await context.SaveChangesAsync(cancellationToken);
                return true;
            }
            return false;
        }
    }
}
