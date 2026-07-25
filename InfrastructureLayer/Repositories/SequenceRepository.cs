using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Threading.Tasks;

namespace InfrastructureLayer.Repositories
{
    public class SequenceRepository : ISequenceRepository
    {
        private readonly SNMDbContext _context;

        public SequenceRepository(SNMDbContext context)
        {
            _context = context;
        }

        public async Task<long> NextPayOSOrderCodeAsync()
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT nextval('payos_order_code_seq')";
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
    }
}
